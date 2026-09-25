window.BusinessHoursPage = (() => {
    const names = ["Chủ nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy"];
    const list = () => document.getElementById("hours-list");

    function time(value) { return value ? value.substring(0, 5) : ""; }

    function row(day) {
        const disabled = day.isClosed ? "disabled" : "";
        return `<div class="hours-row border rounded p-3 mb-3" data-day="${day.dayOfWeek}">
            <div class="row align-items-start g-3">
                <div class="col-md-3 fw-semibold">${names[day.dayOfWeek]}</div>
                <div class="col-md-2 form-check">
                    <input class="form-check-input closed" type="checkbox" ${day.isClosed ? "checked" : ""}>
                    <label class="form-check-label">Đóng cửa</label>
                </div>
                <div class="col-md-3"><label class="form-label">Giờ mở</label><input class="form-control opens" type="time" value="${time(day.opensAt)}" ${disabled}></div>
                <div class="col-md-3"><label class="form-label">Giờ đóng</label><input class="form-control closes" type="time" value="${time(day.closesAt)}" ${disabled}></div>
            </div><div class="invalid-feedback d-block error"></div>
        </div>`;
    }

    function validate(item) {
        const closed = item.querySelector(".closed").checked;
        const error = item.querySelector(".error");
        error.textContent = "";
        if (closed) return true;
        const opens = item.querySelector(".opens").value;
        const closes = item.querySelector(".closes").value;
        if (!opens || !closes) error.textContent = "Vui lòng nhập đủ giờ mở và giờ đóng.";
        else {
            const minutes = value => Number(value.substring(0, 2)) * 60 + Number(value.substring(3, 5));
            if (minutes(closes) - minutes(opens) < 60) error.textContent = "Giờ đóng phải sau giờ mở ít nhất 60 phút.";
        }
        return !error.textContent;
    }

    return {
        async initialize() {
            const response = await SalonAuth.authenticatedFetch("/api/business-hours");
            if (!response.ok) return SalonAuth.redirectToLogin();
            list().innerHTML = (await response.json()).map(row).join("");
            document.getElementById("hours-page").classList.remove("d-none");
            list().addEventListener("change", event => {
                const item = event.target.closest(".hours-row");
                if (event.target.classList.contains("closed")) {
                    item.querySelectorAll("input[type=time]").forEach(input => input.disabled = event.target.checked);
                }
                validate(item);
            });
            document.getElementById("hours-form").addEventListener("submit", async event => {
                event.preventDefault();
                const rows = [...document.querySelectorAll(".hours-row")];
                if (!rows.every(validate)) return;
                const days = rows.map(item => ({
                    dayOfWeek: Number(item.dataset.day),
                    isClosed: item.querySelector(".closed").checked,
                    opensAt: item.querySelector(".closed").checked ? null : item.querySelector(".opens").value,
                    closesAt: item.querySelector(".closed").checked ? null : item.querySelector(".closes").value
                }));
                const response = await SalonAuth.authenticatedFetch("/api/business-hours", {
                    method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ days })
                });
                const result = document.getElementById("hours-result");
                result.className = `alert ${response.ok ? "alert-success" : "alert-danger"}`;
                result.textContent = response.ok ? "Đã lưu giờ hoạt động." : "Không thể lưu. Vui lòng kiểm tra lại các khung giờ.";
            });
        }
    };
})();
