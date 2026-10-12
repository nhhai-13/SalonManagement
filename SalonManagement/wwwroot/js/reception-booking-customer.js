window.ReceptionBookingCustomerPage = (() => {
    const byId = id => document.getElementById(id);

    async function initialize() {
        byId("booking-customer-logout").addEventListener("click", async () => {
            await fetch("/api/auth/logout", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ refreshToken: localStorage.getItem("salon.refreshToken") || sessionStorage.getItem("salon.refreshToken") })
            }).catch(() => {});
            SalonAuth.redirectToLogin();
        });
        byId("reception-booking-customer-form").addEventListener("submit", resolveCustomer);
        const today = new Date().toLocaleDateString("sv-SE", { timeZone: "Asia/Ho_Chi_Minh" });
        byId("reception-booking-date").min = today;
        byId("reception-booking-date").value = new Date(Date.now() + 24 * 60 * 60 * 1000)
            .toLocaleDateString("sv-SE", { timeZone: "Asia/Ho_Chi_Minh" });
        byId("reception-booking-time").value = "10:00";
        await loadOptions();
        byId("reception-booking-customer-page").classList.remove("d-none");
    }

    async function loadOptions() {
        try {
            const response = await fetch("/api/bookings/options");
            if (!response.ok) throw new Error("Không thể tải dịch vụ và chuyên viên.");
            const options = await response.json();
            fillOptions("reception-booking-service", options.services, "Chọn dịch vụ");
            fillOptions("reception-booking-stylist", options.stylists, "Chọn chuyên viên");
        } catch (error) {
            showResult(error.message || "Không thể tải lựa chọn đặt lịch.", "danger");
        }
    }

    function fillOptions(id, options, placeholder) {
        const select = byId(id);
        select.replaceChildren(new Option(placeholder, ""));
        options.forEach(item => select.add(new Option(item.name, item.id)));
    }

    async function resolveCustomer(event) {
        event.preventDefault();
        const name = byId("reception-customer-name").value.trim();
        const phone = byId("reception-customer-phone").value.trim();
        const serviceId = byId("reception-booking-service").value;
        const stylistId = byId("reception-booking-stylist").value;
        const appointmentDate = byId("reception-booking-date").value;
        const startTime = byId("reception-booking-time").value;
        const button = byId("reception-customer-submit");
        if (!name || !phone || !serviceId || !stylistId || !appointmentDate || !startTime) {
            showResult("Vui lòng nhập đủ thông tin khách, dịch vụ, chuyên viên và thời gian hẹn.", "danger");
            return;
        }

        button.disabled = true;
        showResult("Đang tạo lịch hẹn và liên kết hồ sơ khách...", "info");
        try {
            const response = await SalonAuth.authenticatedFetch("/api/reception/bookings", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    fullName: name,
                    phone,
                    serviceId: Number(serviceId),
                    stylistId: Number(stylistId),
                    appointmentDate,
                    startTime: startTime.length === 5 ? `${startTime}:00` : startTime
                })
            });
            if (response.status === 401) return SalonAuth.redirectToLogin("Vui lòng đăng nhập bằng tài khoản lễ tân.");
            if (response.status === 403) throw new Error("Tài khoản hiện tại không có quyền đặt lịch tại quầy.");
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.message || "Không thể tạo lịch hẹn.");
            showResult(result.isReturningCustomer
                ? `Đã nhận diện khách quen và tạo lịch #${result.appointmentId}.`
                : `Đã tạo hồ sơ khách mới và lịch hẹn #${result.appointmentId}.`, "success");
            if (result.isReturningCustomer && Number.isSafeInteger(result.customerId) && result.customerId > 0) {
                const link = document.createElement("a");
                link.href = `/reception/customers/${result.customerId}`;
                link.className = "alert-link ms-2";
                link.textContent = "Mở hồ sơ khách";
                byId("reception-customer-result").append(" ", link);
            }
        } catch (error) {
            showResult(error.message || "Không thể kiểm tra hồ sơ khách.", "danger");
        } finally {
            button.disabled = false;
        }
    }

    function showResult(message, kind) {
        const result = byId("reception-customer-result");
        result.textContent = message;
        result.className = `alert alert-${kind} mt-3 mb-0`;
    }

    return { initialize };
})();
