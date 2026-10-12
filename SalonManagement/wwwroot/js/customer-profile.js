window.ReceptionCustomerProfilePage = (() => {
    const byId = id => document.getElementById(id);

    async function initialize() {
        const page = byId("customer-profile-page");
        const customerId = Number(page.dataset.customerId);
        byId("customer-profile-logout").addEventListener("click", async () => {
            await fetch("/api/auth/logout", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ refreshToken: localStorage.getItem("salon.refreshToken") || sessionStorage.getItem("salon.refreshToken") })
            }).catch(() => {});
            SalonAuth.redirectToLogin();
        });
        byId("customer-profile-form").addEventListener("submit", save);
        page.classList.remove("d-none");
        await load(customerId);
    }

    async function load(customerId) {
        try {
            const response = await SalonAuth.authenticatedFetch(`/api/reception/customers/${customerId}`);
            if (response.status === 401) return SalonAuth.redirectToLogin("Vui lòng đăng nhập bằng tài khoản lễ tân.");
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.message || "Không thể tải hồ sơ khách.");
            render(result);
            byId("customer-profile-loading").classList.add("d-none");
            byId("customer-profile-content").classList.remove("d-none");
        } catch (error) {
            byId("customer-profile-loading").classList.add("d-none");
            const alert = byId("customer-profile-error");
            alert.textContent = error.message || "Không thể tải hồ sơ khách.";
            alert.classList.remove("d-none");
        }
    }

    async function save(event) {
        event.preventDefault();
        const name = byId("customer-profile-name").value.trim();
        if (!name) {
            showResult("Vui lòng nhập họ tên khách.", "danger");
            byId("customer-profile-name").focus();
            return;
        }

        const page = byId("customer-profile-page");
        const button = byId("customer-profile-save");
        button.disabled = true;
        showResult("Đang lưu thông tin hồ sơ...", "info");
        try {
            const response = await SalonAuth.authenticatedFetch(`/api/reception/customers/${Number(page.dataset.customerId)}`, {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    fullName: name,
                    email: byId("customer-profile-email").value.trim() || null,
                    notes: byId("customer-profile-notes").value.trim() || null
                })
            });
            if (response.status === 401) return SalonAuth.redirectToLogin("Phiên đăng nhập đã hết hạn.");
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.message || "Không thể lưu hồ sơ khách.");
            render(result);
            showResult("Đã lưu thay đổi hồ sơ khách.", "success");
        } catch (error) {
            showResult(error.message || "Không thể lưu hồ sơ khách.", "danger");
        } finally {
            button.disabled = false;
        }
    }

    function render(profile) {
        byId("customer-profile-name").value = profile.fullName || "";
        byId("customer-profile-phone").textContent = profile.phone || "";
        byId("customer-profile-email").value = profile.email || "";
        byId("customer-profile-notes").value = profile.notes || "";
        byId("customer-profile-visits").textContent = String(profile.visitCount || 0);
        byId("customer-profile-no-shows").textContent = String(profile.noShowCount || 0);
        byId("customer-profile-updated-at").textContent = profile.updatedAt
            ? `Cập nhật gần nhất: ${new Date(profile.updatedAt).toLocaleString("vi-VN")}`
            : "";
    }

    function showResult(message, kind) {
        const result = byId("customer-profile-result");
        result.textContent = message;
        result.className = `alert alert-${kind} mt-3 mb-0`;
    }

    return { initialize };
})();
