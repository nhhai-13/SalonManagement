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
    let assignmentVersion = 0, assignmentRequest;
    function clearAssignment() {
        assignmentVersion++;
        assignmentRequest?.abort();
        confirmation.hidden = true;
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
        slots.replaceChildren();
        slotMessage.textContent = '';
    }
    async function json(url, signal) {
        const response = await fetch(url, { signal, cache: 'no-store' });
        const data = await response.json();
        if (!response.ok) throw new Error(data.message || 'Unable to load data. Please try again.');
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
    async function loadSlots() {
        clearSlots();
        if (!selected.value || !date.value || !date.validity.valid) {
            slotMessage.textContent = 'Choose a stylist and date to see available times.';
            return;
        }
        const current = slotVersion;
        slotRequest = new AbortController();
        const q = query(); q.set('stylistId', selected.value); q.set('date', date.value);
        slotMessage.textContent = 'Loading available times…';
        try {
            const data = await json(`${panel.dataset.slotsUrl}?${q}`, slotRequest.signal);
            if (current !== slotVersion) return;
            slotMessage.textContent = data.length ? 'Choose a start time (Vietnam time).' : selected.value === '0'
                ? 'No qualified stylists have available times on this date. Choose another date or change your services.'
                : 'Your selected stylist has no available times on this date. Choose another date or stylist.';
            for (const slot of data) {
                const b = button(`${slot.start} – ${slot.end}`);
                b.addEventListener('click', () => {
                    clearAssignment(); selectedSlot.value = slot.start; choose(slots, b); confirm.disabled = false;
                });
                slots.append(b);
            }
        } catch (e) {
            if (current === slotVersion && e.name !== 'AbortError') slotMessage.textContent = e.message;
        }
    }
    async function loadStylists() {
        const current = ++version;
        stylistRequest?.abort();
        stylistRequest = new AbortController();
        const previous = selected.value;
        selected.value = '';
        clearSlots(); options.replaceChildren();
        panel.hidden = ids().length === 0;
        if (panel.hidden) return;
        message.textContent = 'Finding qualified stylists…';
        try {
            const data = await json(`${panel.dataset.stylistsUrl}?${query()}`, stylistRequest.signal);
            if (current !== version) return;
            selected.value = (previous === '0' && data.length > 0) || data.some(s => String(s.id) === previous) ? previous : '';
            message.textContent = !data.length ? 'No stylist can perform all your selected services. Remove or change a service.'
                : previous && !selected.value ? 'Your selected stylist is no longer eligible. Please choose another stylist.' : 'Stylists who can perform all your selected services:';
            const any = button('Any stylist');
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
        assignmentMessage.textContent = 'Checking availability and selecting a stylist…';
        const q = query(); q.set('stylistId', selected.value); q.set('date', date.value); q.set('start', selectedSlot.value);
        try {
            const data = await json(`${panel.dataset.assignmentUrl}?${q}`, assignmentRequest.signal);
            if (current !== assignmentVersion) return;
            assignmentMessage.textContent = `Assigned stylist: ${data.name}`;
            assignmentDetail.textContent = `Date: ${data.date} · ${data.start} – ${data.end}`;
            confirm.disabled = false;
        } catch (e) {
            if (current !== assignmentVersion || e.name === 'AbortError') return;
            selectedSlot.value = '';
            choose(slots, null);
            assignmentMessage.textContent = e.message;
        }
    });
    document.getElementById('btn-submit-booking')?.addEventListener('click', () => {
        panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
        (options.querySelector('button') || date).focus({ preventScroll: true });
    });
    document.addEventListener('booking:services-changed', loadStylists);
    loadStylists();
});
