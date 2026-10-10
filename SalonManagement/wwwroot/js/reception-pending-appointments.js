window.ReceptionPendingAppointments = (() => {
    const refreshMilliseconds = 15000;
    let refreshTimer;

    const escapeHtml = value => String(value ?? "").replace(/[&<>'"]/g, character => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;"
    }[character]));
    const time = value => String(value ?? "").slice(0, 5);
    const date = value => new Intl.DateTimeFormat("vi-VN").format(new Date(`${value}T00:00:00`));

    async function load() {
        const loading = document.getElementById("pending-appointments-loading");
        const error = document.getElementById("pending-appointments-error");
        const wrap = document.getElementById("pending-appointments-table-wrap");
        const list = document.getElementById("pending-appointments-list");
        const count = document.getElementById("pending-appointments-count");
        if (!loading || !error || !wrap || !list || !count) return;

        try {
            const response = await SalonAuth.authenticatedFetch("/api/reception/pending-appointments");
            if (!response.ok) throw new Error();
            const appointments = await response.json();
            count.textContent = `${appointments.length} lịch`;
            loading.classList.toggle("d-none", appointments.length > 0);
            loading.textContent = appointments.length ? "" : "Chưa có lịch nào cần xác nhận.";
            error.classList.add("d-none");
            wrap.classList.toggle("d-none", appointments.length === 0);
            list.innerHTML = appointments.map(appointment => `
                <tr class="table-warning">
                    <td><strong>${escapeHtml(appointment.customerName)}</strong><br><small class="text-muted">${escapeHtml(appointment.customerPhone)}${appointment.customerEmail ? ` · ${escapeHtml(appointment.customerEmail)}` : ""}<br>Mã: ${escapeHtml(appointment.reference)}</small></td>
                    <td>${appointment.services.map(escapeHtml).join("<br>")}</td>
                    <td>${escapeHtml(appointment.stylistName)}</td>
                    <td><strong>${date(appointment.appointmentDate)}</strong><br><small>${time(appointment.startTime)}–${time(appointment.endTime)}</small></td>
                    <td><span class="badge text-bg-warning">Chờ xác nhận</span></td>
                    <td><button class="btn btn-sm btn-success" type="button" data-confirm-appointment="${appointment.appointmentId}">Xác nhận</button></td>
                </tr>`).join("");
            list.querySelectorAll("[data-confirm-appointment]").forEach(button => button.addEventListener("click", async () => {
                button.disabled = true;
                try {
                    const response = await SalonAuth.authenticatedFetch(`/api/reception/pending-appointments/${button.dataset.confirmAppointment}/confirm`, { method: "POST" });
                    if (!response.ok) {
                        const body = await response.json().catch(() => ({}));
                        throw new Error(body.error || "Không thể xác nhận lịch hẹn.");
                    }
                    await load();
                } catch (problem) {
                    error.textContent = problem.message || "Không thể xác nhận lịch hẹn. Vui lòng thử lại.";
                    error.classList.remove("d-none");
                    button.disabled = false;
                }
            }));
        } catch {
            error.textContent = "Không thể tải lịch chờ xác nhận. Vui lòng thử lại.";
            error.classList.remove("d-none");
            loading.classList.add("d-none");
        }
    }

    return {
        start() {
            clearInterval(refreshTimer);
            load();
            refreshTimer = setInterval(load, refreshMilliseconds);
        }
    };
})();
