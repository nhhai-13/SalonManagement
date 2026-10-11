window.ScheduleCoordinationPage = (() => {
    const filter = document.getElementById("schedule-coordination-filter");
    const stylist = document.getElementById("stylist-id");
    const date = document.getElementById("day-off");
    const submit = document.getElementById("load-appointments");
    const error = document.getElementById("schedule-error");
    const loading = document.getElementById("schedule-loading");
    const empty = document.getElementById("schedule-empty");
    const results = document.getElementById("schedule-results");
    const rows = document.getElementById("schedule-appointments");
    const count = document.getElementById("schedule-count");
    const statusLabels = {
        Pending: "Chờ xác nhận",
        Confirmed: "Đã xác nhận",
        Arrived: "Khách đã đến"
    };

    const textCell = value => {
        const cell = document.createElement("td");
        cell.textContent = value || "—";
        return cell;
    };

    function setBusy(isBusy) {
        submit.disabled = isBusy || !stylist.value || !date.value;
        loading.classList.toggle("d-none", !isBusy);
    }

    function renderAppointments(payload) {
        rows.replaceChildren();
        count.textContent = `${payload.totalCount} lịch cần xử lý`;
        if (payload.totalCount === 0) {
            results.classList.add("d-none");
            empty.classList.remove("d-none");
            return;
        }

        empty.classList.add("d-none");
        results.classList.remove("d-none");
        for (const appointment of payload.appointments) {
            const row = document.createElement("tr");
            row.append(
                textCell(appointment.customerName),
                textCell(appointment.services.join(", ") || "Chưa có dịch vụ"),
                textCell(appointment.startTime.slice(0, 5)),
                textCell(`${appointment.durationMinutes} phút`),
                textCell(statusLabels[appointment.status] || appointment.status),
                textCell([appointment.customerPhone, appointment.customerEmail].filter(Boolean).join(" · "))
            );
            rows.append(row);
        }
    }

    async function loadAppointments(event) {
        event?.preventDefault();
        error.classList.add("d-none");
        empty.classList.add("d-none");
        results.classList.add("d-none");
        if (!filter.reportValidity()) return;

        setBusy(true);
        try {
            const query = new URLSearchParams({ stylistId: stylist.value, date: date.value });
            const response = await SalonAuth.authenticatedFetch(`/api/schedule-coordination/appointments?${query}`);
            const payload = await response.json().catch(() => ({}));
            if (response.status === 401) return SalonAuth.redirectToLogin("Vui lòng đăng nhập lại để xem lịch.");
            if (!response.ok) throw new Error(payload.message || "Không thể tải lịch hẹn. Vui lòng thử lại.");
            renderAppointments(payload);
        } catch (loadError) {
            error.textContent = loadError.message || "Không thể tải lịch hẹn. Vui lòng thử lại.";
            error.classList.remove("d-none");
        } finally {
            setBusy(false);
        }
    }

    async function initialize() {
        date.value = new Date().toLocaleDateString("sv-SE", { timeZone: "Asia/Ho_Chi_Minh" });
        filter.addEventListener("submit", loadAppointments);
        stylist.addEventListener("change", () => { submit.disabled = !stylist.value || !date.value; });
        date.addEventListener("change", () => { submit.disabled = !stylist.value || !date.value; });

        try {
            const response = await SalonAuth.authenticatedFetch("/api/schedule-coordination/stylists");
            const payload = await response.json().catch(() => []);
            if (response.status === 401) return SalonAuth.redirectToLogin("Vui lòng đăng nhập bằng tài khoản lễ tân.");
            if (!response.ok) throw new Error("Không thể tải danh sách thợ.");

            stylist.replaceChildren(new Option("Chọn thợ", ""));
            for (const item of payload) stylist.add(new Option(item.name, item.id));
            stylist.disabled = payload.length === 0;
            submit.disabled = payload.length === 0;
            if (payload.length === 0) {
                document.getElementById("schedule-coordination-page").classList.remove("d-none");
                error.textContent = "Chưa có thợ đang hoạt động để chọn.";
                error.classList.remove("d-none");
                return;
            }

            document.getElementById("schedule-coordination-page").classList.remove("d-none");
            document.getElementById("logout").addEventListener("click", async () => {
                await fetch("/api/auth/logout", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ refreshToken: localStorage.getItem("salon.refreshToken") || sessionStorage.getItem("salon.refreshToken") })
                }).catch(() => {});
                SalonAuth.redirectToLogin();
            });
        } catch (loadError) {
            error.textContent = loadError.message || "Không thể tải danh sách thợ. Vui lòng tải lại trang.";
            error.classList.remove("d-none");
            document.getElementById("schedule-coordination-page").classList.remove("d-none");
        }
    }

    return { initialize };
})();
