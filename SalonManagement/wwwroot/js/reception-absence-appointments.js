window.ReceptionAbsenceAppointments = (() => {
    const escapeHtml = value => String(value ?? "").replace(/[&<>'"]/g, character => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;" }[character]));
    const time = value => String(value ?? "").slice(0, 5);
    const statusLabel = status => ({ Pending: "Chờ xác nhận", PendingConfirmation: "Chờ xác nhận", Confirmed: "Đã xác nhận", Arrived: "Khách đã đến", InProgress: "Đang thực hiện" }[status] || status);

    function rowPlan(card) {
        const appointmentId = Number(card.dataset.appointmentId);
        const stylistId = Number(card.querySelector("[data-proposed-stylist]").value);
        const startTime = card.querySelector("[data-proposed-time]").value;
        return appointmentId && stylistId && startTime ? { appointmentId, stylistId, startTime: `${startTime}:00` } : null;
    }

    function renderCallList(appointments) {
        const section = document.getElementById("call-customer-results");
        const count = document.getElementById("call-customer-count");
        const list = document.getElementById("call-customer-list");
        const needsCall = appointments.filter(appointment => !rowPlan(document.querySelector(`[data-appointment-id="${appointment.appointmentId}"]`)));
        section.classList.toggle("d-none", needsCall.length === 0);
        count.textContent = `${needsCall.length} lịch`;
        list.innerHTML = needsCall.map(appointment => `<article><strong>${escapeHtml(appointment.customerName)}</strong><span>${escapeHtml(appointment.customerPhone)} · ${time(appointment.startTime)}–${time(appointment.endTime)}</span><small>${escapeHtml(appointment.suggestion.message)}</small></article>`).join("");
    }

    function updateBatchButton() {
        const selected = document.querySelectorAll("[data-batch-reschedule]:checked").length;
        const button = document.getElementById("batch-reschedule-confirm");
        button.disabled = selected === 0;
        button.innerHTML = selected ? `<i class="bi bi-arrow-left-right"></i> Chuyển ${selected} lịch đã chọn` : '<i class="bi bi-arrow-left-right"></i> Xác nhận chuyển đã chọn';
    }

    function updateMetrics(appointments, stylistOptions) {
        const planned = appointments.filter(item => item.suggestion.stylistId).length;
        const alternatives = new Set(appointments.map(item => item.suggestion.stylistId).filter(Boolean)).size;
        document.getElementById("absence-metric-total").textContent = `${appointments.length} lịch hẹn`;
        document.getElementById("absence-metric-ready").textContent = `${planned} lịch sẵn sàng`;
        document.getElementById("absence-metric-stylists").textContent = `${alternatives || Math.max(stylistOptions.length - 1, 0)} thợ sẵn sàng`;
        document.getElementById("absence-metric-notify").textContent = `${planned} / ${appointments.length} khách`;
    }

    function showBatchResult(result) {
        const section = document.getElementById("batch-reschedule-results");
        const success = document.getElementById("batch-reschedule-success");
        const failed = document.getElementById("batch-reschedule-failed");
        section.classList.remove("d-none");
        success.className = result.succeeded.length ? "absence-result-success" : "d-none";
        success.innerHTML = result.succeeded.length ? `<i class="bi bi-check-circle-fill"></i><span>Đã điều phối thành công ${result.succeeded.length} lịch. Hệ thống đang gửi thông báo thay đổi lịch cho khách.</span>` : "";
        failed.className = result.failed.length ? "absence-result-failed" : "d-none";
        failed.innerHTML = result.failed.length ? `<i class="bi bi-exclamation-octagon-fill"></i><span>${result.failed.map(item => `Lịch #${item.appointmentId}: ${escapeHtml(item.error)}`).join(" · ")}</span>` : "";
    }

    async function submitPlans(items, sourceButton) {
        if (!items.length) return;
        const error = document.getElementById("absence-appointments-error");
        const original = sourceButton?.innerHTML;
        if (sourceButton) { sourceButton.disabled = true; sourceButton.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Đang chuyển…'; }
        try {
            const response = await SalonAuth.authenticatedFetch("/api/reception/appointments/batch-reschedule", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ items }) });
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.error || "Không thể xác nhận chuyển lịch.");
            showBatchResult(result);
            await loadAppointments();
        } catch (problem) {
            error.textContent = problem.message || "Không thể xác nhận chuyển lịch.";
            error.classList.remove("d-none");
        } finally {
            if (sourceButton && document.body.contains(sourceButton)) { sourceButton.disabled = false; sourceButton.innerHTML = original; }
        }
    }

    async function confirmBatch() {
        const items = [...document.querySelectorAll("[data-batch-reschedule]:checked")].map(check => rowPlan(check.closest("[data-appointment-id]"))).filter(Boolean);
        await submitPlans(items, document.getElementById("batch-reschedule-confirm"));
    }

    async function loadStylists() {
        const select = document.getElementById("absent-stylist");
        const response = await SalonAuth.authenticatedFetch("/api/reception/absent-stylist-appointments/stylists");
        if (!response.ok) throw new Error("Không thể tải danh sách thợ.");
        const stylists = await response.json();
        select.innerHTML = `<option value="">-- Chọn thợ nghỉ --</option>${stylists.map(stylist => `<option value="${stylist.stylistId}">${escapeHtml(stylist.fullName)}</option>`).join("")}`;
    }

    function appointmentCard(appointment, absentStylistId, stylistOptions, index) {
        const suggestion = appointment.suggestion;
        const proposedStart = time(suggestion.startTime || appointment.startTime);
        const proposedStylist = suggestion.stylistId || "";
        const choices = stylistOptions.filter(item => item.id !== absentStylistId).map(item => `<option value="${item.id}" ${item.id === proposedStylist ? "selected" : ""}>${escapeHtml(item.name)}</option>`).join("");
        const ready = Boolean(proposedStylist && proposedStart);
        const urgent = index === 0 || appointment.status === "Arrived" || appointment.status === "InProgress";
        const services = appointment.services.map(escapeHtml).join(" + ");
        return `<article class="absence-appointment-card ${ready ? "has-plan" : "needs-contact"}" data-appointment-id="${appointment.appointmentId}">
            <div class="absence-appointment-top">
                <time class="absence-time ${urgent ? "urgent" : ""}">${time(appointment.startTime)}</time>
                <div class="absence-customer"><div><h3>${escapeHtml(appointment.customerName)}</h3><span class="absence-status-badge ${urgent ? "urgent" : ""}">${urgent ? "Cần xử lý ưu tiên" : statusLabel(appointment.status)}</span></div><p><i class="bi bi-telephone"></i> ${escapeHtml(appointment.customerPhone)} <b>·</b> <i class="bi bi-scissors"></i> ${services}</p></div>
                <label class="absence-select-card"><input type="checkbox" data-batch-reschedule="${appointment.appointmentId}" ${ready ? "checked" : "disabled"} /> <span>Chọn</span></label>
            </div>
            <div class="absence-original-stylist"><span>THỢ ĐĂNG KÝ BAN ĐẦU</span><strong>${escapeHtml(document.getElementById("absent-stylist").selectedOptions[0].textContent)} <em>(Báo nghỉ)</em></strong></div>
            <div class="absence-divider"></div>
            <div class="absence-proposal">
                <span class="absence-suggestion-label"><i class="bi bi-hand-thumbs-up-fill"></i> ${ready ? "Gợi ý thay thế" : "Cần liên hệ khách"}</span>
                <div class="absence-proposal-controls">
                    <div class="absence-suggested-stylist"><strong>${ready ? escapeHtml(suggestion.stylistName) : "Chưa có thợ phù hợp"}</strong><span>${ready ? `${suggestion.keepsOriginalTime ? "Giữ giờ cũ" : "Giờ gần nhất"} · ${time(suggestion.startTime)}–${time(suggestion.endTime)}` : escapeHtml(suggestion.message)}</span></div>
                    <select class="form-select form-select-sm" aria-label="Thợ thay thế cho ${escapeHtml(appointment.customerName)}" data-proposed-stylist="${appointment.appointmentId}"><option value="">Chọn thợ khác</option>${choices}</select>
                    <input class="form-control form-control-sm" type="time" value="${proposedStart}" aria-label="Giờ mới cho ${escapeHtml(appointment.customerName)}" data-proposed-time="${appointment.appointmentId}" />
                    <button class="btn absence-transfer-button" type="button" data-quick-transfer ${ready ? "" : "disabled"}><i class="bi bi-person-gear"></i> Chuyển thợ ngay</button>
                    <button class="btn absence-time-button" type="button" data-focus-reschedule><i class="bi bi-calendar-event"></i> Đổi giờ</button>
                    <button class="btn absence-message-button" type="button" data-message-customer><i class="bi bi-send-fill"></i> Gửi Zalo</button>
                </div>
            </div>
        </article>`;
    }

    function bindCards(appointments) {
        document.querySelectorAll("[data-proposed-stylist], [data-proposed-time]").forEach(input => input.addEventListener("change", event => {
            const card = event.target.closest("[data-appointment-id]");
            const checkbox = card.querySelector("[data-batch-reschedule]");
            const transfer = card.querySelector("[data-quick-transfer]");
            const plan = rowPlan(card);
            checkbox.disabled = !plan;
            checkbox.checked = Boolean(plan);
            transfer.disabled = !plan;
            card.classList.toggle("has-plan", Boolean(plan));
            card.classList.toggle("needs-contact", !plan);
            renderCallList(appointments);
            updateBatchButton();
        }));
        document.querySelectorAll("[data-batch-reschedule]").forEach(input => input.addEventListener("change", updateBatchButton));
        document.querySelectorAll("[data-quick-transfer]").forEach(button => button.addEventListener("click", () => submitPlans([rowPlan(button.closest("[data-appointment-id]"))].filter(Boolean), button)));
        document.querySelectorAll("[data-focus-reschedule]").forEach(button => button.addEventListener("click", () => { const field = button.closest("[data-appointment-id]").querySelector("[data-proposed-time]"); field.focus(); field.showPicker?.(); }));
        document.querySelectorAll("[data-message-customer]").forEach(button => button.addEventListener("click", async () => {
            const card = button.closest("[data-appointment-id]"); const appointment = appointments.find(item => item.appointmentId === Number(card.dataset.appointmentId)); const plan = rowPlan(card);
            const message = `Luminol Salon xin thông báo: lịch hẹn của ${appointment.customerName} được điều chỉnh${plan ? ` sang ${time(plan.startTime)} với thợ mới` : " do thợ báo nghỉ"}. Vui lòng liên hệ lễ tân để được hỗ trợ.`;
            try { await navigator.clipboard.writeText(message); button.innerHTML = '<i class="bi bi-check2"></i> Đã soạn tin nhắn'; setTimeout(() => button.innerHTML = '<i class="bi bi-send-fill"></i> Gửi Zalo', 1600); } catch { window.prompt("Nội dung thông báo khách hàng", message); }
        }));
    }

    async function loadAppointments() {
        const stylist = document.getElementById("absent-stylist");
        const date = document.getElementById("absence-date").value;
        const error = document.getElementById("absence-appointments-error");
        if (!stylist.value || !date) return;
        try {
            const response = await SalonAuth.authenticatedFetch(`/api/reception/absent-stylist-appointments?stylistId=${stylist.value}&date=${encodeURIComponent(date)}`);
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.error || "Không thể tải lịch cần xử lý.");
            const stylistOptions = [...stylist.options].filter(option => option.value).map(option => ({ id: Number(option.value), name: option.textContent }));
            const appointments = await Promise.all(result.appointments.map(async appointment => {
                const suggestionResponse = await SalonAuth.authenticatedFetch(`/api/reception/replacement-suggestions?appointmentId=${appointment.appointmentId}&absentStylistId=${stylist.value}`);
                const suggestion = await suggestionResponse.json().catch(() => ({ message: "Không thể tạo đề xuất." }));
                return { ...appointment, suggestion };
            }));
            document.getElementById("absence-appointments-stylist").textContent = result.stylist.fullName;
            document.getElementById("absence-appointments-list").innerHTML = appointments.map((item, index) => appointmentCard(item, Number(stylist.value), stylistOptions, index)).join("");
            document.getElementById("absence-appointments-results").classList.remove("d-none");
            document.getElementById("absence-appointments-metrics").classList.remove("d-none");
            document.getElementById("absence-appointments-empty").classList.toggle("d-none", appointments.length !== 0);
            updateMetrics(appointments, stylistOptions);
            const selectAll = document.getElementById("batch-reschedule-select-all");
            selectAll.checked = appointments.length > 0 && appointments.every(item => item.suggestion.stylistId);
            selectAll.disabled = !appointments.some(item => item.suggestion.stylistId);
            bindCards(appointments);
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
            catch (problem) { const error = document.getElementById("absence-appointments-error"); error.textContent = problem.message || "Không thể tải danh sách thợ."; error.classList.remove("d-none"); }
            form.addEventListener("submit", event => { event.preventDefault(); loadAppointments(); });
            document.getElementById("batch-reschedule-confirm").addEventListener("click", confirmBatch);
            document.getElementById("batch-reschedule-select-all").addEventListener("change", event => { document.querySelectorAll("[data-batch-reschedule]:not(:disabled)").forEach(check => check.checked = event.target.checked); updateBatchButton(); });
        }
    };
})();
