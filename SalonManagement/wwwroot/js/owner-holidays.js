(() => {
    const form = document.getElementById('holiday-form');
    const start = document.getElementById('holiday-start'), end = document.getElementById('holiday-end');
    const reason = document.getElementById('holiday-reason'), warning = document.getElementById('holiday-warning');
    const confirmation = document.getElementById('holiday-confirm'), save = document.getElementById('holiday-save');
    const feedback = document.getElementById('holiday-feedback'), list = document.getElementById('holiday-list');
    const formatDate = value => new Intl.DateTimeFormat('vi-VN').format(new Date(value.slice(0, 10) + 'T00:00:00'));
    let busy = false;
    function resetWarning() { warning.hidden = true; confirmation.checked = false; save.textContent = 'Kiểm tra và lưu ngày nghỉ'; feedback.textContent = ''; }
    [start, end, reason].forEach(input => input.addEventListener('input', resetWarning));
    start.addEventListener('change', () => { end.min = start.value; if (!end.value || end.value < start.value) end.value = start.value; });
    async function request(url, options) {
        const response = await fetch(url, { credentials: 'same-origin', ...options });
        if (response.redirected || response.status === 401 || response.status === 403) throw new Error('Phiên đăng nhập đã hết hạn hoặc tài khoản không có quyền chủ tiệm. Vui lòng đăng nhập lại.');
        const data = await response.json().catch(() => null);
        if (!data) throw new Error('Không đọc được phản hồi. Vui lòng thử lại.');
        return { response, data };
    }
    async function load() {
        list.textContent = 'Đang tải…';
        try {
            const { response, data } = await request('/api/shop-holidays');
            if (!response.ok) throw new Error(data.message || 'Không tải được ngày nghỉ.');
            list.replaceChildren();
            if (!data.length) { list.textContent = 'Chưa có ngày nghỉ. Salon phục vụ theo giờ hoạt động đã thiết lập.'; return; }
            data.forEach(item => {
                const row = document.createElement('div'); row.className = 'border-bottom py-3';
                const date = document.createElement('strong'); date.textContent = formatDate(item.holidayDate);
                const text = document.createElement('p'); text.className = 'text-muted mb-0 text-break'; text.textContent = item.reason || 'Không có lý do';
                row.append(date, text); list.append(row);
            });
        } catch (error) { list.textContent = error.message; }
    }
    form.addEventListener('submit', async event => {
        event.preventDefault(); if (busy || !form.reportValidity()) return;
        if (end.value < start.value) { feedback.textContent = 'Ngày kết thúc phải từ ngày bắt đầu trở đi.'; return; }
        if ((Date.parse(end.value) - Date.parse(start.value)) / 86400000 > 365) { feedback.textContent = 'Khoảng nghỉ tối đa 366 ngày, bao gồm ngày bắt đầu và kết thúc.'; return; }
        if (!reason.value.trim()) { feedback.textContent = 'Vui lòng nhập lý do nghỉ, không chỉ nhập khoảng trắng.'; reason.focus(); return; }
        if (!warning.hidden && !confirmation.checked) { feedback.textContent = 'Vui lòng xác nhận đã kiểm tra các lịch bị ảnh hưởng.'; return; }
        busy = true; save.disabled = true;
        [start, end, reason, confirmation].forEach(input => input.disabled = true);
        feedback.textContent = 'Đang kiểm tra và lưu…';
        try {
            const { response, data } = await request('/api/shop-holidays/range', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ startDate: start.value, endDate: end.value, reason: reason.value.trim(), confirmAffectedAppointments: !warning.hidden && confirmation.checked })
            });
            if (response.status === 409 && data.affectedAppointments) {
                const affected = document.getElementById('affected-list'); affected.replaceChildren();
                data.affectedAppointments.forEach(item => { const li = document.createElement('li'); li.className = 'text-break'; li.textContent = `${item.bookingReference || '#' + item.appointmentId} · ${formatDate(item.appointmentDate)} · ${item.startTime.slice(0, 5)}–${item.endTime.slice(0, 5)}`; affected.append(li); });
                warning.hidden = false; confirmation.checked = false; save.textContent = 'Xác nhận lưu khoảng nghỉ';
                feedback.textContent = data.message; return;
            }
            if (!response.ok) throw new Error(data.message || 'Không lưu được. Kiểm tra khoảng ngày và lý do nghỉ.');
            form.reset(); resetWarning(); end.removeAttribute('min');
            feedback.textContent = `Đã lưu thành công ${data.holidays.length} ngày nghỉ.`;
            await load();
        } catch (error) { feedback.textContent = error.message; }
        finally { busy = false; save.disabled = false; [start, end, reason, confirmation].forEach(input => input.disabled = false); }
    });
    document.getElementById('holiday-refresh').addEventListener('click', load);
    load();
})();
