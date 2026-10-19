window.ReceptionOperations = (() => {
    const refreshMilliseconds = 15000;
    let refreshTimer;
    let loadInProgress = false;
    let reloadRequested = false;
    let selectedCheckout;

    const escapeHtml = value => String(value ?? "").replace(/[&<>'"]/g, character => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;"
    }[character]));
    const money = value => new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND", maximumFractionDigits: 0 }).format(Number(value || 0));
    const time = value => String(value ?? "").slice(0, 5);
    const shortPhone = value => value ? `(${escapeHtml(value)})` : "";
    const formatDate = value => new Intl.DateTimeFormat("vi-VN", { weekday: "long", day: "2-digit", month: "2-digit", year: "numeric" }).format(new Date(`${value}T00:00:00`));

    function showError(message) {
        const error = document.getElementById("reception-operations-error");
        error.textContent = message;
        error.classList.remove("d-none");
    }

    function clearError() {
        document.getElementById("reception-operations-error")?.classList.add("d-none");
    }

    function renderKpis(snapshot) {
        document.querySelector("[data-kpi-total]").textContent = snapshot.totalAppointments;
        document.querySelector("[data-kpi-checkin]").textContent = snapshot.checkedInAppointments;
        document.querySelector("[data-kpi-checkout]").textContent = snapshot.pendingCheckoutCount;
        document.querySelector("[data-kpi-idle]").textContent = `${snapshot.idleStylistCount} / ${snapshot.onShiftStylistCount}`;
        document.querySelectorAll("[data-operations-date]").forEach(element => element.textContent = formatDate(snapshot.generatedAt.slice(0, 10)));
    }

    function renderUpcoming(items) {
        const target = document.getElementById("upcoming-checkins-list");
        if (!items.length) {
            target.innerHTML = '<div class="reception-empty-state"><i class="bi bi-check2-circle"></i> Chưa có khách nào cần check-in trong 60 phút tới.</div>';
            return;
        }
        target.innerHTML = items.map(item => `
            <article class="reception-operation-row">
                <time>${time(item.startTime)}</time>
                <div class="reception-operation-detail"><strong>${escapeHtml(item.customerName)} <small>${shortPhone(item.phone)}</small></strong><span>${escapeHtml(item.services)} · Thợ: ${escapeHtml(item.stylistName)}</span>${item.isLate ? `<em>Đến muộn${item.lateMinutes ? ` +${item.lateMinutes}p` : ""}</em>` : '<em class="confirmed">Đã xác nhận</em>'}</div>
                <button class="btn btn-primary btn-sm" type="button" data-operation-checkin="${item.appointmentId}"><i class="bi bi-box-arrow-in-right"></i> Check-in</button>
            </article>`).join("");
        target.querySelectorAll("[data-operation-checkin]").forEach(button => button.addEventListener("click", () => checkIn(button)));
    }

    function renderCheckout(items) {
        const target = document.getElementById("checkout-queue-list");
        if (!items.length) {
            target.innerHTML = '<div class="reception-empty-state compact"><i class="bi bi-receipt"></i> Chưa có phiếu nào chờ thanh toán.</div>';
            return;
        }
        target.innerHTML = items.map(item => `
            <article class="reception-checkout-row">
                <div><strong>${escapeHtml(item.customerName)}</strong><span>Thợ ${escapeHtml(item.stylistName)} · ${item.status === "Completed" ? "Đã hoàn tất" : "Đang phục vụ"}</span><b>${money(item.totalAmount)}</b></div>
                <button class="btn btn-success btn-sm" type="button" data-operation-payment="${item.appointmentId}" data-customer="${escapeHtml(item.customerName)}" data-stylist="${escapeHtml(item.stylistName)}" data-services="${escapeHtml(item.services)}" data-total="${item.totalAmount}">${item.status === "Completed" ? "Thanh toán" : "Mở hóa đơn"}</button>
            </article>`).join("");
        target.querySelectorAll("[data-operation-payment]").forEach(button => button.addEventListener("click", () => openPayment(button)));
    }

    function renderPending(items) {
        const target = document.getElementById("pending-approvals-list");
        document.querySelectorAll("[data-pending-approval-count]").forEach(element => element.textContent = items.length);
        if (!items.length) {
            target.innerHTML = '<div class="reception-empty-state compact"><i class="bi bi-calendar2-check"></i> Không có lịch online cần duyệt.</div>';
            return;
        }
        const previousState = new Map([...target.querySelectorAll("[data-operation-reject]")].map(button => {
            const row = button.closest(".reception-pending-row");
            return [button.dataset.operationReject, {
                reason: row.querySelector("[data-operation-reason]").value,
                expanded: !row.querySelector(`[data-operation-reject-options="${button.dataset.operationReject}"]`).classList.contains("d-none")
            }];
        }));
        target.innerHTML = items.map(item => `
            <article class="reception-pending-row">
                <div class="reception-pending-content"><strong>${escapeHtml(item.customerName)} <small>${shortPhone(item.customerPhone)}</small></strong><span>${item.appointmentDate.slice(0, 10).split("-").reverse().join("/")} · ${time(item.startTime)} · ${escapeHtml((item.services || []).join(", "))}</span>${item.isOverdue ? '<em class="overdue">Chờ xử lý quá 12 giờ</em>' : '<em>Mới đặt online</em>'}${item.nameConflictResolution === "PendingReceptionReview" ? '<em class="overdue">Cần xử lý tên hồ sơ ở màn lịch</em>' : ""}</div>
                <div class="reception-pending-actions"><button class="btn btn-primary btn-sm" type="button" data-operation-approve="${item.appointmentId}" ${item.nameConflictResolution === "PendingReceptionReview" ? "disabled title=\"Cần xử lý tên hồ sơ trước\"" : ""}>Duyệt</button><button class="btn btn-outline-danger btn-sm" type="button" data-operation-reject-toggle="${item.appointmentId}">Hủy</button></div>
                <div class="reception-reject-options d-none" data-operation-reject-options="${item.appointmentId}"><select class="form-select form-select-sm" data-operation-reason><option value="">Lý do hủy…</option><option>Không còn thợ phù hợp</option><option>Khung giờ không còn trống</option><option>Không liên hệ được khách</option><option>Yêu cầu của khách không phù hợp</option></select><button class="btn btn-danger btn-sm" type="button" data-operation-reject="${item.appointmentId}">Xác nhận hủy</button></div>
            </article>`).join("");
        target.querySelectorAll("[data-operation-reject]").forEach(button => {
            const previous = previousState.get(button.dataset.operationReject);
            if (!previous) return;
            const row = button.closest(".reception-pending-row");
            row.querySelector("[data-operation-reason]").value = previous.reason;
            row.querySelector(`[data-operation-reject-options="${button.dataset.operationReject}"]`).classList.toggle("d-none", !previous.expanded);
        });
        target.querySelectorAll("[data-operation-approve]").forEach(button => button.addEventListener("click", () => approve(button)));
        target.querySelectorAll("[data-operation-reject-toggle]").forEach(button => button.addEventListener("click", () => document.querySelector(`[data-operation-reject-options="${button.dataset.operationRejectToggle}"]`)?.classList.toggle("d-none")));
        target.querySelectorAll("[data-operation-reject]").forEach(button => button.addEventListener("click", () => reject(button)));
    }

    async function checkIn(button) {
        button.disabled = true;
        try {
            const response = await SalonAuth.authenticatedFetch(`/api/reception/operations/appointments/${button.dataset.operationCheckin}/check-in`, { method: "POST" });
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.error || "Không thể check-in khách.");
            await load();
        } catch (error) {
            showError(error.message || "Không thể check-in khách.");
            button.disabled = false;
        }
    }

    async function approve(button) {
        button.disabled = true;
        try {
            const response = await SalonAuth.authenticatedFetch(`/api/reception/pending-appointments/${button.dataset.operationApprove}/confirm`, { method: "POST" });
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.error || "Không thể duyệt lịch hẹn.");
            await load();
        } catch (error) {
            showError(error.message || "Không thể duyệt lịch hẹn.");
            button.disabled = false;
        }
    }

    async function reject(button) {
        const row = button.closest(".reception-pending-row");
        const reason = row.querySelector("[data-operation-reason]").value;
        if (!reason) return showError("Vui lòng chọn lý do trước khi hủy lịch.");
        button.disabled = true;
        try {
            const response = await SalonAuth.authenticatedFetch(`/api/reception/pending-appointments/${button.dataset.operationReject}/reject`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ reason }) });
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.error || "Không thể hủy lịch hẹn.");
            await load();
        } catch (error) {
            showError(error.message || "Không thể hủy lịch hẹn.");
            button.disabled = false;
        }
    }

    function renderPaymentLines() {
        const target = document.getElementById("reception-payment-lines");
        const total = selectedCheckout.lines.reduce((sum, line) => sum + Number(line.price || 0), 0);
        target.innerHTML = selectedCheckout.lines.map((line, index) => `<div class="reception-pos-line"><select class="form-select form-select-sm" data-payment-line="${index}">${selectedCheckout.availableServices.map(service => `<option value="${service.serviceId}" ${service.serviceId === line.serviceId ? "selected" : ""} ${service.serviceId !== line.serviceId && selectedCheckout.lines.some((selected, selectedIndex) => selectedIndex !== index && selected.serviceId === service.serviceId) ? "disabled" : ""}>${escapeHtml(service.serviceName)}</option>`).join("")}</select><strong>${money(line.price)}</strong><button class="btn btn-link text-danger p-0" type="button" data-payment-remove="${index}" aria-label="Bỏ dịch vụ"><i class="bi bi-trash3"></i></button></div>`).join("");
        renderPaymentTotals(total, selectedCheckout.discountAmount || 0);
        document.getElementById("reception-payment-add-service").disabled = selectedCheckout.lines.length >= selectedCheckout.availableServices.length;
        target.querySelectorAll("[data-payment-line]").forEach(select => select.addEventListener("change", () => {
            const service = selectedCheckout.availableServices.find(item => item.serviceId === Number(select.value));
            selectedCheckout.lines[Number(select.dataset.paymentLine)] = service;
            clearPromotion();
            renderPaymentLines();
        }));
        target.querySelectorAll("[data-payment-remove]").forEach(button => button.addEventListener("click", () => {
            selectedCheckout.lines.splice(Number(button.dataset.paymentRemove), 1);
            clearPromotion();
            renderPaymentLines();
        }));
    }

    function renderPaymentTotals(subtotal, discount) {
        const safeDiscount = Math.min(Math.max(Number(discount || 0), 0), subtotal);
        document.getElementById("reception-payment-subtotal").textContent = money(subtotal);
        document.getElementById("reception-payment-discount").textContent = `−${money(safeDiscount)}`;
        document.getElementById("reception-payment-total").textContent = money(subtotal - safeDiscount);
    }

    function clearPromotion() {
        if (!selectedCheckout) return;
        selectedCheckout.promotionCode = null;
        selectedCheckout.discountAmount = 0;
        const message = document.getElementById("reception-promotion-message");
        message.textContent = "";
        message.className = "small mt-2 mb-0 d-none";
    }

    async function applyPromotion() {
        if (!selectedCheckout) return;
        const codeInput = document.getElementById("reception-promotion-code");
        const applyButton = document.getElementById("reception-promotion-apply");
        const message = document.getElementById("reception-promotion-message");
        const code = codeInput.value.trim().toUpperCase();
        const subtotal = selectedCheckout.lines.reduce((sum, line) => sum + Number(line.price || 0), 0);
        selectedCheckout.promotionCode = null;
        selectedCheckout.discountAmount = 0;
        renderPaymentTotals(subtotal, 0);
        if (!code) {
            message.textContent = "Đã bỏ mã giảm giá. Tổng tiền gốc được giữ nguyên.";
            message.className = "small mt-2 mb-0 text-muted";
            return;
        }
        applyButton.disabled = true;
        try {
            const response = await SalonAuth.authenticatedFetch(`/api/reception/operations/appointments/${selectedCheckout.appointmentId}/promotion`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ promotionCode: code, services: selectedCheckout.lines.map(line => ({ serviceId: line.serviceId })) })
            });
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.error || "Không thể áp dụng mã giảm giá.");
            selectedCheckout.promotionCode = result.code;
            selectedCheckout.discountAmount = Number(result.discountAmount || 0);
            renderPaymentTotals(Number(result.subtotal || subtotal), selectedCheckout.discountAmount);
            message.textContent = `✓ Áp dụng thành công: ${result.promotionName} (−${money(selectedCheckout.discountAmount)}).`;
            message.className = "small mt-2 mb-0 text-success";
        } catch (error) {
            message.textContent = error.message || "Mã giảm giá không hợp lệ.";
            message.className = "small mt-2 mb-0 text-danger";
        } finally {
            applyButton.disabled = false;
        }
    }

    function syncPaymentReference() {
        const transfer = document.getElementById("reception-payment-method").value === "Transfer";
        const reference = document.getElementById("reception-payment-reference");
        const label = document.getElementById("reception-payment-reference-label");
        const help = document.getElementById("reception-payment-reference-help");
        reference.required = false;
        reference.readOnly = transfer;
        document.getElementById("reception-payment-reference-wrap").classList.toggle("is-required", false);
        if (transfer && !reference.value) reference.value = createTransferReference();
        label.textContent = transfer ? "Mã giao dịch tự động" : "Mã giao dịch / ghi chú đối soát";
        help.textContent = transfer ? "Mã được tạo tự động để đối soát chuyển khoản." : "Có thể nhập ghi chú đối soát nếu cần.";
    }

    function createTransferReference() {
        const now = new Date();
        const part = value => String(value).padStart(2, "0");
        const timestamp = `${String(now.getFullYear()).slice(-2)}${part(now.getMonth() + 1)}${part(now.getDate())}${part(now.getHours())}${part(now.getMinutes())}${part(now.getSeconds())}`;
        return `FT${timestamp}${String(selectedCheckout?.appointmentId || 0).padStart(5, "0")}`;
    }

    async function openPayment(button) {
        button.disabled = true;
        try {
            const response = await SalonAuth.authenticatedFetch(`/api/reception/operations/appointments/${button.dataset.operationPayment}/checkout`);
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.error || "Không thể nạp phiếu thanh toán.");
            selectedCheckout = { ...result, lines: [...(result.services || [])], availableServices: result.availableServices || [], promotionCode: result.promotionCode || null, discountAmount: Number(result.discountAmount || 0) };
            document.getElementById("reception-payment-summary").innerHTML = `<strong>${escapeHtml(selectedCheckout.customerName)} · ${escapeHtml(selectedCheckout.phone)}</strong><span>Thợ ${escapeHtml(selectedCheckout.stylistName)} · Đã hoàn tất dịch vụ</span>`;
            document.getElementById("reception-payment-reference").value = "";
            document.getElementById("reception-promotion-code").value = selectedCheckout.promotionCode || "";
            const promotionMessage = document.getElementById("reception-promotion-message");
            promotionMessage.textContent = selectedCheckout.promotionCode ? `Mã ${selectedCheckout.promotionCode} đã được lưu trên hóa đơn.` : "";
            promotionMessage.className = selectedCheckout.promotionCode ? "small mt-2 mb-0 text-success" : "small mt-2 mb-0 d-none";
            const message = document.getElementById("reception-payment-message");
            message.className = "small mt-3 mb-0 d-none";
            syncPaymentReference();
            renderPaymentLines();
            document.getElementById("reception-payment-dialog").showModal();
        } catch (error) {
            showError(error.message || "Không thể nạp phiếu thanh toán.");
        } finally {
            button.disabled = false;
        }
    }

    function paymentMethodName(method) {
        return ({ Cash: "Tiền mặt", Transfer: "Chuyển khoản", Card: "Thẻ" })[method] || method;
    }

    function requestPaymentConfirmation() {
        if (!selectedCheckout) return;
        const method = document.getElementById("reception-payment-method").value;
        const transactionReference = document.getElementById("reception-payment-reference").value.trim();
        const message = document.getElementById("reception-payment-message");
        if (!selectedCheckout.lines.length) { message.textContent = "Phiếu thanh toán cần có ít nhất một dịch vụ."; message.className = "small mt-3 mb-0 text-danger"; return; }
        if (method === "Transfer" && !transactionReference) syncPaymentReference();
        const subtotal = selectedCheckout.lines.reduce((sum, line) => sum + Number(line.price || 0), 0);
        const total = subtotal - Number(selectedCheckout.discountAmount || 0);
        document.querySelector("[data-payment-confirm-customer]").textContent = selectedCheckout.customerName || "—";
        document.querySelector("[data-payment-confirm-method]").textContent = paymentMethodName(method);
        document.querySelector("[data-payment-confirm-total]").textContent = money(total);
        document.getElementById("reception-payment-confirm-dialog").showModal();
    }

    async function pay() {
        if (!selectedCheckout) return;
        const submit = document.getElementById("reception-payment-submit");
        const message = document.getElementById("reception-payment-message");
        submit.disabled = true;
        try {
            const method = document.getElementById("reception-payment-method").value;
            const transactionReference = document.getElementById("reception-payment-reference").value.trim();
            if (!selectedCheckout.lines.length) throw new Error("Phiếu thanh toán cần có ít nhất một dịch vụ.");
            const response = await SalonAuth.authenticatedFetch(`/api/reception/operations/appointments/${selectedCheckout.appointmentId}/payment`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ method, transactionReference, promotionCode: selectedCheckout.promotionCode, services: selectedCheckout.lines.map(line => ({ serviceId: line.serviceId })) }) });
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.error || "Không thể ghi nhận thanh toán.");
            message.textContent = result.message || "Đã ghi nhận thanh toán.";
            message.className = "small mt-3 mb-0 text-success";
            window.setTimeout(() => document.getElementById("reception-payment-dialog").close(), 450);
            await load();
        } catch (error) {
            message.textContent = error.message || "Không thể ghi nhận thanh toán.";
            message.className = "small mt-3 mb-0 text-danger";
        } finally {
            submit.disabled = false;
        }
    }

    async function saveInvoice() {
        if (!selectedCheckout) return;
        const save = document.getElementById("reception-invoice-save");
        const message = document.getElementById("reception-payment-message");
        save.disabled = true;
        try {
            if (!selectedCheckout.lines.length) throw new Error("Hóa đơn cần có ít nhất một dịch vụ.");
            const response = await SalonAuth.authenticatedFetch(`/api/reception/operations/appointments/${selectedCheckout.appointmentId}/invoice`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ promotionCode: selectedCheckout.promotionCode, services: selectedCheckout.lines.map(line => ({ serviceId: line.serviceId })) }) });
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.error || "Không thể lưu hóa đơn.");
            selectedCheckout.promotionCode = result.promotionCode || null;
            selectedCheckout.discountAmount = Number(result.discountAmount || 0);
            renderPaymentTotals(Number(result.subtotal || 0), selectedCheckout.discountAmount);
            message.textContent = "Đã lưu hóa đơn nháp. Có thể tiếp tục thêm dịch vụ trước khi thanh toán.";
            message.className = "small mt-3 mb-0 text-success";
            await load();
        } catch (error) {
            message.textContent = error.message || "Không thể lưu hóa đơn.";
            message.className = "small mt-3 mb-0 text-danger";
        } finally {
            save.disabled = false;
        }
    }

    function openCounterBooking() {
        const dialog = document.getElementById("reception-counter-dialog");
        const frame = document.getElementById("reception-counter-frame");
        frame.src = "/reception/counter-appointments?embed=true";
        dialog.showModal();
    }

    function closeCounterBooking() {
        const dialog = document.getElementById("reception-counter-dialog");
        if (!dialog?.open) return;
        dialog.close();
        document.getElementById("reception-counter-frame").src = "about:blank";
    }

    async function load() {
        if (loadInProgress) {
            reloadRequested = true;
            return;
        }
        loadInProgress = true;
        try {
            const [operationsResponse, pendingResponse] = await Promise.all([
                SalonAuth.authenticatedFetch("/api/reception/operations"),
                SalonAuth.authenticatedFetch("/api/reception/pending-appointments")
            ]);
            if (!operationsResponse.ok || !pendingResponse.ok) throw new Error("Không thể tải dữ liệu vận hành. Vui lòng thử lại.");
            const [snapshot, pending] = await Promise.all([operationsResponse.json(), pendingResponse.json()]);
            renderKpis(snapshot);
            renderUpcoming(snapshot.upcomingCheckIns || []);
            renderCheckout(snapshot.pendingCheckouts || []);
            renderPending(Array.isArray(pending) ? pending : []);
            clearError();
        } catch (error) {
            showError(error.message || "Không thể tải dữ liệu vận hành. Vui lòng thử lại.");
        } finally {
            loadInProgress = false;
            if (reloadRequested) {
                reloadRequested = false;
                load();
            }
        }
    }

    return {
        start() {
            clearInterval(refreshTimer);
            document.getElementById("reception-payment-submit")?.addEventListener("click", requestPaymentConfirmation);
            document.getElementById("reception-payment-confirm-submit")?.addEventListener("click", () => {
                document.getElementById("reception-payment-confirm-dialog")?.close();
                pay();
            });
            document.getElementById("reception-invoice-save")?.addEventListener("click", saveInvoice);
            document.getElementById("reception-payment-add-service")?.addEventListener("click", () => {
                const available = selectedCheckout?.availableServices.find(service => !selectedCheckout.lines.some(line => line.serviceId === service.serviceId));
                if (available) { selectedCheckout.lines.push(available); clearPromotion(); renderPaymentLines(); }
            });
            document.getElementById("reception-promotion-apply")?.addEventListener("click", applyPromotion);
            document.getElementById("reception-promotion-code")?.addEventListener("keydown", event => { if (event.key === "Enter") { event.preventDefault(); applyPromotion(); } });
            document.getElementById("reception-payment-method")?.addEventListener("change", syncPaymentReference);
            document.getElementById("reception-counter-booking")?.addEventListener("click", openCounterBooking);
            document.getElementById("reception-counter-close")?.addEventListener("click", closeCounterBooking);
            document.getElementById("reception-counter-dialog")?.addEventListener("click", event => {
                if (event.target === event.currentTarget) closeCounterBooking();
            });
            window.addEventListener("message", event => {
                if (event.origin !== window.location.origin || event.data?.type !== "counter-appointment-created") return;
                closeCounterBooking();
                load();
            });
            load();
            refreshTimer = window.setInterval(load, refreshMilliseconds);
        }
    };
})();
