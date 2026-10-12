window.ReceptionAbsenceAppointments = (() => {
    const escapeHtml = value => String(value ?? "").replace(/[&<>'"]/g, character => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;"
    }[character]));
    const time = value => String(value ?? "").slice(0, 5);
    const statusLabel = status => ({
        Pending: "Chờ xác nhận", PendingConfirmation: "Chờ xác nhận", Confirmed: "Đã xác nhận",
        Arrived: "Khách đã đến", InProgress: "Đang thực hiện"
    }[status] || status);

    function renderCallList(appointments) {
        const section = document.getElementById("call-customer-results");
        const count = document.getElementById("call-customer-count");
        const list = document.getElementById("call-customer-list");
        const needsCall = appointments.filter(appointment => {
            const manualStylist = document.querySelector(`[data-proposed-stylist="${appointment.appointmentId}"]`)?.value;
            const manualTime = document.querySelector(`[data-proposed-time="${appointment.appointmentId}"]`)?.value;
            return !appointment.suggestion.stylistId && !(manualStylist && manualTime);
        });
        section.classList.toggle("d-none", needsCall.length === 0);
        count.textContent = `${needsCall.length} lịch`;
        list.innerHTML = needsCall.map(appointment => `<tr>
            <td><strong>${escapeHtml(appointment.customerName)}</strong><br><small>Mã: ${escapeHtml(appointment.bookingReference)}</small></td>
            <td><a href="tel:${escapeHtml(appointment.customerPhone)}">${escapeHtml(appointment.customerPhone)}</a></td>
            <td>${appointment.services.map(escapeHtml).join("<br>")}</td>
            <td>${time(appointment.startTime)}–${time(appointment.endTime)}</td>
            <td class="text-danger">${escapeHtml(appointment.suggestion.message)}</td>
        </tr>`).join("");
    }

    function updateBatchButton() {
        const selected = document.querySelectorAll("[data-batch-reschedule]:checked").length;
        const button = document.getElementById("batch-reschedule-confirm");
        button.disabled = selected === 0;
        button.textContent = selected ? `Xác nhận chuyển ${selected} lịch` : "Xác nhận chuyển đã chọn";
    }

    function showBatchResult(result) {
        const section = document.getElementById("batch-reschedule-results");
        const success = document.getElementById("batch-reschedule-success");
        const failed = document.getElementById("batch-reschedule-failed");
        section.classList.remove("d-none");
        success.className = `alert alert-success ${result.succeeded.length ? "" : "d-none"}`;
        const notificationLabel = status => ({
            Queued: "Đang chờ gửi email", Sending: "Đang gửi email", Sent: "Đã gửi email",
            Retrying: "Sẽ thử gửi lại", Failed: "Gửi email thất bại", NotRequired: "Không có email để gửi"
        }[status] || status || "Không có email để gửi");
        success.innerHTML = result.succeeded.length
            ? `<strong>Đã chuyển ${result.succeeded.length} lịch:</strong><ul class="mb-0 mt-2">${result.succeeded.map(item => `<li>Lịch #${item.appointmentId}: thợ #${item.stylistId}, ${time(item.startTime)}–${time(item.endTime)} <span class="text-muted">· ${notificationLabel(item.notificationStatus)}</span></li>`).join("")}</ul>` : "";
        failed.className = `alert alert-danger ${result.failed.length ? "" : "d-none"}`;
        failed.innerHTML = result.failed.length
            ? `<strong>Không thể chuyển ${result.failed.length} lịch:</strong><ul class="mb-0 mt-2">${result.failed.map(item => `<li>Lịch #${item.appointmentId}: ${escapeHtml(item.error)}</li>`).join("")}</ul>` : "";
    }

    async function confirmBatch() {
        const error = document.getElementById("absence-appointments-error");
        const selected = [...document.querySelectorAll("[data-batch-reschedule]:checked")];
        const items = selected.map(check => {
            const row = check.closest("tr");
            return {
                appointmentId: Number(check.dataset.batchReschedule),
                stylistId: Number(row.querySelector("[data-proposed-stylist]").value),
                startTime: `${row.querySelector("[data-proposed-time]").value}:00`
            };
        }).filter(item => item.appointmentId && item.stylistId && item.startTime !== ":00");
        if (!items.length) return;
        const button = document.getElementById("batch-reschedule-confirm");
        button.disabled = true;
        try {
            const response = await SalonAuth.authenticatedFetch("/api/reception/appointments/batch-reschedule", {
                method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ items })
            });
            const result = await response.json();
            if (!response.ok) throw new Error(result.error || "Không thể xác nhận chuyển lịch.");
            showBatchResult(result);
            await loadAppointments();
        } catch (problem) {
            error.textContent = problem.message || "Không thể xác nhận chuyển lịch.";
            error.classList.remove("d-none");
            updateBatchButton();
        }
    }

    async function loadStylists() {
        const select = document.getElementById("absent-stylist");
        const response = await SalonAuth.authenticatedFetch("/api/reception/absent-stylist-appointments/stylists");
        if (!response.ok) throw new Error("Không thể tải danh sách thợ.");
        const stylists = await response.json();
        select.innerHTML = `<option value="">-- Chọn thợ --</option>${stylists.map(stylist => `<option value="${stylist.stylistId}">${escapeHtml(stylist.fullName)}</option>`).join("")}`;
    }

    async function loadAppointments() {
        const stylist = document.getElementById("absent-stylist");
        const date = document.getElementById("absence-date").value;
        const error = document.getElementById("absence-appointments-error");
        if (!stylist.value || !date) return;
        try {
            const response = await SalonAuth.authenticatedFetch(`/api/reception/absent-stylist-appointments?stylistId=${stylist.value}&date=${encodeURIComponent(date)}`);
            const result = await response.json();
            if (!response.ok) throw new Error(result.error || "Không thể tải lịch cần xử lý.");
            const stylistOptions = [...stylist.options]
                .filter(option => option.value)
                .map(option => ({ id: Number(option.value), name: option.textContent }));
            const appointments = await Promise.all(result.appointments.map(async appointment => {
                const suggestionResponse = await SalonAuth.authenticatedFetch(`/api/reception/replacement-suggestions?appointmentId=${appointment.appointmentId}&absentStylistId=${stylist.value}`);
                const suggestion = await suggestionResponse.json().catch(() => ({ message: "Không thể tạo đề xuất." }));
                return { ...appointment, suggestion };
            }));
            const section = document.getElementById("absence-appointments-results");
            const table = document.getElementById("absence-appointments-table-wrap");
            const empty = document.getElementById("absence-appointments-empty");
            document.getElementById("absence-appointments-stylist").textContent = result.stylist.fullName;
            document.getElementById("absence-appointments-count").textContent = `${appointments.length} lịch cần chuyển`;
            document.getElementById("absence-appointments-list").innerHTML = appointments.map(appointment => {
                const suggestion = appointment.suggestion;
                const proposedStart = time(suggestion.startTime || appointment.startTime);
                const proposedStylist = suggestion.stylistId || "";
                const choices = stylistOptions.filter(item => item.id !== Number(stylist.value))
                    .map(item => `<option value="${item.id}" ${item.id === proposedStylist ? "selected" : ""}>${escapeHtml(item.name)}</option>`).join("");
                const proposal = suggestion.stylistId
                    ? `<strong>${escapeHtml(suggestion.stylistName)}</strong> · ${time(suggestion.startTime)}–${time(suggestion.endTime)}<br><small class="text-muted">${escapeHtml(suggestion.message)}</small>`
                    : `<span class="text-danger">${escapeHtml(suggestion.message)}</span>`;
                const hasPlan = Boolean(proposedStylist && proposedStart);
                return `<tr>
                <td><input type="checkbox" data-batch-reschedule="${appointment.appointmentId}" ${hasPlan ? "checked" : "disabled"} aria-label="Chọn lịch của ${escapeHtml(appointment.customerName)}"></td>
                <td><strong>${escapeHtml(appointment.customerName)}</strong><br><small>Mã: ${escapeHtml(appointment.bookingReference)}</small></td>
                <td>${appointment.services.map(escapeHtml).join("<br>")}</td>
                <td>${time(appointment.startTime)}–${time(appointment.endTime)}</td>
                <td><span class="badge text-bg-warning">${escapeHtml(statusLabel(appointment.status))}</span></td>
                <td>${proposal}<div class="row g-1 mt-2"><div class="col-7"><select class="form-select form-select-sm" aria-label="Thợ thay thế cho ${escapeHtml(appointment.customerName)}" data-proposed-stylist="${appointment.appointmentId}"><option value="">Chọn thợ khác</option>${choices}</select></div><div class="col-5"><input class="form-control form-control-sm" type="time" value="${proposedStart}" aria-label="Giờ mới cho ${escapeHtml(appointment.customerName)}" data-proposed-time="${appointment.appointmentId}"></div></div><small class="text-muted">Có thể chỉnh thợ và giờ trước khi xác nhận.</small></td>
            </tr>`;
            }).join("");
            section.classList.remove("d-none");
            table.classList.toggle("d-none", appointments.length === 0);
            empty.classList.toggle("d-none", appointments.length !== 0);
            document.querySelectorAll("[data-proposed-stylist], [data-proposed-time]")
                .forEach(input => input.addEventListener("change", event => {
                    const row = event.target.closest("tr");
                    const checkbox = row.querySelector("[data-batch-reschedule]");
                    checkbox.disabled = !(row.querySelector("[data-proposed-stylist]").value && row.querySelector("[data-proposed-time]").value);
                    renderCallList(appointments);
                    updateBatchButton();
                }));
            document.querySelectorAll("[data-batch-reschedule]").forEach(input => input.addEventListener("change", updateBatchButton));
            const selectAll = document.getElementById("batch-reschedule-select-all");
            selectAll.checked = appointments.some(appointment => appointment.suggestion.stylistId);
            selectAll.disabled = !appointments.some(appointment => appointment.suggestion.stylistId);
            renderCallList(appointments);
            updateBatchButton();
            error.classList.add("d-none");
        } catch (problem) {
            error.textContent = problem.message || "Không thể tải lịch cần xử lý.";
            error.classList.remove("d-none");
        }
    }

    return {
        async initialize() {
            const form = document.getElementById("absence-appointments-form");
            try { await loadStylists(); }
            catch (problem) {
                const error = document.getElementById("absence-appointments-error");
                error.textContent = problem.message || "Không thể tải danh sách thợ.";
                error.classList.remove("d-none");
            }
            form.addEventListener("submit", event => { event.preventDefault(); loadAppointments(); });
            document.getElementById("batch-reschedule-confirm").addEventListener("click", confirmBatch);
            document.getElementById("batch-reschedule-select-all").addEventListener("change", event => {
                document.querySelectorAll("[data-batch-reschedule]:not(:disabled)").forEach(check => check.checked = event.target.checked);
                updateBatchButton();
            });
        }
    };
})();
