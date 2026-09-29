window.SalonAuth = (() => {
    const accessKey = "salon.accessToken";
    const refreshKey = "salon.refreshToken";
    const accessExpiryKey = "salon.accessExpiresAt";
    const refreshExpiryKey = "salon.refreshExpiresAt";
    const portalKey = "salon.portal";
    let refreshTimer;

    function activeStorage() {
        return localStorage.getItem(refreshKey) ? localStorage : sessionStorage;
    }

    function clearSession() {
        [localStorage, sessionStorage].forEach(storage => {
            storage.removeItem(accessKey);
            storage.removeItem(refreshKey);
            storage.removeItem(accessExpiryKey);
            storage.removeItem(refreshExpiryKey);
            storage.removeItem(portalKey);
        });
        clearTimeout(refreshTimer);
    }

    function saveSession(tokens, remember, portal) {
        const existingStorage = activeStorage();
        const storage = remember === undefined ? existingStorage : (remember ? localStorage : sessionStorage);
        if (remember !== undefined) clearSession();
        storage.setItem(accessKey, tokens.accessToken);
        storage.setItem(refreshKey, tokens.refreshToken);
        storage.setItem(accessExpiryKey, tokens.accessTokenExpiresAtUtc);
        storage.setItem(refreshExpiryKey, tokens.refreshTokenExpiresAtUtc);
        if (portal) storage.setItem(portalKey, portal);
        scheduleRefresh();
    }

    function scheduleRefresh() {
        clearTimeout(refreshTimer);
        const storage = activeStorage();
        const accessExpiry = Date.parse(storage.getItem(accessExpiryKey) || "");
        const refreshExpiry = Date.parse(storage.getItem(refreshExpiryKey) || "");
        if (!Number.isFinite(refreshExpiry) || refreshExpiry <= Date.now()) {
            if (storage.getItem(refreshKey)) redirectToLogin("Phiên đăng nhập đã hết hạn.");
            return;
        }
        const delay = Number.isFinite(accessExpiry)
            ? Math.max(0, Math.min(accessExpiry - Date.now() - 30_000, 2_147_000_000))
            : 0;
        refreshTimer = setTimeout(async () => {
            if (!await refreshSession()) return redirectToLogin("Phiên đăng nhập đã hết hạn.");
            scheduleRefresh();
        }, delay);
    }

    async function refreshSession() {
        const storage = activeStorage();
        const refreshToken = storage.getItem(refreshKey);
        if (!refreshToken) return false;
        try {
            const response = await fetch("/api/auth/refresh", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ refreshToken })
            });
            if (!response.ok) return false;
            saveSession(await response.json(), undefined, storage.getItem(portalKey));
            return true;
        } catch {
            return false;
        }
    }

    async function authenticatedFetch(url, options = {}, retry = true) {
        const headers = new Headers(options.headers || {});
        headers.set("Authorization", `Bearer ${activeStorage().getItem(accessKey) || ""}`);
        const response = await fetch(url, { ...options, headers });
        if (response.status === 401 && retry && await refreshSession()) {
            return authenticatedFetch(url, options, false);
        }
        return response;
    }

    function redirectToLogin(message) {
        const portal = activeStorage().getItem(portalKey) || "admin";
        clearSession();
        const loginPath = portal === "stylist"
            ? "/stylist/login"
            : portal === "reception"
                ? "/reception/login"
                : portal === "owner"
                    ? "/owner/login"
                    : "/admin/login";
        const query = message ? `?message=${encodeURIComponent(message)}` : "";
        location.replace(loginPath + query);
    }

    return {
        authenticatedFetch,
        redirectToLogin,
        bindLoginForm() {
            const form = document.getElementById("login-form");
            const email = document.getElementById("email");
            const password = document.getElementById("password");
            const submit = document.getElementById("login-submit");
            const error = document.getElementById("login-error");
            const errorMessage = error.querySelector("span") || error;
            const toggle = document.getElementById("toggle-password");
            const pageMessage = new URLSearchParams(location.search).get("message");
            if (pageMessage) {
                errorMessage.textContent = pageMessage;
                error.classList.remove("d-none");
                history.replaceState({}, "", location.pathname);
            }

            toggle?.addEventListener("click", () => {
                const reveal = password.type === "password";
                password.type = reveal ? "text" : "password";
                toggle.setAttribute("aria-pressed", reveal.toString());
                toggle.setAttribute("aria-label", reveal ? "Ẩn mật khẩu" : "Hiện mật khẩu");
                password.focus();
            });

            [email, password].forEach(input => input.addEventListener("input", () => {
                input.closest(".field-group")?.classList.remove("invalid");
                error.classList.add("d-none");
            }));

            form.addEventListener("submit", async event => {
                event.preventDefault();
                error.classList.add("d-none");
                let valid = true;
                [email, password].forEach(input => {
                    const invalid = !input.value.trim() || (input === email && !input.validity.valid);
                    input.closest(".field-group")?.classList.toggle("invalid", invalid);
                    valid = valid && !invalid;
                });
                if (!valid) return;

                submit.disabled = true;
                submit.classList.add("loading");
                submit.querySelector(".button-label").textContent = "Đang đăng nhập...";

                try {
                    const response = await fetch("/api/auth/login", {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({
                            email: email.value.trim(),
                            password: password.value,
                            portal: form.dataset.portal || null
                        })
                    });
                    if (!response.ok) {
                        const result = await response.json().catch(() => ({}));
                        errorMessage.textContent = result.message || "Email hoặc mật khẩu không chính xác.";
                        error.classList.remove("d-none");
                        return;
                    }
                    saveSession(
                        await response.json(),
                        document.getElementById("remember")?.checked === true,
                        form.dataset.portal || "admin"
                    );
                    location.replace(form.dataset.redirect || "/admin");
                } catch {
                    errorMessage.textContent = "Không thể kết nối đến hệ thống. Vui lòng thử lại.";
                    error.classList.remove("d-none");
                } finally {
                    submit.disabled = false;
                    submit.classList.remove("loading");
                    submit.querySelector(".button-label").textContent = "Đăng nhập";
                }
            });
        },
        bindRegisterForm() {
            const form = document.getElementById("register-form");
            const email = document.getElementById("email");
            const password = document.getElementById("password");
            const confirmPassword = document.getElementById("confirm-password");
            const submit = document.getElementById("register-submit");
            const error = document.getElementById("register-error");
            const errorMessage = error.querySelector("span") || error;

            form.addEventListener("submit", async event => {
                event.preventDefault();
                error.classList.add("d-none");
                const fields = [email, password, confirmPassword];
                fields.forEach(input => input.closest(".field-group")?.classList.remove("invalid"));
                let valid = fields.every(input => input.checkValidity());
                if (password.value !== confirmPassword.value) {
                    confirmPassword.closest(".field-group")?.classList.add("invalid");
                    valid = false;
                }
                fields.filter(input => !input.checkValidity()).forEach(input => input.closest(".field-group")?.classList.add("invalid"));
                if (!valid) return;

                submit.disabled = true;
                submit.classList.add("loading");
                submit.querySelector(".button-label").textContent = "Đang tạo...";
                try {
                    const response = await fetch("/api/auth/register", {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ email: email.value.trim(), password: password.value, role: form.dataset.role })
                    });
                    const result = await response.json().catch(() => ({}));
                    if (!response.ok) {
                        errorMessage.textContent = result.message || "Không thể tạo tài khoản.";
                        error.classList.remove("d-none");
                        return;
                    }
                    location.replace(`${form.dataset.login}?message=${encodeURIComponent(result.message)}`);
                } catch {
                    errorMessage.textContent = "Không thể kết nối đến hệ thống. Vui lòng thử lại.";
                    error.classList.remove("d-none");
                } finally {
                    submit.disabled = false;
                    submit.classList.remove("loading");
                    submit.querySelector(".button-label").textContent = "Tạo tài khoản";
                }
            });
        },
        bindStaffAccountForm() {
            const form = document.getElementById("staff-account-form");
            if (!form) return;
            const message = document.getElementById("staff-account-message");
            form.addEventListener("submit", async event => {
                event.preventDefault();
                message.className = "alert d-none";
                if (!form.checkValidity()) return form.reportValidity();
                const response = await authenticatedFetch("/api/admin/staff-accounts", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(Object.fromEntries(new FormData(form)))
                });
                const result = await response.json().catch(() => ({}));
                message.textContent = result.message || (response.ok ? "Đã tạo tài khoản." : "Không thể tạo tài khoản.");
                message.className = `alert ${response.ok ? "alert-success" : "alert-danger"}`;
                if (response.ok) {
                    form.reset();
                    await this.loadStaffAccounts();
                }
            });
        },
        async loadStaffAccounts() {
            const target = document.getElementById("staff-account-list");
            if (!target) return;
            const search = document.getElementById("staff-search")?.value.trim() || "";
            const role = document.getElementById("staff-filter-role")?.value || "";
            const status = document.getElementById("staff-filter-status")?.value || "";
            const query = new URLSearchParams();
            if (search) query.set("search", search);
            if (role) query.set("role", role);
            if (status) query.set("isActive", status);
            const response = await authenticatedFetch(`/api/admin/staff?${query}`);
            if (!response.ok) return;
            const result = await response.json();
            target.innerHTML = result.items.length ? result.items.map(account => `
                <tr data-id="${account.id}">
                    <td><strong>${this.escapeHtml(account.email)}</strong>${account.phoneNumber ? `<small class="d-block text-muted">${this.escapeHtml(account.phoneNumber)}</small>` : ""}</td>
                    <td>${this.escapeHtml(account.role)}</td>
                    <td><span class="badge ${account.isActive ? "bg-success" : "bg-secondary"}">${account.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}</span>${account.isLockedOut ? '<span class="badge bg-warning text-dark ms-1">Tạm khóa</span>' : ""}</td>
                    <td class="text-end"><button class="btn btn-sm ${account.isActive ? "btn-outline-danger" : "btn-outline-success"} staff-status" data-active="${!account.isActive}">${account.isActive ? "Ngừng" : "Kích hoạt"}</button><button class="btn btn-sm btn-outline-secondary ms-1 staff-revoke">Thu hồi phiên</button></td>
                </tr>`).join("") : '<tr><td colspan="4" class="text-center text-muted py-4">Không có tài khoản phù hợp.</td></tr>';
        },
        escapeHtml(value) {
            const element = document.createElement("span");
            element.textContent = value || "";
            return element.innerHTML;
        },
        bindStaffManagement() {
            const list = document.getElementById("staff-account-list");
            if (!list) return;
            document.getElementById("staff-filter-form")?.addEventListener("submit", event => { event.preventDefault(); this.loadStaffAccounts(); });
            document.getElementById("staff-refresh")?.addEventListener("click", () => this.loadStaffAccounts());
            list.addEventListener("click", async event => {
                const button = event.target.closest("button");
                const row = event.target.closest("tr[data-id]");
                if (!button || !row) return;
                let response;
                if (button.classList.contains("staff-status")) {
                    response = await authenticatedFetch(`/api/admin/staff/${encodeURIComponent(row.dataset.id)}/status`, { method: "PATCH", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ isActive: button.dataset.active === "true" }) });
                } else if (button.classList.contains("staff-revoke")) {
                    response = await authenticatedFetch(`/api/admin/staff/${encodeURIComponent(row.dataset.id)}/revoke-sessions`, { method: "POST" });
                } else return;
                const result = await response.json().catch(() => ({}));
                const message = document.getElementById("staff-account-message");
                message.textContent = result.message || (response.ok ? "Đã cập nhật." : "Không thể cập nhật.");
                message.className = `alert ${response.ok ? "alert-success" : "alert-danger"}`;
                await this.loadStaffAccounts();
            });
            this.loadStaffAccounts();
        },
        async requireSession(options = {}) {
            const endpoint = options.endpoint || "/api/admin/session";
            const contentId = options.contentId || "admin-content";
            const response = await authenticatedFetch(endpoint);
            if (!response.ok) return redirectToLogin("Phiên đăng nhập đã hết hạn.");
            scheduleRefresh();
            const session = await response.json().catch(() => ({}));
            document.getElementById(contentId)?.classList.remove("d-none");
            document.querySelectorAll("[data-session-email]").forEach(element => {
                element.textContent = session.email || "";
            });
            const isAdmin = session.role === "Admin";
            document.getElementById(isAdmin ? "admin-tools" : "owner-tools")?.classList.remove("d-none");
            if (isAdmin) {
                this.bindStaffAccountForm();
                this.bindStaffManagement();
            }
            document.getElementById("logout")?.addEventListener("click", async () => {
                const refreshToken = activeStorage().getItem(refreshKey);
                if (refreshToken) {
                    await fetch("/api/auth/logout", {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ refreshToken })
                    });
                }
                redirectToLogin();
            });
        }
    };
})();
