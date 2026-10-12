window.ReceptionCustomerSearchPage = (() => {
    const byId = id => document.getElementById(id);
    let debounceTimer;
    let requestSequence = 0;

    async function initialize() {
        const page = byId("customer-search-page");
        page.classList.remove("d-none");
        byId("customer-search-form").addEventListener("submit", event => {
            event.preventDefault();
            clearTimeout(debounceTimer);
            return search();
        });
        byId("customer-search-query").addEventListener("input", () => {
            clearTimeout(debounceTimer);
            const query = byId("customer-search-query").value.trim();
            if (query.length < 2) {
                requestSequence++;
                clearResults();
                setStatus("Nhập ít nhất 2 ký tự để tìm.");
                return;
            }
            debounceTimer = setTimeout(search, 250);
        });
        byId("customer-search-logout").addEventListener("click", logout);
    }

    async function search() {
        const query = byId("customer-search-query").value.trim();
        const sequence = ++requestSequence;
        if (query.length < 2) {
            clearResults();
            setStatus("Nhập ít nhất 2 ký tự để tìm.");
            return;
        }
        setStatus("Đang tìm hồ sơ...");
        try {
            const response = await SalonAuth.authenticatedFetch(`/api/reception/customers?query=${encodeURIComponent(query)}`);
            if (sequence !== requestSequence) return;
            if (response.status === 401) return SalonAuth.redirectToLogin("Vui lòng đăng nhập bằng tài khoản lễ tân.");
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.message || "Không thể tìm hồ sơ khách.");
            render(result);
        } catch (error) {
            if (sequence !== requestSequence) return;
            clearResults();
            setStatus(error.message || "Không thể tìm hồ sơ khách.", "text-danger");
        }
    }

    function render(response) {
        const body = byId("customer-search-results");
        body.replaceChildren();
        for (const customer of response.results || []) {
            const row = document.createElement("tr");
            addCell(row, customer.fullName);
            addCell(row, customer.phone);
            addCell(row, customer.email || "—");
            addCell(row, String(customer.visitCount || 0));
            const actionCell = document.createElement("td");
            const link = document.createElement("a");
            link.className = "btn btn-sm btn-outline-primary";
            link.href = `/reception/customers/${Number(customer.customerId)}`;
            link.textContent = "Mở hồ sơ";
            actionCell.append(link);
            row.append(actionCell);
            body.append(row);
        }
        const table = byId("customer-search-table");
        if (!response.results?.length) {
            table.classList.add("d-none");
            setStatus("Không tìm thấy hồ sơ phù hợp.");
            return;
        }
        table.classList.remove("d-none");
        const suffix = response.hasMore ? " Hiển thị 25 hồ sơ đầu tiên; hãy nhập thêm ký tự để thu hẹp." : "";
        setStatus(`Tìm thấy ${response.totalCount} hồ sơ.${suffix}`);
    }

    function addCell(row, value) {
        const cell = document.createElement("td");
        cell.textContent = value || "—";
        row.append(cell);
    }

    function clearResults() {
        byId("customer-search-results").replaceChildren();
        byId("customer-search-table").classList.add("d-none");
    }

    function setStatus(message, className = "text-muted") {
        const status = byId("customer-search-status");
        status.textContent = message;
        status.className = `mt-3 small ${className}`;
    }

    async function logout() {
        await fetch("/api/auth/logout", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ refreshToken: localStorage.getItem("salon.refreshToken") || sessionStorage.getItem("salon.refreshToken") })
        }).catch(() => {});
        SalonAuth.redirectToLogin();
    }

    return { initialize, search, render };
})();
