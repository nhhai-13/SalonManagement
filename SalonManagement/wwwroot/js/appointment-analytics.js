(() => {
    const form = document.getElementById('analytics-form');
    const field = id => document.getElementById(id);
    const rate = value => new Intl.NumberFormat('vi-VN', {
        minimumFractionDigits: 1, maximumFractionDigits: 1
    }).format(value) + ' %';
    const date = value => value.split('-').reverse().join('/');
    let pending = false;

    async function load(event) {
        event?.preventDefault();
        if (pending) return;
        field('start-error').textContent = '';
        field('end-error').textContent = '';
        field('request-error').textContent = '';
        ['StartDate', 'EndDate'].forEach(id => field(id).classList.remove('is-invalid'));
        pending = true;
        form.querySelector('button').disabled = true;
        try {
            const response = await fetch(form.action + '?' + new URLSearchParams(new FormData(form)), {
                headers: { Accept: 'application/json' }
            });
            const data = await response.json();
            if (!response.ok) {
                if (response.status === 400 && data.errors) {
                    for (const [key, messages] of Object.entries(data.errors)) {
                        const id = key.split('.').pop();
                        if (id === 'StartDate' || id === 'EndDate') {
                            field(id).classList.add('is-invalid');
                            field(id === 'StartDate' ? 'start-error' : 'end-error').textContent = messages.join(' ');
                        } else field('request-error').textContent = messages.join(' ');
                    }
                } else field('request-error').textContent = data.message || 'Không thể tải báo cáo. Vui lòng thử lại.';
                return; // Keep the last successful report and its date range.
            }
            field('viewing-range').textContent = date(data.startDate) + ' - ' + date(data.endDate);
            field('empty-reason').textContent = data.emptyReason || '';
            field('empty-reason').hidden = data.total !== 0;
            field('metric-cards').hidden = data.total === 0;
            field('sample-warning').hidden = !data.isSmallSample;
            if (data.total > 0) {
                field('no-show-rate').textContent = rate(data.noShowRate);
                field('cancellation-rate').textContent = rate(data.cancellationRate);
                field('no-show-count').textContent = `${data.noShowCount}/${data.total} lịch hẹn`;
                field('cancellation-count').textContent = `${data.cancelledCount}/${data.total} lịch hẹn`;
            }
            field('analytics-results').hidden = false;
        } catch {
            field('request-error').textContent = 'Không thể tải báo cáo. Vui lòng thử lại.';
        } finally {
            pending = false;
            form.querySelector('button').disabled = false;
        }
    }
    form.addEventListener('submit', load);
    load();
})();
