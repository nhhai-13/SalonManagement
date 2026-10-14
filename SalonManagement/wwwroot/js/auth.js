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
        clearSession();
        const query = message ? `?message=${encodeURIComponent(message)}` : "";
        location.replace("/admin/login" + query);
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
            const lockedAccounts = new Map();
            let lockoutTimer;
            const normalizeEmail = value => value.trim().toLowerCase();
            const applyLockoutState = () => {
                clearTimeout(lockoutTimer);
                const lockedUntil = lockedAccounts.get(normalizeEmail(email.value));
                const remainingMs = lockedUntil ? lockedUntil - Date.now() : 0;
                if (remainingMs <= 0) {
                    if (lockedUntil) lockedAccounts.delete(normalizeEmail(email.value));
                    submit.disabled = false;
                    submit.classList.remove("loading");
                    submit.querySelector(".button-label").textContent = "Đăng nhập";
                    return;
                }

                const remainingSeconds = Math.ceil(remainingMs / 1000);
                const minutes = Math.floor(remainingSeconds / 60).toString().padStart(2, "0");
                const seconds = (remainingSeconds % 60).toString().padStart(2, "0");
                submit.disabled = true;
                submit.classList.remove("loading");
                submit.querySelector(".button-label").textContent = `Tài khoản bị khóa (${minutes}:${seconds})`;
                lockoutTimer = setTimeout(applyLockoutState, 1000);
            };
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
                if (input === email) applyLockoutState();
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
                            password: password.value
                        })
                    });
                    if (!response.ok) {
                        const result = await response.json().catch(() => ({}));
                        if (result.requiresEmailVerification && result.email) {
                            location.replace(`/verify-email?email=${encodeURIComponent(result.email)}`);
                            return;
                        }
                        if (result.lockedUntilUtc) {
                            const lockedUntil = Date.parse(result.lockedUntilUtc);
                            if (Number.isFinite(lockedUntil)) {
                                lockedAccounts.set(normalizeEmail(email.value), lockedUntil);
                                applyLockoutState();
                            }
                        }
                        errorMessage.textContent = result.message || "Email hoặc mật khẩu không chính xác.";
                        error.classList.remove("d-none");
                        return;
                    }
                    const result = await response.json();
                    saveSession(
                        result,
                        document.getElementById("remember")?.checked === true,
                        result.role
                    );
                    location.replace(result.redirectUrl || "/admin/login");
                } catch {
                    errorMessage.textContent = "Không thể kết nối đến hệ thống. Vui lòng thử lại.";
                    error.classList.remove("d-none");
                } finally {
                    applyLockoutState();
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
                    if (result.requiresEmailVerification) {
                        location.replace(result.redirectUrl || `/verify-email?email=${encodeURIComponent(result.email || email.value.trim())}&portal=${encodeURIComponent(form.dataset.role)}`);
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
        bindEmailVerificationForm() {
            const form = document.getElementById("verify-email-form");
            if (!form) return;
            const code = document.getElementById("verification-code");
            const submit = document.getElementById("verify-email-submit");
            const resend = document.getElementById("resend-verification");
            const message = document.getElementById("verify-email-message");
            const messageText = message.querySelector("span") || message;
            const showMessage = (text, success = false) => {
                messageText.textContent = text;
                message.classList.remove("d-none", "login-alert-success");
                if (success) message.classList.add("login-alert-success");
            };

            code.addEventListener("input", () => {
                code.value = code.value.replace(/\D/g, "").slice(0, 6);
                code.closest(".field-group")?.classList.remove("invalid");
                message.classList.add("d-none");
            });

            form.addEventListener("submit", async event => {
                event.preventDefault();
                const valid = /^\d{6}$/.test(code.value);
                code.closest(".field-group")?.classList.toggle("invalid", !valid);
                if (!valid) return;
                submit.disabled = true;
                submit.classList.add("loading");
                try {
                    const response = await fetch("/api/auth/confirm-email", {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ email: form.dataset.email, code: code.value })
                    });
                    const result = await response.json().catch(() => ({}));
                    if (!response.ok) return showMessage(result.message || "Không thể xác minh email.");
                    location.replace(`${form.dataset.login}?message=${encodeURIComponent(result.message)}`);
                } catch {
                    showMessage("Không thể kết nối đến hệ thống. Vui lòng thử lại.");
                } finally {
                    submit.disabled = false;
                    submit.classList.remove("loading");
                }
            });

            resend.addEventListener("click", async () => {
                resend.disabled = true;
                try {
                    const response = await fetch("/api/auth/resend-email-verification", {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ email: form.dataset.email })
                    });
                    const result = await response.json().catch(() => ({}));
                    showMessage(result.message || "Không thể gửi lại mã.", response.ok);
                } catch {
                    showMessage("Không thể kết nối đến hệ thống. Vui lòng thử lại.");
                } finally {
                    resend.disabled = false;
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
                    body: JSON.stringify({ ...Object.fromEntries(new FormData(form)), isActive: form.elements.isActive.value === "true" })
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
            this.staffAccounts = result.items;
            target.innerHTML = result.items.length ? result.items.map(account => `
                <tr data-id="${account.id}">
                    <td><strong>${this.escapeHtml(account.email)}</strong><small class="d-block">${this.escapeHtml(account.fullName)}</small>${account.phoneNumber ? `<small class="d-block text-muted">${this.escapeHtml(account.phoneNumber)}</small>` : ""}</td>
                    <td>${this.escapeHtml(account.role)}</td>
                    <td><span class="badge ${account.isActive ? "bg-success" : "bg-secondary"}">${account.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}</span>${account.isLockedOut ? '<span class="badge bg-warning text-dark ms-1">Tạm khóa</span>' : ""}</td>
                    <td class="text-end"><button class="btn btn-sm btn-outline-primary staff-edit">Sửa</button>${account.isLockedOut
                        ? '<button class="btn btn-sm btn-outline-warning staff-unlock"><i class="bi bi-unlock me-1"></i>Mở khóa</button>'
                        : ""}${account.role === "Admin"
                        ? '<span class="badge bg-light text-dark border ms-1" title="Tài khoản Admin không thể bị ngừng hoặc thu hồi phiên"><i class="bi bi-shield-lock me-1"></i>Được bảo vệ</span>'
                        : `<button class="btn btn-sm ${account.isActive ? "btn-outline-danger" : "btn-outline-success"} staff-status ms-1" data-active="${!account.isActive}">${account.isActive ? "Ngừng" : "Kích hoạt"}</button><button class="btn btn-sm btn-outline-secondary ms-1 staff-revoke">Thu hồi phiên</button>`}</td>
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
            const editForm = document.getElementById("staff-edit-form");
            const dialog = document.getElementById("staff-edit-dialog");
            editForm?.addEventListener("submit", async event => {
                event.preventDefault();
                const values = editForm.elements;
                const submit = editForm.querySelector('[type="submit"]');
                submit.disabled = true;
                try {
                    const response = await authenticatedFetch(`/api/admin/staff/${encodeURIComponent(values.id.value)}`, {
                        method: "PUT", headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ fullName: values.fullName.value, email: values.email.value, phoneNumber: values.phoneNumber.value, role: values.role.value })
                    });
                    const result = await response.json().catch(() => ({}));
                    if (!response.ok) { document.getElementById("staff-edit-error").textContent = result.message || "Vui lòng kiểm tra dữ liệu."; return; }
                    dialog.close();
                    await this.loadStaffAccounts();
                } catch { document.getElementById("staff-edit-error").textContent = "Không thể kết nối đến hệ thống."; }
                finally { submit.disabled = false; }
            });
            document.getElementById("staff-filter-form")?.addEventListener("submit", event => { event.preventDefault(); this.loadStaffAccounts(); });
            document.getElementById("staff-refresh")?.addEventListener("click", () => this.loadStaffAccounts());
            list.addEventListener("click", async event => {
                const button = event.target.closest("button");
                const row = event.target.closest("tr[data-id]");
                if (!button || !row) return;
                if (button.classList.contains("staff-edit")) {
                    const account = this.staffAccounts.find(item => item.id === row.dataset.id);
                    for (const key of ["id", "fullName", "email", "phoneNumber", "role"]) editForm.elements[key].value = account[key] || "";
                    editForm.elements.role.disabled = account.role === "Admin";
                    document.getElementById("staff-edit-error").textContent = "";
                    dialog.showModal();
                    return;
                }
                const message = document.getElementById("staff-account-message");
                button.disabled = true;
                const originalContent = button.innerHTML;
                button.innerHTML = '<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>Đang xử lý';
                try {
                    let response;
                    if (button.classList.contains("staff-unlock")) {
                        response = await authenticatedFetch(`/api/admin/staff/${encodeURIComponent(row.dataset.id)}/unlock`, { method: "POST" });
                    } else if (button.classList.contains("staff-status")) {
                        response = await authenticatedFetch(`/api/admin/staff/${encodeURIComponent(row.dataset.id)}/status`, { method: "PATCH", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ isActive: button.dataset.active === "true" }) });
                    } else if (button.classList.contains("staff-revoke")) {
                        response = await authenticatedFetch(`/api/admin/staff/${encodeURIComponent(row.dataset.id)}/revoke-sessions`, { method: "POST" });
                    } else return;
                    const result = await response.json().catch(() => ({}));
                    message.textContent = result.message || (response.ok ? "Đã cập nhật." : "Không thể cập nhật.");
                    message.className = `alert ${response.ok ? "alert-success" : "alert-danger"}`;
                    if (response.ok) await this.loadStaffAccounts();
                } catch {
                    message.textContent = "Không thể kết nối đến hệ thống. Vui lòng thử lại.";
                    message.className = "alert alert-danger";
                } finally {
                    if (button.isConnected) {
                        button.disabled = false;
                        button.innerHTML = originalContent;
                    }
                }
            });
            this.loadStaffAccounts();
        },
        async bindRoleManagement() {
            const target = document.getElementById("role-account-list");
            if (!target) return;
            const message = document.getElementById("role-management-message");
            const showMessage = (text, success) => {
                message.textContent = text;
                message.className = `alert ${success ? "alert-success" : "alert-danger"}`;
            };
            try {
                const response = await authenticatedFetch("/api/admin/staff");
                const result = await response.json().catch(() => ({}));
                if (!response.ok) return showMessage(result.message || "Không thể tải danh sách tài khoản.", false);
                this.roleAccounts = result.items || [];
                target.innerHTML = this.roleAccounts.length ? this.roleAccounts.map(account => `
                    <tr data-id="${account.id}">
                        <td><strong>${this.escapeHtml(account.fullName)}</strong><small class="d-block">${this.escapeHtml(account.email)}</small></td>
                        <td><span class="badge ${account.isActive ? "bg-success" : "bg-secondary"}">${account.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}</span></td>
                        <td><select class="sl-select role-select" aria-label="Vai trò của ${this.escapeHtml(account.email)}">
                            ${["Admin", "Owner", "Receptionist", "Stylist"].map(role => `<option value="${role}" ${account.role === role ? "selected" : ""}>${role === "Receptionist" ? "Lễ tân" : role === "Stylist" ? "Thợ" : role === "Owner" ? "Chủ tiệm" : "Quản trị"}</option>`).join("")}
                        </select></td>
                        <td class="text-end"><button class="sl-btn sl-btn-outline sl-btn-sm role-save" type="button">Lưu vai trò</button></td>
                    </tr>`).join("") : '<tr><td colspan="4" class="sl-table-empty">Chưa có tài khoản nội bộ.</td></tr>';
            } catch {
                showMessage("Không thể kết nối đến hệ thống.", false);
                return;
            }
            target.addEventListener("click", async event => {
                const button = event.target.closest(".role-save");
                const row = event.target.closest("tr[data-id]");
                if (!button || !row) return;
                const account = this.roleAccounts.find(item => item.id === row.dataset.id);
                if (!account) return;
                const role = row.querySelector(".role-select").value;
                if (role === account.role) return showMessage("Vai trò chưa thay đổi.", true);
                button.disabled = true;
                try {
                    const response = await authenticatedFetch(`/api/admin/staff/${encodeURIComponent(account.id)}/role`, {
                        method: "PATCH",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ role })
                    });
                    const result = await response.json().catch(() => ({}));
                    if (!response.ok) return showMessage(result.message || "Không thể cập nhật vai trò.", false);
                    account.role = role;
                    showMessage("Đã cập nhật vai trò. Quyền mới có hiệu lực ở lần gọi API tiếp theo.", true);
                } catch {
                    showMessage("Không thể kết nối đến hệ thống.", false);
                } finally {
                    button.disabled = false;
                }
            });
        },
        async requireSession(options = {}) {
            const endpoint = options.endpoint || "/api/admin/session";
            const contentId = options.contentId || "admin-content";
            const response = await authenticatedFetch(endpoint);
            if (response.status === 403) {
                const error = await response.json().catch(() => ({}));
                if (error.redirectUrl) { location.replace(error.redirectUrl); return; }
                const current = await authenticatedFetch("/api/auth/session");
                if (current.ok) { location.replace((await current.json()).redirectUrl); return; }
            }
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
            document.getElementById("logout")?.addEventListener("click", async event => {
                event.preventDefault();
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

