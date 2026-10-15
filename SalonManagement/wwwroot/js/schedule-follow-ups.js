window.ScheduleFollowUpsPage = (() => {
    const page = document.getElementById("schedule-follow-ups-page");
    const filter = document.getElementById("follow-ups-filter");
    const date = document.getElementById("follow-ups-date");
    const status = document.getElementById("follow-ups-status");
    const category = document.getElementById("follow-ups-category");
    const error = document.getElementById("follow-ups-error");
    const loading = document.getElementById("follow-ups-loading");
    const empty = document.getElementById("follow-ups-empty");
    const results = document.getElementById("follow-ups-results");
    const rows = document.getElementById("follow-ups-rows");
    const count = document.getElementById("follow-ups-count");
    let busy = false;

    function cell(text) {
        const element = document.createElement("td");
        element.textContent = text || "—";
        return element;
    }

    async function load() {
        if (busy) return;
        busy = true;
        error.classList.add("d-none");
        loading.classList.remove("d-none");
        empty.classList.add("d-none");
        results.classList.add("d-none");
        try {
            const query = new URLSearchParams();
            if (date.value) query.set("date", date.value);
            if (status.value) query.set("contactStatus", status.value);
            if (category.value) query.set("category", category.value);
            const response = await SalonAuth.authenticatedFetch(`/api/schedule-coordination/follow-ups?${query}`);
            const payload = await response.json().catch(() => ({}));
            if (response.status === 401) return SalonAuth.redirectToLogin("Vui lòng đăng nhập bằng tài khoản lễ tân.");
            if (!response.ok) throw new Error(payload.message || "Không thể tải danh sách khách cần liên hệ.");
            rows.replaceChildren();
            count.textContent = `${payload.totalCount} lịch`;
            if (!payload.totalCount) {
                empty.classList.remove("d-none");
                return;
            }

            for (const item of payload.items) rows.append(renderItem(item));
            results.classList.remove("d-none");
        } catch (loadError) {
            error.textContent = loadError.message || "Không thể tải danh sách khách cần liên hệ.";
            error.classList.remove("d-none");
        } finally {
            busy = false;
            loading.classList.add("d-none");
        }
    }

    function renderItem(item) {
        const row = document.createElement("tr");
        row.append(cell(item.customerName));
        const phone = document.createElement("td");
        const phoneLink = document.createElement("a");
        phoneLink.href = `tel:${item.customerPhone}`;
        phoneLink.textContent = item.customerPhone || "—";
        phone.append(phoneLink);
        row.append(phone, cell((item.services || []).join(", ")),
            cell(`${item.originalDate} · ${item.originalStartTime}`));
        const reason = document.createElement("td");
        const label = document.createElement("strong");
        label.textContent = item.categoryLabel;
        const detail = document.createElement("div");
        detail.className = "small text-muted";
        detail.textContent = item.reason;
        reason.append(label, detail);
        row.append(reason);

        const contactCell = document.createElement("td");
        const contacted = document.createElement("input");
        contacted.type = "checkbox";
        contacted.className = "form-check-input";
        contacted.checked = item.contactStatus === "Contacted";
        contacted.setAttribute("aria-label", `Đã liên hệ ${item.customerName}`);
        contactCell.append(contacted);
        row.append(contactCell);

        const noteCell = document.createElement("td");
        const note = document.createElement("textarea");
        note.className = "form-control form-control-sm";
        note.rows = 2;
        note.maxLength = 1000;
        note.placeholder = "Ghi chú kết quả liên hệ";
        note.value = item.contactNote || "";
        noteCell.append(note);
        row.append(noteCell);

        const actionCell = document.createElement("td");
        const save = document.createElement("button");
        save.type = "button";
        save.className = "btn btn-sm btn-primary";
        save.textContent = "Lưu";
        save.addEventListener("click", async () => {
            save.disabled = true;
            try {
                const response = await SalonAuth.authenticatedFetch(`/api/schedule-coordination/follow-ups/${item.id}/contact`, {
                    method: "PUT",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({
                        contactStatus: contacted.checked ? "Contacted" : "Pending",
                        contactNote: note.value
                    })
                });
                const payload = await response.json().catch(() => ({}));
                if (response.status === 401) return SalonAuth.redirectToLogin("Vui lòng đăng nhập lại.");
                if (!response.ok) throw new Error(payload.message || "Không thể lưu kết quả liên hệ.");
                await load();
            } catch (saveError) {
                error.textContent = saveError.message || "Không thể lưu kết quả liên hệ.";
                error.classList.remove("d-none");
            } finally {
                save.disabled = false;
            }
        });
        actionCell.append(save);
        row.append(actionCell);
        return row;
    }

    function initialize() {
        page.classList.remove("d-none");
        date.addEventListener("change", load);
        status.addEventListener("change", load);
        category.addEventListener("change", load);
        filter.addEventListener("submit", event => event.preventDefault());
        document.getElementById("follow-ups-logout").addEventListener("click", async () => {
            await fetch("/api/auth/logout", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ refreshToken: localStorage.getItem("salon.refreshToken") || sessionStorage.getItem("salon.refreshToken") })
            }).catch(() => {});
            SalonAuth.redirectToLogin();
        });
        load();
    }

    return { initialize };
})();
