document.addEventListener('DOMContentLoaded', () => {
    const panel = document.getElementById('stylist-selection');
    if (!panel) return;
    const options = document.getElementById('stylist-options');
    const slots = document.getElementById('slot-options');
    const message = document.getElementById('stylist-message');
    const slotMessage = document.getElementById('slot-message');
    const date = document.getElementById('booking-date');
    const selected = document.getElementById('selected-stylist');
    const selectedSlot = document.getElementById('selected-slot');
    const confirm = document.getElementById('confirm-stylist-selection');
    const confirmation = document.getElementById('assignment-confirmation');
    const assignmentMessage = document.getElementById('assignment-message');
    const assignmentDetail = document.getElementById('assignment-detail');
    const customerDetails = document.getElementById('booking-customer-details');
    const submitAppointment = document.getElementById('submit-appointment');
    const bookingResult = document.getElementById('booking-result');
    let confirmedStylistId = null, submitting = false;
    let assignmentVersion = 0, assignmentRequest;
    function clearAssignment() {
        assignmentVersion++;
        assignmentRequest?.abort();
        confirmation.hidden = true;
        customerDetails.hidden = true;
        confirmedStylistId = null;
        bookingResult.textContent = '';
        bookingResult.classList.remove('text-success');
        document.getElementById('booking-confirmed-services').hidden = true;
        assignmentMessage.textContent = assignmentDetail.textContent = '';
        confirm.disabled = true;
    }
    const parts = new Intl.DateTimeFormat('en', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date());
    const part = type => parts.find(p => p.type === type).value;
    date.value = date.min = `${part('year')}-${part('month')}-${part('day')}`;
    let version = 0;
    let slotVersion = 0;
    let stylistRequest, slotRequest;
    const ids = () => [...document.querySelectorAll('.service-checkbox:checked')].map(e => e.value);
    const query = () => { const q = new URLSearchParams(); ids().forEach(id => q.append('serviceIds', id)); return q; };
    function clearSlots() {
        clearAssignment();
        slotVersion++;
        slotRequest?.abort();
        selectedSlot.value = '';
        selectedSlot.disabled = true;
        slots.replaceChildren();
        slotMessage.textContent = '';
    }
    async function json(url, signal) {
        const response = await fetch(url, { signal, cache: 'no-store' });
        const text = await response.text();
        let data;
        try { data = JSON.parse(text); }
        catch { throw new Error('Máy chủ không thể tải dữ liệu đặt lịch. Vui lòng thử lại.'); }
        if (!response.ok) throw new Error(data.message || 'Không thể tải dữ liệu. Vui lòng thử lại.');
        return data;
    }
    function button(text) {
        const b = document.createElement('button');
        b.type = 'button'; b.className = 'btn btn-outline-primary';
        b.style.cssText = 'white-space:normal;overflow-wrap:anywhere;max-width:100%;min-height:44px';
        b.textContent = text; b.setAttribute('aria-pressed', 'false');
        return b;
    }
    function choose(container, target) {
        container.querySelectorAll('button').forEach(b => {
            b.setAttribute('aria-pressed', String(b === target));
            b.classList.toggle('active', b === target);
        });
    }
    async function validateTime() {
        clearAssignment();
        if (!selected.value || !date.value || !selectedSlot.value || !date.validity.valid || !selectedSlot.validity.valid) {
            slotMessage.textContent = 'Chọn thợ, ngày và nhập giờ theo dạng HH:mm, ví dụ 14:30.';
            return;
        }
        const current = assignmentVersion;
        assignmentRequest = new AbortController();
        slotMessage.textContent = 'Đang kiểm tra giờ hẹn…';
        const q = query(); q.set('stylistId', selected.value); q.set('date', date.value); q.set('start', selectedSlot.value);
        try {
            const data = await json(`${panel.dataset.assignmentUrl}?${q}`, assignmentRequest.signal);
            if (current !== assignmentVersion) return;
            slotMessage.textContent = `Giờ hợp lệ: ${data.start} – ${data.end}. Thợ phù hợp: ${data.name}.`;
            confirm.disabled = false;
        } catch (e) {
            if (current === assignmentVersion && e.name !== 'AbortError') slotMessage.textContent = e.message;
        }
    }
    async function loadSlots() {
        clearSlots();
        const suggestedDates = document.getElementById('suggested-dates');
        const dateMessage = document.getElementById('date-message');
        suggestedDates.replaceChildren(); dateMessage.textContent = '';
        selectedSlot.disabled = !selected.value || !date.value || !date.validity.valid;
        slotMessage.textContent = selectedSlot.disabled
            ? 'Chọn thợ và ngày trước khi nhập giờ hẹn.'
            : 'Nhập giờ hẹn. Dịch vụ phải hoàn tất trong giờ mở cửa và ca làm của thợ.';
        if (selectedSlot.disabled) return;
        const current = slotVersion;
        slotRequest = new AbortController();
        const q = query(); q.set('stylistId', selected.value); q.set('date', date.value);
        try {
            const data = await json(`${panel.dataset.datesUrl}?${q}`, slotRequest.signal);
            if (current !== slotVersion) return;
            if (data.slotCount === 0) {
                dateMessage.textContent = data.suggestions.length
                    ? 'Ngày này không còn giờ trống. Bạn có thể chọn một trong hai ngày gần nhất còn chỗ:'
                    : 'Ngày này không còn giờ trống; chưa tìm thấy ngày còn chỗ trong 14 ngày tiếp theo.';
                data.suggestions.forEach(item => {
                    const b = button(`${item.label} (từ ${item.earliestSlot})`);
                    b.addEventListener('click', () => { date.value = item.date; loadSlots(); });
                    suggestedDates.append(b);
                });
            }
        } catch (e) {
            if (current === slotVersion && e.name !== 'AbortError') dateMessage.textContent = e.message;
        }
    }
    selectedSlot.addEventListener('input', validateTime);
    async function loadStylists() {
        const current = ++version;
        stylistRequest?.abort();
        stylistRequest = new AbortController();
        const previous = selected.value;
        selected.value = '';
        clearSlots(); options.replaceChildren();
        panel.hidden = ids().length === 0;
        if (panel.hidden) return;
        message.textContent = 'Đang tìm thợ phù hợp…';
        try {
            const data = await json(`${panel.dataset.stylistsUrl}?${query()}`, stylistRequest.signal);
            if (current !== version) return;
            selected.value = (previous === '0' && data.length > 0) || data.some(s => String(s.id) === previous) ? previous : '';
            message.textContent = !data.length ? 'Không có thợ nào thực hiện được toàn bộ dịch vụ. Vui lòng bỏ bớt hoặc đổi dịch vụ.'
                : previous && !selected.value ? 'Thợ đã chọn không còn phù hợp. Vui lòng chọn lại thợ.' : 'Các thợ có thể thực hiện toàn bộ dịch vụ:';
            const any = button('Thợ bất kỳ');
            any.dataset.anyStylist = 'true';
            any.disabled = data.length === 0;
            options.append(any);
            if (selected.value === '0') choose(options, any);
            any.addEventListener('click', () => { selected.value = '0'; choose(options, any); loadSlots(); });
            for (const stylist of data) {
                const b = button(stylist.name + (stylist.specialty ? ` — ${stylist.specialty}` : ''));
                if (stylist.image && stylist.image.startsWith('/') && !stylist.image.startsWith('//')) {
                    const img = document.createElement('img'); img.src = stylist.image; img.alt = ''; img.width = img.height = 48;
                    img.style.cssText = 'object-fit:cover;border-radius:50%;margin-right:8px'; img.loading = 'lazy'; b.prepend(img);
                }
                options.append(b);
                if (String(stylist.id) === selected.value) choose(options, b);
                b.addEventListener('click', () => { selected.value = String(stylist.id); choose(options, b); loadSlots(); });
            }
            await loadSlots();
        } catch (e) {
            if (current === version && e.name !== 'AbortError') message.textContent = e.message;
        }
    }
    date.addEventListener('change', loadSlots);
    confirm.addEventListener('click', async () => {
        if (!selected.value || !selectedSlot.value || !date.value) return;
        clearAssignment();
        const current = assignmentVersion;
        assignmentRequest = new AbortController();
        confirmation.hidden = false;
        assignmentMessage.textContent = 'Đang kiểm tra và chọn thợ phù hợp…';
        const q = query(); q.set('stylistId', selected.value); q.set('date', date.value); q.set('start', selectedSlot.value);
        try {
            const data = await json(`${panel.dataset.assignmentUrl}?${q}`, assignmentRequest.signal);
            if (current !== assignmentVersion) return;
            assignmentMessage.textContent = `Thợ được gán: ${data.name}`;
            assignmentDetail.textContent = `Ngày ${data.date} · ${data.start} – ${data.end}`;
            confirmedStylistId = data.stylistId;
            customerDetails.hidden = false;
            confirm.disabled = false;
        } catch (e) {
            if (current !== assignmentVersion || e.name === 'AbortError') return;
            selectedSlot.value = '';
            choose(slots, null);
            assignmentMessage.textContent = e.message;
        }
    });
    submitAppointment.addEventListener('click', async () => {
        if (submitting || confirmedStylistId == null) return;
        const fields = ['booking-full-name', 'booking-phone', 'booking-email', 'booking-notes'].map(id => document.getElementById(id));
        fields[0].value = fields[0].value.trim();
        if (fields.some(field => !field.reportValidity())) return;
        const payload = { date: date.value, startTime: selectedSlot.value, serviceIds: ids().map(Number),
            stylistId: confirmedStylistId, fullName: fields[0].value, phone: fields[1].value,
            email: fields[2].value || null, notes: fields[3].value || null };
        submitting = true;
        const controls = [...document.getElementById('booking-form').querySelectorAll('input, button, textarea')];
        const disabledStates = controls.map(control => control.disabled);
        controls.forEach(control => control.disabled = true);
        bookingResult.textContent = 'Đang gửi yêu cầu đặt lịch…';
        let succeeded = false;
        try {
            const response = await fetch(panel.dataset.confirmUrl, { method: 'POST',
                headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
            const data = await response.json();
            if (!response.ok) {
                bookingResult.textContent = [data.message, ...Object.values(data.errors || {})].filter(Boolean).join(' ');
                if (data.reloadSlots) {
                    await validateTime();
                    slotMessage.textContent = `${data.message} ${slotMessage.textContent}`;
                }
                return;
            }
            succeeded = true;
            customerDetails.hidden = true;
            confirmedStylistId = null;
            assignmentMessage.textContent = `Thợ: ${data.stylistName}`;
            assignmentDetail.textContent = `${data.date} · ${data.startTime} – ${data.endTime}`;
            const confirmedServices = document.getElementById('booking-confirmed-services');
            confirmedServices.textContent = `Dịch vụ: ${data.services.join(', ')}`;
            confirmedServices.hidden = false;
            bookingResult.textContent = `Đặt lịch thành công! Mã lịch hẹn: ${data.reference}. Vui lòng lưu mã để tra cứu lịch hẹn.`;
            bookingResult.classList.add('text-success');
        } catch {
            bookingResult.textContent = 'Chưa nhận được kết quả từ máy chủ. Vui lòng tra cứu lịch hẹn bằng số điện thoại trước khi gửi lại.';
        } finally {
            submitting = false;
            controls.forEach((control, index) => control.disabled = disabledStates[index]);
            if (succeeded) confirm.disabled = true;
        }
    });
    document.getElementById('btn-submit-booking')?.addEventListener('click', () => {
        panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
        (options.querySelector('button') || date).focus({ preventScroll: true });
    });
    document.addEventListener('booking:services-changed', loadStylists);
    loadStylists();
});
