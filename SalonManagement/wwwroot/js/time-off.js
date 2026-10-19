window.TimeOffPage = (() => {
    const time = value => value ? value.substring(0, 5) : "";
    const apiTime = value => value && value.length === 5 ? `${value}:00` : value;
    const formatDate = value => {
        if (!value) return "";
        const [year, month, day] = value.substring(0, 10).split("-");
        return year && month && day ? `${day}/${month}/${year}` : value;
    };
    const escapeHtml = value => String(value ?? "").replace(/[&<>'\"]/g, character => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", "\"": "&quot;" })[character]);
    const message = (text, success) => {
        const target = document.getElementById("time-off-result");
        target.textContent = text;
        target.className = `alert ${success ? "alert-success" : "alert-danger"}`;
    };
    const fetchJson = async (url, options) => {
        try {
            const response = await SalonAuth.authenticatedFetch(url, options);
            return { response, result: await response.json().catch(() => ({})) };
        } catch {
            return {
                response: { ok: false },
                result: { message: "Không thể kết nối đến máy chủ. Vui lòng thử lại." }
            };
        }
    };
    const errorMessage = result => {
        if (!result.affectedAppointments?.length) return result.message || "Không thể lưu ngày nghỉ.";
        const appointments = result.affectedAppointments
            .map(item => `#${item.appointmentId} (${formatDate(item.appointmentDate)}, ${time(item.startTime)})`)
            .join(", ");
        return `${result.message} Lịch hẹn bị ảnh hưởng: ${appointments}.`;
    };
    async function loadShopHolidays() {
        const { response, result } = await fetchJson("/api/shop-holidays");
        if (!response.ok) return message(result.message || "Không thể tải ngày nghỉ.", false);
        const list = document.getElementById("shop-holiday-list");
        list.innerHTML = result.length
            ? result.map(item => `<li class="list-group-item d-flex justify-content-between align-items-center"><span><strong>${formatDate(item.holidayDate)}</strong>${item.reason ? ` · ${escapeHtml(item.reason)}` : ""}</span><button class="btn btn-sm btn-outline-danger" data-delete-shop="${item.shopHolidayId}">Xóa</button></li>`).join("")
            : '<li class="list-group-item text-muted">Chưa có ngày nghỉ.</li>';
    }
    async function loadStylistTimeOffs(stylistId) {
        const list = document.getElementById("stylist-time-off-list");
        if (!stylistId) {
            list.innerHTML = '<li class="list-group-item text-muted">Chọn thợ để xem lịch nghỉ.</li>';
            return;
        }
        const { response, result } = await fetchJson(`/api/stylist-time-offs/${stylistId}`);
        if (!response.ok) return message(result.message || "Không thể tải lịch nghỉ của thợ.", false);
        list.innerHTML = result.length
            ? result.map(item => `<li class="list-group-item d-flex justify-content-between align-items-center"><span><strong>${formatDate(item.offDate)}</strong> · ${item.isFullDay ? "Cả ngày" : `${time(item.startTime)}–${time(item.endTime)}`}${item.reason ? ` · ${escapeHtml(item.reason)}` : ""}</span><button class="btn btn-sm btn-outline-danger" data-delete-stylist="${item.stylistTimeOffId}">Xóa</button></li>`).join("")
            : '<li class="list-group-item text-muted">Chưa có lịch nghỉ.</li>';
    }
    return {
        initialize() {
            const shopForm = document.getElementById("shop-holiday-form");
            const stylistForm = document.getElementById("stylist-time-off-form");
            const stylistSelect = stylistForm.elements.stylistId;
            const fullDay = stylistForm.elements.isFullDay;
            loadShopHolidays();
            fullDay.addEventListener("change", () => [stylistForm.elements.startTime, stylistForm.elements.endTime].forEach(input => {
                input.disabled = fullDay.checked;
                input.required = !fullDay.checked;
            }));
            stylistSelect.addEventListener("change", () => loadStylistTimeOffs(stylistSelect.value));
            shopForm.addEventListener("submit", async event => {
                event.preventDefault();
                const button = event.submitter || shopForm.querySelector('[type="submit"]');
                const values = Object.fromEntries(new FormData(shopForm));
                if (button) button.disabled = true;
                try {
                    const { response, result } = await fetchJson("/api/shop-holidays", {
                        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(values)
                    });
                    if (!response.ok) return message(errorMessage(result), false);
                    message("Đã thêm ngày nghỉ của tiệm.", true);
                    shopForm.reset();
                    await loadShopHolidays();
                } finally {
                    if (button) button.disabled = false;
                }
            });
            stylistForm.addEventListener("submit", async event => {
                event.preventDefault();
                const button = event.submitter || stylistForm.querySelector('[type="submit"]');
                const values = Object.fromEntries(new FormData(stylistForm));
                const payload = {
                    stylistId: Number(values.stylistId), offDate: values.offDate, isFullDay: fullDay.checked,
                    startTime: fullDay.checked ? null : apiTime(values.startTime), endTime: fullDay.checked ? null : apiTime(values.endTime),
                    reason: values.reason || null,
                    replacementStylistId: values.replacementStylistId ? Number(values.replacementStylistId) : null
                };
                if (button) button.disabled = true;
                try {
                    const { response, result } = await fetchJson("/api/stylist-time-offs", {
                        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload)
                    });
                    if (!response.ok) return message(errorMessage(result), false);
                    message("Đã thêm lịch nghỉ của thợ.", true);
                    await loadStylistTimeOffs(payload.stylistId);
                } finally {
                    if (button) button.disabled = false;
                }
            });
            document.addEventListener("click", async event => {
                const target = event.target instanceof Element ? event.target : null;
                const shopButton = target?.closest("[data-delete-shop]");
                const stylistButton = target?.closest("[data-delete-stylist]");
                const shopId = shopButton?.dataset.deleteShop;
                const stylistId = stylistButton?.dataset.deleteStylist;
                if (shopId && confirm("Xóa ngày nghỉ này?")) {
                    shopButton.disabled = true;
                    try {
                        const { response, result } = await fetchJson(`/api/shop-holidays/${shopId}`, { method: "DELETE" });
                        if (!response.ok) return message(result.message || "Không thể xóa ngày nghỉ.", false);
                        message("Đã xóa ngày nghỉ.", true);
                        await loadShopHolidays();
                    } finally {
                        if (document.body.contains(shopButton)) shopButton.disabled = false;
                    }
                }
                if (stylistId && confirm("Xóa lịch nghỉ này?")) {
                    stylistButton.disabled = true;
                    try {
                        const { response, result } = await fetchJson(`/api/stylist-time-offs/${stylistId}`, { method: "DELETE" });
                        if (!response.ok) return message(result.message || "Không thể xóa lịch nghỉ.", false);
                        message("Đã xóa lịch nghỉ.", true);
                        await loadStylistTimeOffs(stylistSelect.value);
                    } finally {
                        if (document.body.contains(stylistButton)) stylistButton.disabled = false;
                    }
                }
            });
        }
    };
})();
