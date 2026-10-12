(() => {
    const menuButton = document.querySelector(".home-menu-toggle");
    const menu = document.querySelector(".home-nav");
    menuButton?.addEventListener("click", () => {
        const open = menu?.classList.toggle("open") === true;
        menuButton.setAttribute("aria-expanded", open.toString());
        menuButton.querySelector("i")?.classList.toggle("bi-list", !open);
        menuButton.querySelector("i")?.classList.toggle("bi-x-lg", open);
    });
    menu?.querySelectorAll("a").forEach(link => link.addEventListener("click", () => {
        menu.classList.remove("open");
        menuButton?.setAttribute("aria-expanded", "false");
    }));

    const bookingForm = document.getElementById("quick-booking-form");
    const bookingDate = document.getElementById("booking-date");
    if (bookingDate) {
        const today = new Date().toLocaleDateString("sv-SE", { timeZone: "Asia/Ho_Chi_Minh" });
        bookingDate.min = today;
        bookingDate.value = new Date(Date.now() + 24 * 60 * 60 * 1000)
            .toLocaleDateString("sv-SE", { timeZone: "Asia/Ho_Chi_Minh" });
    }
    const bookingTime = document.getElementById("booking-time");
    if (bookingTime) bookingTime.value = "10:00";
    bookingForm?.addEventListener("submit", async event => {
        event.preventDefault();
        const phone = document.getElementById("booking-phone");
        const name = document.getElementById("booking-name");
        const service = document.getElementById("booking-service");
        const stylist = document.getElementById("booking-stylist");
        const message = document.getElementById("booking-message");
        const missing = !name.value.trim() || !phone.value.trim() || !service.value || !stylist.value || !bookingDate.value || !bookingTime.value;
        if (missing) {
            message.textContent = !name.value.trim() ? "Vui lòng nhập họ tên."
                : !phone.value.trim() ? "Vui lòng nhập số điện thoại."
                    : !service.value ? "Vui lòng chọn dịch vụ."
                        : !stylist.value ? "Vui lòng chọn chuyên viên."
                            : "Vui lòng chọn ngày và giờ hẹn.";
            message.classList.add("show");
            (!name.value.trim() ? name : !phone.value.trim() ? phone : !service.value ? service : !stylist.value ? stylist : bookingDate).focus();
            return;
        }
        const button = bookingForm.querySelector('button[type="submit"]');
        button.disabled = true;
        message.textContent = "Đang tạo lịch hẹn và liên kết hồ sơ khách hàng...";
        message.classList.add("show");
        try {
            const response = await fetch("/api/bookings", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    fullName: name.value.trim(),
                    phone: phone.value.trim(),
                    serviceId: Number(service.value),
                    stylistId: Number(stylist.value),
                    appointmentDate: bookingDate.value,
                    startTime: bookingTime.value.length === 5 ? `${bookingTime.value}:00` : bookingTime.value
                })
            });
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.message || "Không thể ghi nhận yêu cầu. Vui lòng thử lại.");
            message.textContent = result.isReturningCustomer
                ? `Đã nhận diện khách quen và gửi lịch hẹn #${result.appointmentId} để Lumière xác nhận.`
                : `Đã tạo hồ sơ khách mới và gửi lịch hẹn #${result.appointmentId} để Lumière xác nhận.`;
            bookingForm.reset();
        } catch (error) {
            message.textContent = error.message || "Không thể ghi nhận yêu cầu. Vui lòng thử lại.";
        } finally {
            button.disabled = false;
            message.classList.add("show");
        }
    });

    const backToTop = document.getElementById("back-to-top");
    window.addEventListener("scroll", () => backToTop?.classList.toggle("show", window.scrollY > 500), { passive: true });
    backToTop?.addEventListener("click", () => window.scrollTo({ top: 0, behavior: "smooth" }));
})();
