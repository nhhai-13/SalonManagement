window.ScheduleCoordinationPage = (() => {
    const filter = document.getElementById("schedule-coordination-filter");
    const stylist = document.getElementById("stylist-id");
    const date = document.getElementById("day-off");
    const submit = document.getElementById("load-appointments");
    const error = document.getElementById("schedule-error");
    const loading = document.getElementById("schedule-loading");
    const empty = document.getElementById("schedule-empty");
    const results = document.getElementById("schedule-results");
    const rows = document.getElementById("schedule-appointments");
    const count = document.getElementById("schedule-count");
    const selectedCount = document.getElementById("transfer-selected-count");
    const previewButton = document.getElementById("preview-transfers");
    const previewPanel = document.getElementById("transfer-preview");
    const previewSummary = document.getElementById("transfer-preview-summary");
    const previewList = document.getElementById("transfer-preview-list");
    const confirmButton = document.getElementById("confirm-transfers");
    const resultsPanel = document.getElementById("transfer-results");
    const resultsSummary = document.getElementById("transfer-results-summary");
    const resultsList = document.getElementById("transfer-results-list");
    const statusLabels = {
        Pending: "Chờ xác nhận",
        Confirmed: "Đã xác nhận",
        Arrived: "Khách đã đến"
    };
    let selectionControls = [];
    let transferBusy = false;
    let selectionSequence = 0;

    const textCell = value => {
        const cell = document.createElement("td");
        cell.textContent = value || "—";
        return cell;
    };

    function setBusy(isBusy) {
        submit.disabled = isBusy || !stylist.value || !date.value;
        loading.classList.toggle("d-none", !isBusy);
    }

    function clearPreview() {
        previewPanel.classList.add("d-none");
        previewList.replaceChildren();
        confirmButton.disabled = true;
    }

    function updateSelectionState(clearExistingPreview = true) {
        const selected = selectionControls.filter(control => control.checkbox.checked);
        selectedCount.textContent = selected.length
            ? `Đã chọn ${selected.length} lịch`
            : "Chưa chọn lịch";
        previewButton.disabled = transferBusy || selected.length === 0;
        if (clearExistingPreview) clearPreview();
    }

    function minuteOfDay(value) {
        const parts = value.slice(0, 5).split(":").map(Number);
        return parts[0] * 60 + parts[1];
    }

    function renderChoice(appointment) {
        const cell = document.createElement("td");
        const select = document.createElement("select");
        select.className = "form-select form-select-sm";
        select.add(new Option("Chọn thợ và giờ", ""));
        const choices = [];
        for (const option of appointment.plan?.stylists || []) {
            const times = option.availableAtOriginalTime
                ? [appointment.startTime]
                : option.suggestedStartTimes || [];
            for (const time of times) {
                const shownTime = time.slice(0, 5);
                const sameTime = shownTime === appointment.startTime.slice(0, 5);
                const value = `${option.stylistId}|${minuteOfDay(shownTime)}`;
                const label = `${option.stylistName} · ${shownTime}${sameTime ? " (giờ ban đầu)" : " (dời giờ)"}`;
                select.add(new Option(label, value));
                choices.push({ value, label });
            }
        }
        select.disabled = choices.length === 0;
        select.addEventListener("change", updateSelectionState);
        cell.append(select);
        return { cell, select, choices };
    }

    function createRow(appointment) {
        const row = document.createElement("tr");
        const selectionCell = document.createElement("td");
        const checkbox = document.createElement("input");
        checkbox.type = "checkbox";
        checkbox.className = "form-check-input";
        checkbox.disabled = !appointment.plan?.hasOptions;
        checkbox.setAttribute?.("aria-label", `Chọn lịch của ${appointment.customerName}`);
        selectionCell.append(checkbox);

        const choice = renderChoice(appointment);
        const consentCell = document.createElement("td");
        const consent = document.createElement("input");
        consent.type = "checkbox";
        consent.className = "form-check-input customer-confirmation";
        consent.disabled = choice.choices.length === 0;
        consent.setAttribute?.("aria-label", `Khách đồng ý lịch của ${appointment.customerName}`);
        consentCell.append(consent);

        const outcomeCell = document.createElement("td");
        outcomeCell.textContent = "Chưa xác nhận";
        const control = {
            appointmentId: appointment.id,
            checkbox,
            choice: choice.select,
            consent,
            outcomeCell,
            selectionOrder: null
        };
        checkbox.addEventListener("change", () => {
            control.selectionOrder = checkbox.checked ? ++selectionSequence : null;
            updateSelectionState();
        });
        consent.addEventListener("change", updateSelectionState);
        selectionControls.push(control);

        row.append(
            selectionCell,
            textCell(appointment.customerName),
            textCell(appointment.services.join(", ") || "Chưa có dịch vụ"),
            textCell(appointment.startTime.slice(0, 5)),
            textCell(`${appointment.durationMinutes} phút`),
            textCell(statusLabels[appointment.status] || appointment.status),
            textCell([appointment.customerPhone, appointment.customerEmail].filter(Boolean).join(" · ")),
            renderPlan(appointment.plan),
            choice.cell,
            consentCell,
            outcomeCell
        );
        return row;
    }

    function renderPlan(plan) {
        const cell = document.createElement("td");
        if (!plan?.hasOptions) {
            const none = document.createElement("div");
            none.className = "text-danger fw-semibold mb-2";
            none.textContent = "Chưa tìm được thợ hoặc khung giờ phù hợp.";
            cell.append(none);
        }

        const list = document.createElement("ul");
        list.className = "list-unstyled mb-0";
        for (const option of plan?.stylists || []) {
            const item = document.createElement("li");
            item.className = "mb-2";
            const title = document.createElement("strong");
            title.textContent = option.stylistName;
            item.append(title);

            if (option.availableAtOriginalTime) {
                const fit = document.createElement("span");
                fit.className = "badge text-bg-success ms-2";
                fit.textContent = "Rảnh đúng giờ";
                item.append(fit);
            } else if (option.suggestedStartTimes?.length) {
                const times = document.createElement("div");
                times.textContent = `Có thể nhận lúc: ${option.suggestedStartTimes.join(", ")}`;
                item.append(times);
                appendReasons(item, option.reasonsUnavailableAtOriginalTime);
            } else {
                appendReasons(item, option.reasonsUnavailableAtOriginalTime);
            }

            list.append(item);
        }
        cell.append(list);
        return cell;
    }

    function appendReasons(parent, reasons) {
        if (!reasons?.length) return;
        const reason = document.createElement("small");
        reason.className = "d-block text-muted";
        reason.textContent = `Không phù hợp giờ ban đầu: ${reasons.join(" ")}`;
        parent.append(reason);
    }

    function renderAppointments(payload) {
        rows.replaceChildren();
        selectionControls = [];
        selectionSequence = 0;
        count.textContent = `${payload.totalCount} lịch cần xử lý`;
        selectedCount.textContent = "Chưa chọn lịch";
        previewButton.disabled = true;
        clearPreview();
        resultsPanel.classList.add("d-none");
        if (payload.totalCount === 0) {
            results.classList.add("d-none");
            empty.classList.remove("d-none");
            return;
        }

        empty.classList.add("d-none");
        results.classList.remove("d-none");
        for (const appointment of payload.appointments) {
            rows.append(createRow(appointment));
        }
    }

    function getSelectedItems() {
        const chosen = selectionControls
            .filter(control => control.checkbox.checked)
            .sort((left, right) => (left.selectionOrder ?? 0) - (right.selectionOrder ?? 0));
        if (chosen.length === 0) {
            throw new Error("Hãy chọn ít nhất một lịch cần chuyển.");
        }

        return chosen.map(control => {
            if (!control.choice.value) {
                throw new Error("Hãy chọn thợ và giờ mới cho từng lịch đã đánh dấu.");
            }
            const [stylistId, startMinuteOfDay] = control.choice.value.split("|").map(Number);
            return {
                appointmentId: control.appointmentId,
                stylistId,
                startMinuteOfDay,
                customerConfirmed: control.consent.checked
            };
        });
    }

    async function postTransfer(path, items) {
        const response = await SalonAuth.authenticatedFetch(path, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ items })
        });
        const payload = await response.json().catch(() => ({}));
        if (response.status === 401) return SalonAuth.redirectToLogin("Vui lòng đăng nhập lại để chuyển lịch.");
        if (!response.ok) throw new Error(payload.message || "Không thể xử lý lô lịch đã chọn.");
        return payload;
    }

    function renderPreview(payload, items) {
        previewList.replaceChildren();
        previewSummary.textContent = `${payload.successCount} phương án có thể chuyển; ${payload.failureCount} lịch cần xử lý hoặc đang xung đột.`;
        for (let index = 0; index < payload.results.length; index++) {
            const result = payload.results[index];
            const selection = items[index];
            const control = selectionControls.find(item => item.appointmentId === result.appointmentId);
            const targetLabel = control?.choice.options?.[control.choice.selectedIndex]?.text || "Phương án đã chọn";
            const line = document.createElement("li");
            line.textContent = `Lịch #${result.appointmentId}: ${targetLabel} — ${result.message}`;
            line.className = result.outcome === "Ready" || result.outcome === "AlreadyApplied"
                ? "text-success"
                : "text-danger";
            previewList.append(line);
        }
        confirmButton.disabled = payload.successCount === 0;
        previewPanel.classList.remove("d-none");
    }

    function renderTransferResults(payload) {
        resultsList.replaceChildren();
        resultsSummary.textContent = `${payload.successCount} lịch thành công; ${payload.failureCount} lịch thất bại hoặc cần xử lý lại.`;
        for (const result of payload.results) {
            const line = document.createElement("li");
            const appliedPlan = result.stylistName && result.startTime
                ? `Thợ: ${result.stylistName}, giờ: ${result.startTime}${result.appointmentStatus ? `, trạng thái: ${result.appointmentStatus}` : ""}. `
                : "";
            line.textContent = `Lịch #${result.appointmentId}: ${appliedPlan}${result.message}`;
            line.className = result.outcome === "Succeeded" || result.outcome === "AlreadyApplied"
                ? "text-success"
                : "text-danger";
            resultsList.append(line);

            const control = selectionControls.find(item => item.appointmentId === result.appointmentId);
            if (control) {
                control.outcomeCell.textContent = `${appliedPlan}${result.message}`;
                if (result.outcome === "Succeeded" || result.outcome === "AlreadyApplied") {
                    control.checkbox.checked = false;
                    control.checkbox.disabled = true;
                    control.choice.disabled = true;
                    control.consent.disabled = true;
                }
            }
        }
        resultsPanel.classList.remove("d-none");
        updateSelectionState();
    }

    async function previewTransfers() {
        error.classList.add("d-none");
        clearPreview();
        let items;
        try {
            items = getSelectedItems();
        } catch (selectionError) {
            error.textContent = selectionError.message;
            error.classList.remove("d-none");
            return;
        }

        transferBusy = true;
        previewButton.disabled = true;
        try {
            const payload = await postTransfer("/api/schedule-coordination/transfers/preview", items);
            if (!payload) return;
            renderPreview(payload, items);
        } catch (previewError) {
            error.textContent = previewError.message || "Không thể kiểm tra phương án đã chọn.";
            error.classList.remove("d-none");
        } finally {
            transferBusy = false;
            updateSelectionState(false);
        }
    }

    async function confirmTransfers() {
        error.classList.add("d-none");
        let items;
        try {
            items = getSelectedItems();
        } catch (selectionError) {
            error.textContent = selectionError.message;
            error.classList.remove("d-none");
            return;
        }

        transferBusy = true;
        confirmButton.disabled = true;
        previewButton.disabled = true;
        try {
            const payload = await postTransfer("/api/schedule-coordination/transfers", items);
            if (!payload) return;
            clearPreview();
            renderTransferResults(payload);
        } catch (transferError) {
            error.textContent = transferError.message || "Không thể chuyển lịch đã chọn.";
            error.classList.remove("d-none");
        } finally {
            transferBusy = false;
            updateSelectionState(false);
        }
    }

    async function loadAppointments(event) {
        event?.preventDefault();
        error.classList.add("d-none");
        empty.classList.add("d-none");
        results.classList.add("d-none");
        if (!filter.reportValidity()) return;

        setBusy(true);
        try {
            const query = new URLSearchParams({ stylistId: stylist.value, date: date.value });
            const response = await SalonAuth.authenticatedFetch(`/api/schedule-coordination/appointments?${query}`);
            const payload = await response.json().catch(() => ({}));
            if (response.status === 401) return SalonAuth.redirectToLogin("Vui lòng đăng nhập lại để xem lịch.");
            if (!response.ok) throw new Error(payload.message || "Không thể tải lịch hẹn. Vui lòng thử lại.");
            const syncResponse = await SalonAuth.authenticatedFetch("/api/schedule-coordination/follow-ups/sync", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ stylistId: Number(stylist.value), date: date.value })
            });
            if (syncResponse.status === 401) return SalonAuth.redirectToLogin("Vui lòng đăng nhập lại để cập nhật danh sách cần gọi khách.");
            if (!syncResponse.ok) {
                const syncPayload = await syncResponse.json().catch(() => ({}));
                throw new Error(syncPayload.message || "Không thể cập nhật danh sách khách cần liên hệ.");
            }
            renderAppointments(payload);
        } catch (loadError) {
            error.textContent = loadError.message || "Không thể tải lịch hẹn. Vui lòng thử lại.";
            error.classList.remove("d-none");
        } finally {
            setBusy(false);
        }
    }

    async function initialize() {
        date.value = new Date().toLocaleDateString("sv-SE", { timeZone: "Asia/Ho_Chi_Minh" });
        filter.addEventListener("submit", loadAppointments);
        previewButton.addEventListener("click", previewTransfers);
        confirmButton.addEventListener("click", confirmTransfers);
        document.getElementById("cancel-transfer-preview").addEventListener("click", clearPreview);
        stylist.addEventListener("change", () => { submit.disabled = !stylist.value || !date.value; });
        date.addEventListener("change", () => { submit.disabled = !stylist.value || !date.value; });

        try {
            const response = await SalonAuth.authenticatedFetch("/api/schedule-coordination/stylists");
            const payload = await response.json().catch(() => []);
            if (response.status === 401) return SalonAuth.redirectToLogin("Vui lòng đăng nhập bằng tài khoản lễ tân.");
            if (!response.ok) throw new Error("Không thể tải danh sách thợ.");

            stylist.replaceChildren(new Option("Chọn thợ", ""));
            for (const item of payload) stylist.add(new Option(item.name, item.id));
            stylist.disabled = payload.length === 0;
            submit.disabled = payload.length === 0;
            if (payload.length === 0) {
                document.getElementById("schedule-coordination-page").classList.remove("d-none");
                error.textContent = "Chưa có thợ đang hoạt động để chọn.";
                error.classList.remove("d-none");
                return;
            }

            document.getElementById("schedule-coordination-page").classList.remove("d-none");
            document.getElementById("logout").addEventListener("click", async () => {
                await fetch("/api/auth/logout", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ refreshToken: localStorage.getItem("salon.refreshToken") || sessionStorage.getItem("salon.refreshToken") })
                }).catch(() => {});
                SalonAuth.redirectToLogin();
            });
        } catch (loadError) {
            error.textContent = loadError.message || "Không thể tải danh sách thợ. Vui lòng tải lại trang.";
            error.classList.remove("d-none");
            document.getElementById("schedule-coordination-page").classList.remove("d-none");
        }
    }

    return { initialize };
})();
