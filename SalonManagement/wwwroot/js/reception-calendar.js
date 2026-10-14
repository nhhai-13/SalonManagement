// reception-calendar.js - Màn hình Lịch ngày và Điều chỉnh lịch hẹn cho Lễ tân (S3-04)
window.ReceptionCalendar = (() => {
    let currentDate = new Date().toISOString().split("T")[0];
    let currentAppointmentDetail = null;
    let rescheduleModalInstance = null;

    async function authFetch(url, options = {}) {
        if (window.SalonAuth && typeof window.SalonAuth.authenticatedFetch === "function") {
            return await window.SalonAuth.authenticatedFetch(url, options);
        }
        return await fetch(url, options);
    }

    function init() {
        const dateInput = document.getElementById("calendar-date-input");
        const btnPrev = document.getElementById("btn-prev-day");
        const btnNext = document.getElementById("btn-next-day");
        const btnToday = document.getElementById("btn-today");
        const btnRefresh = document.getElementById("btn-refresh-calendar");

        if (dateInput) {
            dateInput.value = currentDate;
            dateInput.addEventListener("change", (e) => {
                currentDate = e.target.value;
                loadDailySchedule(currentDate);
            });
        }

        if (btnPrev) {
            btnPrev.addEventListener("click", () => {
                changeDate(-1);
            });
        }

        if (btnNext) {
            btnNext.addEventListener("click", () => {
                changeDate(1);
            });
        }

        if (btnToday) {
            btnToday.addEventListener("click", () => {
                currentDate = new Date().toISOString().split("T")[0];
                if (dateInput) dateInput.value = currentDate;
                loadDailySchedule(currentDate);
            });
        }

        if (btnRefresh) {
            btnRefresh.addEventListener("click", () => {
                loadDailySchedule(currentDate);
            });
        }

        bindRescheduleModalEvents();
        loadDailySchedule(currentDate);
    }

    function changeDate(daysOffset) {
        const d = new Date(currentDate);
        d.setDate(d.getDate() + daysOffset);
        currentDate = d.toISOString().split("T")[0];
        const dateInput = document.getElementById("calendar-date-input");
        if (dateInput) dateInput.value = currentDate;
        loadDailySchedule(currentDate);
    }

    async function loadDailySchedule(date) {
        const loadingIndicator = document.getElementById("calendar-loading");
        const gridContainer = document.getElementById("calendar-grid-container");
        const dateDisplay = document.getElementById("calendar-current-date-display");

        if (loadingIndicator) loadingIndicator.classList.remove("d-none");
        if (gridContainer) gridContainer.classList.add("opacity-50");

        try {
            const res = await authFetch(`/api/reception/daily-schedule?date=${encodeURIComponent(date)}`);
            if (!res.ok) {
                console.error("Lỗi tải lịch ngày:", res.status);
                return;
            }

            const data = await res.json();
            if (dateDisplay) {
                dateDisplay.textContent = data.formattedDate || date;
            }
            renderScheduleGrid(data);
        } catch (err) {
            console.error("Lỗi kết nối khi tải lịch:", err);
        } finally {
            if (loadingIndicator) loadingIndicator.classList.add("d-none");
            if (gridContainer) gridContainer.classList.remove("opacity-50");
        }
    }

    function renderScheduleGrid(scheduleData) {
        const gridContainer = document.getElementById("calendar-grid-container");
        if (!gridContainer) return;

        if (scheduleData.isClosed) {
            gridContainer.innerHTML = `
                <div class="col-12 text-center py-5">
                    <i class="bi bi-door-closed fs-1 text-muted d-block mb-3"></i>
                    <h5 class="text-muted">Salon nghỉ làm việc vào ngày này</h5>
                    <p class="text-secondary small">Không có ca làm việc hoặc lịch hẹn.</p>
                </div>
            `;
            return;
        }

        if (!scheduleData.stylists || scheduleData.stylists.length === 0) {
            gridContainer.innerHTML = `
                <div class="col-12 text-center py-5">
                    <i class="bi bi-people fs-1 text-muted d-block mb-3"></i>
                    <h5 class="text-muted">Không có thợ nào hoạt động trong hệ thống</h5>
                </div>
            `;
            return;
        }

        let html = `<div class="d-flex gap-3 overflow-auto pb-4" style="min-height: 480px;">`;

        scheduleData.stylists.forEach(stylist => {
            const hasShift = stylist.shifts && stylist.shifts.length > 0;
            const shiftText = hasShift 
                ? stylist.shifts.map(s => `${s.startTime.slice(0, 5)} - ${s.endTime.slice(0, 5)}`).join(", ") 
                : "Nghỉ";

            html += `
                <div class="stylist-column flex-shrink-0" style="width: 290px;">
                    <div class="card h-100 shadow-sm border-0 bg-light">
                        <div class="card-header bg-white border-bottom py-3 d-flex align-items-center gap-2">
                            <div class="rounded-circle bg-primary-subtle text-primary fw-bold d-flex align-items-center justify-content-center" style="width: 40px; height: 40px; font-size: 1.1rem;">
                                ${stylist.fullName ? stylist.fullName.charAt(0).toUpperCase() : "T"}
                            </div>
                            <div class="overflow-hidden">
                                <h6 class="mb-0 text-truncate fw-bold text-dark">${escapeHtml(stylist.fullName)}</h6>
                                <small class="text-muted d-block text-truncate" style="font-size: 0.75rem;">
                                    <i class="bi bi-clock me-1"></i>${shiftText}
                                </small>
                            </div>
                        </div>
                        <div class="card-body p-2 d-flex flex-column gap-2" style="max-height: 700px; overflow-y: auto;">
            `;

            if (!stylist.appointments || stylist.appointments.length === 0) {
                html += `
                    <div class="text-center py-4 text-muted small fst-italic">
                        Chưa có lịch hẹn
                    </div>
                `;
            } else {
                stylist.appointments.forEach(appt => {
                    const statusBadge = getStatusBadge(appt.status);
                    const canEdit = appt.canReschedule;

                    html += `
                        <div class="card border rounded-3 p-2 bg-white appointment-card shadow-xs position-relative" data-appointment-id="${appt.appointmentId}">
                            <div class="d-flex justify-content-between align-items-start mb-1">
                                <span class="badge bg-light text-dark border fw-semibold">
                                    <i class="bi bi-clock me-1 text-primary"></i>${appt.formattedTimeRange}
                                </span>
                                ${statusBadge}
                            </div>
                            <div class="fw-bold text-dark mb-1 small">${escapeHtml(appt.customerName)}</div>
                            <div class="text-secondary small mb-2 text-truncate" title="${escapeHtml(appt.serviceNamesSummary)}" style="font-size: 0.75rem;">
                                <i class="bi bi-scissors me-1"></i>${escapeHtml(appt.serviceNamesSummary || "Dịch vụ")}
                            </div>
                            <div class="d-flex justify-content-between align-items-center pt-1 border-top mt-1">
                                <span class="text-muted" style="font-size: 0.72rem;">
                                    <i class="bi bi-telephone me-1"></i>${escapeHtml(appt.customerPhone || "N/A")}
                                </span>
                                ${canEdit ? `
                                    <button type="button" class="btn btn-sm btn-outline-primary py-0 px-2 btn-open-reschedule" data-appointment-id="${appt.appointmentId}">
                                        <i class="bi bi-pencil-square me-1"></i>Sửa
                                    </button>
                                ` : `
                                    <span class="text-muted small fst-italic" style="font-size: 0.7rem;">Cố định</span>
                                `}
                            </div>
                        </div>
                    `;
                });
            }

            html += `
                        </div>
                    </div>
                </div>
            `;
        });

        html += `</div>`;
        gridContainer.innerHTML = html;

        // Gắn sự kiện mở Modal Sửa lịch
        gridContainer.querySelectorAll(".btn-open-reschedule").forEach(btn => {
            btn.addEventListener("click", (e) => {
                e.stopPropagation();
                const apptId = btn.dataset.appointmentId;
                openRescheduleModal(apptId);
            });
        });
    }

    function getStatusBadge(status) {
        switch ((status || "").toLowerCase()) {
            case "confirmed":
                return `<span class="badge bg-primary-subtle text-primary">Đã xác nhận</span>`;
            case "completed":
                return `<span class="badge bg-success-subtle text-success">Hoàn tất</span>`;
            case "cancelled":
                return `<span class="badge bg-danger-subtle text-danger">Đã hủy</span>`;
            case "inservice":
            case "checkedin":
                return `<span class="badge bg-info-subtle text-info">Đã đến</span>`;
            default:
                return `<span class="badge bg-warning-subtle text-warning">Chờ xác nhận</span>`;
        }
    }

    async function openRescheduleModal(appointmentId) {
        const modalElement = document.getElementById("rescheduleModal");
        if (!modalElement) return;

        if (!rescheduleModalInstance) {
            rescheduleModalInstance = new bootstrap.Modal(modalElement);
        }

        // Reset UI modal
        const alertBox = document.getElementById("reschedule-validation-alert");
        if (alertBox) alertBox.classList.add("d-none");
        const submitBtn = document.getElementById("btn-submit-reschedule");
        if (submitBtn) submitBtn.disabled = false;

        document.getElementById("reschedule-appointment-id").value = appointmentId;

        try {
            const res = await authFetch(`/api/reception/appointments/${appointmentId}/reschedule-info?targetDate=${currentDate}`);
            if (!res.ok) {
                alert("Không thể tải thông tin lịch hẹn!");
                return;
            }

            currentAppointmentDetail = await res.json();
            populateModalData(currentAppointmentDetail);
            await loadChangeHistory(appointmentId);
            rescheduleModalInstance.show();
        } catch (err) {
            console.error("Lỗi khi mở modal:", err);
            alert("Đã xảy ra lỗi khi tải thông tin lịch hẹn.");
        }
    }

    function populateModalData(detail) {
        document.getElementById("reschedule-modal-title").textContent = `Điều chỉnh lịch hẹn #${detail.appointmentId}`;
        document.getElementById("reschedule-customer-name").textContent = detail.customerName;
        document.getElementById("reschedule-customer-phone").textContent = detail.customerPhone || "N/A";
        document.getElementById("reschedule-current-stylist").textContent = detail.currentStylistName;
        document.getElementById("reschedule-current-time").textContent = 
            `${detail.currentStartTime.slice(0, 5)} - ${detail.currentEndTime.slice(0, 5)} (${detail.totalDurationMinutes} phút)`;

        // Danh sách dịch vụ
        const servicesList = document.getElementById("reschedule-services-list");
        if (servicesList) {
            servicesList.innerHTML = detail.services.map(s => `
                <span class="badge bg-light text-dark border me-1 mb-1">
                    ${escapeHtml(s.serviceName)} (${s.durationMinutes}p)
                </span>
            `).join("");
        }

        // Ngày mới
        const dateInput = document.getElementById("reschedule-date");
        const formattedDate = detail.currentDate.split("T")[0];
        dateInput.value = formattedDate;

        // Giờ bắt đầu
        const timeInput = document.getElementById("reschedule-start-time");
        timeInput.value = detail.currentStartTime.slice(0, 5);

        // Giờ kết thúc
        updateCalculatedEndTime(detail.currentStartTime.slice(0, 5), detail.totalDurationMinutes);

        // Populate thợ
        populateStylistOptions(detail.eligibleStylists, detail.currentStylistId);
    }

    function populateStylistOptions(stylists, selectedStylistId) {
        const select = document.getElementById("reschedule-stylist-select");
        if (!select) return;

        select.innerHTML = "";
        stylists.forEach(st => {
            const option = document.createElement("option");
            option.value = st.stylistId;

            let label = st.fullName;
            if (!st.hasAllSkills) {
                label += ` ⚠️ [Thiếu kỹ năng: ${st.missingServices.join(", ")}]`;
                option.classList.add("text-danger");
            } else if (!st.hasWorkingShift) {
                label += " (Không có ca làm)";
            }

            option.textContent = label;
            if (st.stylistId === selectedStylistId) {
                option.selected = true;
            }
            select.appendChild(option);
        });

        updateStylistSkillNote();
    }

    function updateCalculatedEndTime(startTimeStr, durationMinutes) {
        const endTimeInput = document.getElementById("reschedule-end-time-display");
        if (!endTimeInput || !startTimeStr) return;

        const [hours, minutes] = startTimeStr.split(":").map(Number);
        const totalStartMin = hours * 60 + minutes;
        const totalEndMin = totalStartMin + durationMinutes;

        const endHours = Math.floor(totalEndMin / 60) % 24;
        const endMinutes = totalEndMin % 60;
        const formatted = `${String(endHours).padStart(2, "0")}:${String(endMinutes).padStart(2, "0")}`;
        endTimeInput.value = `${formatted} (${durationMinutes} phút)`;
    }

    function updateStylistSkillNote() {
        const select = document.getElementById("reschedule-stylist-select");
        const noteEl = document.getElementById("reschedule-stylist-note");
        if (!select || !noteEl || !currentAppointmentDetail) return;

        const stylistId = parseInt(select.value, 10);
        const stylist = currentAppointmentDetail.eligibleStylists.find(s => s.stylistId === stylistId);

        if (!stylist) {
            noteEl.innerHTML = "";
            return;
        }

        if (!stylist.hasAllSkills) {
            noteEl.innerHTML = `
                <div class="alert alert-danger py-1 px-2 small mb-0 mt-1">
                    <i class="bi bi-exclamation-triangle-fill me-1"></i>
                    <strong>[Mã F33]</strong> Thợ không làm được: <em>${escapeHtml(stylist.missingServices.join(", "))}</em>.
                </div>
            `;
        } else if (!stylist.hasWorkingShift) {
            noteEl.innerHTML = `
                <div class="alert alert-warning py-1 px-2 small mb-0 mt-1">
                    <i class="bi bi-clock-history me-1"></i> Thợ chưa có lịch làm việc trong ngày này.
                </div>
            `;
        } else {
            const shiftDesc = stylist.shifts.map(s => `${s.startTime.slice(0, 5)} - ${s.endTime.slice(0, 5)}`).join(", ");
            noteEl.innerHTML = `
                <small class="text-success d-block mt-1">
                    <i class="bi bi-check-circle-fill me-1"></i> Đủ kỹ năng · Ca làm: ${shiftDesc}
                </small>
            `;
        }
    }

    function bindRescheduleModalEvents() {
        const stylistSelect = document.getElementById("reschedule-stylist-select");
        const startTimeInput = document.getElementById("reschedule-start-time");
        const dateInput = document.getElementById("reschedule-date");
        const submitBtn = document.getElementById("btn-submit-reschedule");
        const form = document.getElementById("reschedule-form");

        if (stylistSelect) {
            stylistSelect.addEventListener("change", () => {
                updateStylistSkillNote();
                clearValidationAlert();
            });
        }

        if (startTimeInput) {
            startTimeInput.addEventListener("input", (e) => {
                if (currentAppointmentDetail) {
                    updateCalculatedEndTime(e.target.value, currentAppointmentDetail.totalDurationMinutes);
                }
                clearValidationAlert();
            });
        }

        if (dateInput) {
            dateInput.addEventListener("change", async (e) => {
                // Tải lại danh sách thợ theo ngày mới
                if (currentAppointmentDetail) {
                    try {
                        const res = await authFetch(`/api/reception/appointments/${currentAppointmentDetail.appointmentId}/reschedule-info?targetDate=${e.target.value}`);
                        if (res.ok) {
                            currentAppointmentDetail = await res.json();
                            populateStylistOptions(currentAppointmentDetail.eligibleStylists, parseInt(stylistSelect.value, 10));
                        }
                    } catch (err) {
                        console.error(err);
                    }
                }
                clearValidationAlert();
            });
        }

        const toggleHistoryBtn = document.getElementById("btn-toggle-history");
        const historyContainer = document.getElementById("reschedule-history-container");
        if (toggleHistoryBtn && historyContainer) {
            toggleHistoryBtn.addEventListener("click", () => {
                const isHidden = historyContainer.style.display === "none";
                historyContainer.style.display = isHidden ? "block" : "none";
                const count = document.getElementById("history-count")?.textContent || "0";
                toggleHistoryBtn.innerHTML = isHidden 
                    ? `<i class="bi bi-chevron-up me-1"></i>Thu gọn` 
                    : `<i class="bi bi-chevron-down me-1"></i>Xem lịch sử (<span id="history-count">${count}</span>)`;
            });
        }

        if (form) {
            form.addEventListener("submit", async (e) => {
                e.preventDefault();
                await handleRescheduleSubmit();
            });
        }
    }

    async function handleRescheduleSubmit() {
        if (!currentAppointmentDetail) return;

        const appointmentId = currentAppointmentDetail.appointmentId;
        const newStylistId = parseInt(document.getElementById("reschedule-stylist-select").value, 10);
        const newDate = document.getElementById("reschedule-date").value;
        const newStartTimeStr = document.getElementById("reschedule-start-time").value;
        const reason = document.getElementById("reschedule-reason").value;

        if (!newStylistId || !newDate || !newStartTimeStr) {
            showValidationAlert("VALIDATION", "Vui lòng nhập đầy đủ thợ, ngày và giờ bắt đầu.");
            return;
        }

        const submitBtn = document.getElementById("btn-submit-reschedule");
        const originalBtnText = submitBtn.innerHTML;
        submitBtn.disabled = true;
        submitBtn.innerHTML = `<span class="spinner-border spinner-border-sm me-1" role="status"></span>Đang lưu...`;

        const payload = {
            newStylistId: newStylistId,
            newDate: newDate,
            newStartTime: `${newStartTimeStr}:00`,
            reason: reason
        };

        try {
            const res = await authFetch(`/api/reception/appointments/${appointmentId}/reschedule`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });

            const result = await res.json();

            if (!res.ok || !result.success) {
                const validation = result.validation;
                const code = validation?.errorCode || "ERROR";
                const message = validation?.errorMessage || result.message || "Không thể thực hiện dời lịch.";
                showValidationAlert(code, message);
            } else {
                // Thành công
                if (rescheduleModalInstance) {
                    rescheduleModalInstance.hide();
                }
                showToastSuccess("Đổi thợ và dời giờ hẹn thành công!");
                loadDailySchedule(currentDate);
            }
        } catch (err) {
            console.error("Lỗi gửi dời lịch:", err);
            showValidationAlert("NETWORK_ERROR", "Đã xảy ra lỗi kết nối với máy chủ.");
        } finally {
            submitBtn.disabled = false;
            submitBtn.innerHTML = originalBtnText;
        }
    }

    function showValidationAlert(code, message) {
        const alertBox = document.getElementById("reschedule-validation-alert");
        const alertMsg = document.getElementById("reschedule-validation-message");
        if (!alertBox || !alertMsg) return;

        let icon = "bi-exclamation-triangle-fill";
        let prefix = "Lỗi:";

        if (code === "F33") {
            prefix = "[Mã F33 - Thiếu kỹ năng]:";
            icon = "bi-shield-x";
        } else if (code === "OVERLAP") {
            prefix = "[Trùng lấn lịch]:";
            icon = "bi-calendar-x";
        } else if (code === "OUT_OF_SHIFT") {
            prefix = "[Ngoài ca làm]:";
            icon = "bi-clock-history";
        }

        alertMsg.innerHTML = `<i class="bi ${icon} me-1"></i> <strong>${prefix}</strong> ${escapeHtml(message)}`;
        alertBox.classList.remove("d-none");
    }

    function clearValidationAlert() {
        const alertBox = document.getElementById("reschedule-validation-alert");
        if (alertBox) alertBox.classList.add("d-none");
    }

    function showToastSuccess(message) {
        // Simple alert or toast
        const toastEl = document.getElementById("reception-toast");
        if (toastEl) {
            const msgEl = document.getElementById("reception-toast-message");
            if (msgEl) msgEl.textContent = message;
            const toast = new bootstrap.Toast(toastEl, { delay: 4000 });
            toast.show();
        } else {
            alert(message);
        }
    }

    async function loadChangeHistory(appointmentId) {
        const listEl = document.getElementById("reschedule-history-list");
        const emptyEl = document.getElementById("reschedule-history-empty");
        const countEl = document.getElementById("history-count");
        if (!listEl) return;

        try {
            const res = await authFetch(`/api/reception/appointments/${appointmentId}/change-history`);
            if (!res.ok) return;

            const history = await res.json();
            if (countEl) countEl.textContent = history.length;

            if (history.length === 0) {
                listEl.innerHTML = "";
                if (emptyEl) emptyEl.style.display = "block";
                return;
            }

            if (emptyEl) emptyEl.style.display = "none";
            listEl.innerHTML = history.map(item => `
                <div class="border-bottom pb-2 mb-2">
                    <div class="d-flex justify-content-between align-items-center">
                        <span class="badge bg-secondary-subtle text-dark border" style="font-size: 0.7rem;">
                            <i class="bi bi-calendar-event me-1"></i>${item.formattedChangedAt}
                        </span>
                        <span class="text-muted" style="font-size: 0.72rem;">
                            <i class="bi bi-person me-1"></i>${escapeHtml(item.modifiedByUserName || "Lễ tân")}
                        </span>
                    </div>
                    <div class="mt-1 fw-semibold text-dark" style="font-size: 0.78rem;">
                        ${escapeHtml(item.formattedChangeSummary)}
                    </div>
                    ${item.reason ? `
                        <div class="text-muted fst-italic mt-1" style="font-size: 0.73rem;">
                            <i class="bi bi-chat-left-quote me-1"></i>Lý do: "${escapeHtml(item.reason)}"
                        </div>
                    ` : ""}
                </div>
            `).join("");
        } catch (err) {
            console.error("Lỗi tải lịch sử thay đổi:", err);
        }
    }

    return {
        init,
        loadDailySchedule,
        openRescheduleModal
    };
})();
