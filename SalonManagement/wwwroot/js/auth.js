window.SalonAuth = (() => {
    const accessKey = "salon.accessToken";
    const refreshKey = "salon.refreshToken";

    function clearSession() {
        localStorage.removeItem(accessKey);
        localStorage.removeItem(refreshKey);
    }

    function saveSession(tokens) {
        localStorage.setItem(accessKey, tokens.accessToken);
        localStorage.setItem(refreshKey, tokens.refreshToken);
    }

    async function refreshSession() {
        const refreshToken = localStorage.getItem(refreshKey);
        if (!refreshToken) return false;
        const response = await fetch("/api/auth/refresh", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ refreshToken })
        });
        if (!response.ok) return false;
        saveSession(await response.json());
        return true;
    }

    async function authenticatedFetch(url, options = {}, retry = true) {
        const headers = new Headers(options.headers || {});
        headers.set("Authorization", `Bearer ${localStorage.getItem(accessKey) || ""}`);
        const response = await fetch(url, { ...options, headers });
        if (response.status === 401 && retry && await refreshSession()) {
            return authenticatedFetch(url, options, false);
        }
        return response;
    }

    function redirectToLogin() {
        clearSession();
        location.replace("/admin/login");
    }

    return {
        authenticatedFetch,
        redirectToLogin,
        bindLoginForm() {
            document.getElementById("login-form").addEventListener("submit", async event => {
                event.preventDefault();
                const error = document.getElementById("login-error");
                error.classList.add("d-none");
                const response = await fetch("/api/auth/login", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({
                        email: document.getElementById("email").value,
                        password: document.getElementById("password").value
                    })
                });
                if (!response.ok) {
                    const result = await response.json();
                    error.textContent = result.message || "Email hoặc mật khẩu không chính xác.";
                    error.classList.remove("d-none");
                    return;
                }
                saveSession(await response.json());
                location.replace("/admin");
            });
        },
        async requireSession() {
            const response = await authenticatedFetch("/api/admin/session");
            if (!response.ok) return redirectToLogin();
            document.getElementById("admin-content").classList.remove("d-none");
            document.getElementById("logout").addEventListener("click", async () => {
                const refreshToken = localStorage.getItem(refreshKey);
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
