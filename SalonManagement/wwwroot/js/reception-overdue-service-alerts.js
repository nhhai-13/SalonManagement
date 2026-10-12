window.ReceptionOverdueServiceAlerts = (() => {
    const refreshMilliseconds = 15000;
    let refreshTimer;

    const escapeHtml = value => String(value ?? "").replace(/[&<>'"]/g, character => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;"
    }[character]));
    const date = value => new Intl.DateTimeFormat("vi-VN").format(new Date(`${value}T00:00:00`));
    const time = value => String(value ?? "").slice(0, 5);

    async function load() {
        const section = document.getElementById("overdue-service-alerts");
        const loading = document.getElementById("overdue-service-alerts-loading");
        const error = document.getElementById("overdue-service-alerts-error");
        const count = document.getElementById("overdue-service-alerts-count");
        const table = document.getElementById("overdue-service-alerts-table");
        const list = document.getElementById("overdue-service-alerts-list");
        if (!section || !loading || !error || !count || !table || !list) return;

        try {
            const response = await SalonAuth.authenticatedFetch("/api/reception/overdue-service-alerts");
            if (!response.ok) throw new Error();
            const alerts = await response.json();
            count.textContent = `${alerts.length} cảnh báo`;
            section.classList.toggle("d-none", alerts.length === 0);
            loading.classList.add("d-none");
            error.classList.add("d-none");
            table.classList.toggle("d-none", alerts.length === 0);
            list.innerHTML = alerts.map(alert => `<tr>
                <td><strong>${escapeHtml(alert.customerName)}</strong><br><small>Mã: ${escapeHtml(alert.bookingReference)}</small></td>
                <td>${escapeHtml(alert.stylistName)}</td>
                <td>${escapeHtml(alert.services.join(", "))}</td>
                <td>${date(alert.appointmentDate)}<br><small>${time(alert.startTime)}–${time(alert.endTime)}</small></td>
                <td>${alert.standardDurationMinutes} phút</td>
                <td><strong>${alert.actualDurationMinutes} phút</strong><br><span class="badge text-bg-danger">+${alert.excessDurationMinutes} phút</span></td>
            </tr>`).join("");
        } catch {
            section.classList.remove("d-none");
            loading.classList.add("d-none");
            error.textContent = "Không thể tải cảnh báo quá thời lượng. Vui lòng thử lại.";
            error.classList.remove("d-none");
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
