window.ReceptionBookingCustomerPage = (() => {
    const byId = id => document.getElementById(id);
    let recognizedProfile = null;
    let lastLookedUpPhone = null;
    let nameDecision = null;
    let lookupSequence = 0;
    let lookupTask = Promise.resolve(null);

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
        byId("reception-customer-phone").addEventListener("blur", () => (lookupTask = lookupCustomer()));
        byId("reception-customer-name").addEventListener("input", () => {
            nameDecision = null;
            updateNameWarning();
        });
        byId("reception-customer-keep-profile-name").addEventListener("click", () => selectNameDecision(false));
        byId("reception-customer-update-profile-name").addEventListener("click", () => selectNameDecision(true));
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

    async function lookupCustomer() {
        const phone = byId("reception-customer-phone").value.trim();
        const result = byId("reception-customer-lookup");
        const sequence = ++lookupSequence;
        result.replaceChildren();
        recognizedProfile = null;
        lastLookedUpPhone = phone;
        nameDecision = null;
        updateNameWarning();
        if (!phone) return null;

        result.className = "mt-2 small text-muted";
        result.textContent = "Đang kiểm tra hồ sơ theo số điện thoại...";
        try {
            const response = await SalonAuth.authenticatedFetch(`/api/reception/customers/lookup?phone=${encodeURIComponent(phone)}`);
            if (sequence !== lookupSequence || phone !== byId("reception-customer-phone").value.trim()) return null;
            if (response.status === 401) return SalonAuth.redirectToLogin("Vui lòng đăng nhập bằng tài khoản lễ tân.");
            const lookup = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(lookup.message || "Không thể kiểm tra hồ sơ khách.");

            result.replaceChildren();
            if (!lookup.isReturningCustomer || !lookup.profile) {
                result.textContent = "Chưa có hồ sơ cho số điện thoại này; hệ thống sẽ tạo hồ sơ khi lưu lịch.";
                return null;
            }

            recognizedProfile = lookup.profile;
            result.className = "mt-2 small text-success";
            result.append(`Đã nhận diện hồ sơ #${lookup.profile.customerId}: ${lookup.profile.fullName} `);
            const link = document.createElement("a");
            link.href = `/reception/customers/${lookup.profile.customerId}`;
            link.className = "alert-link";
            link.textContent = "Xem và cập nhật hồ sơ";
            result.append(link);
            updateNameWarning();
            return recognizedProfile;
        } catch (error) {
            if (sequence !== lookupSequence || phone !== byId("reception-customer-phone").value.trim()) return null;
            result.className = "mt-2 small text-danger";
            result.textContent = error.message || "Không thể kiểm tra hồ sơ khách.";
            return null;
        }
    }

    function namesMatch(first, second) {
        const normalize = value => (value || "").trim().replace(/\s+/g, " ").toLocaleLowerCase("vi-VN");
        return normalize(first) === normalize(second);
    }

    function updateNameWarning() {
        const warning = byId("reception-customer-name-warning");
        const shouldWarn = recognizedProfile && !namesMatch(
            byId("reception-customer-name").value,
            recognizedProfile.fullName);
        warning.classList.toggle("d-none", !shouldWarn);
        if (!shouldWarn) {
            byId("reception-customer-name-choice").textContent = "";
            return false;
        }
        byId("reception-customer-profile-name").textContent = recognizedProfile.fullName;
        byId("reception-customer-appointment-name").textContent = byId("reception-customer-name").value.trim();
        return true;
    }

    function selectNameDecision(updateProfile) {
        nameDecision = updateProfile;
        byId("reception-customer-name-choice").textContent = updateProfile
            ? "Bạn đã chọn cập nhật tên hồ sơ sau khi lịch được lưu thành công."
            : "Tên hồ sơ sẽ được giữ nguyên; tên bạn nhập chỉ lưu trên lịch mới.";
    }

    async function resolveCustomer(event) {
        event.preventDefault();
        let name = byId("reception-customer-name").value.trim();
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

        if (lastLookedUpPhone !== phone) lookupTask = lookupCustomer();
        await lookupTask;
        name = byId("reception-customer-name").value.trim();
        if (!name) {
            showResult("Vui lòng nhập họ tên khách.", "danger");
            byId("reception-customer-name").focus();
            return;
        }
        if (updateNameWarning() && nameDecision === null) {
            showResult("Vui lòng chọn giữ nguyên tên hồ sơ hoặc xác nhận cập nhật tên trước khi tạo lịch.", "warning");
            byId("reception-customer-name-warning").scrollIntoView?.({ behavior: "smooth", block: "center" });
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
                    startTime: startTime.length === 5 ? `${startTime}:00` : startTime,
                    updateCustomerProfileName: nameDecision === true
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
