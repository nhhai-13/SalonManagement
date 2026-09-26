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
        const loginPath = portal === "stylist" ? "/stylist/login" : portal === "reception" ? "/reception/login" : "/admin/login";
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
        async requireSession() {
            const response = await authenticatedFetch("/api/admin/session");
            if (!response.ok) return redirectToLogin("Phiên đăng nhập đã hết hạn.");
            scheduleRefresh();
            document.getElementById("admin-content").classList.remove("d-none");
            document.getElementById("logout").addEventListener("click", async () => {
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
