(() => {
    const form = document.getElementById('analytics-form');
    const field = id => document.getElementById(id);
    const rate = value => new Intl.NumberFormat('vi-VN', {
        minimumFractionDigits: 1, maximumFractionDigits: 1
    }).format(value) + ' %';
    const date = value => value.split('-').reverse().join('/');
    let pending = false;

    function renderChart(data) {
        const buckets = data.hourlyBuckets || [];
        const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        const max = Math.max(1, ...buckets.map(bucket => bucket.count));
        const top = Math.max(5, Math.ceil(max / 5) * 5);
        const x0 = 65, y0 = 275, plotHeight = 220, columnWidth = 47;
        function node(tag, attrs, text, parent = svg) {
            const item = document.createElementNS('http://www.w3.org/2000/svg', tag);
            for (const [key, value] of Object.entries(attrs)) item.setAttribute(key, value);
            if (text !== undefined) item.textContent = text;
            parent.appendChild(item);
            return item;
        }
        node('text', { x: 5, y: 20, 'font-size': 14 }, 'Số lượt hẹn');
        for (let i = 0; i <= 5; i++) {
            const y = y0 - plotHeight * i / 5;
            node('line', { x1: x0, x2: 1195, y1: y, y2: y, stroke: '#d9dee5' });
            node('text', { x: 55, y: y + 5, 'text-anchor': 'end', 'font-size': 13 }, String(top * i / 5));
        }
        node('line', { x1: x0, x2: x0, y1: 55, y2: y0, stroke: '#495057' });
        buckets.forEach((bucket, index) => {
            const x = x0 + index * columnWidth + 8;
            const height = bucket.count / top * plotHeight;
            const detail = `${bucket.start} - ${bucket.end}: ${bucket.count} lượt`;
            const group = node('g', { tabindex: 0, role: 'button', 'aria-label': detail, cursor: 'pointer' });
            node('title', {}, detail, group);
            node('rect', { x, y: 40, width: 34, height: 265, fill: 'transparent' }, undefined, group);
            node('rect', { x, y: y0 - Math.max(1, height), width: 34, height: Math.max(1, height),
                rx: 2, fill: bucket.count ? '#315bd6' : '#adb5bd' }, undefined, group);
            node('text', { x: x + 17, y: y0 - height - 8, 'text-anchor': 'middle', 'font-size': 13 }, String(bucket.count), group);
            node('text', { x: x + 17, y: 295, 'text-anchor': 'middle', 'font-size': 12 }, bucket.start, group);
            const show = () => { field('hourly-detail').textContent = detail; };
            ['mouseenter', 'focus', 'click'].forEach(event => group.addEventListener(event, show));
            group.addEventListener('keydown', event => {
                if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); show(); }
            });
        });
        node('text', { x: 630, y: 330, 'text-anchor': 'middle', 'font-size': 14 }, 'Khung giờ bắt đầu (UTC+7)');
        return svg;
    }

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
            const chart = data.total > 0 ? renderChart(data) : null;
            field('viewing-range').textContent = date(data.startDate) + ' - ' + date(data.endDate);
            field('empty-reason').textContent = data.emptyReason || '';
            field('empty-reason').hidden = data.total !== 0;
            field('metric-cards').hidden = data.total === 0;
            field('sample-warning').hidden = !data.isSmallSample;
            field('hourly-chart-section').hidden = data.total === 0;
            field('unassigned-times').hidden = !data.unassignedTimeCount;
            field('unassigned-times').textContent = data.unassignedTimeCount
                ? `${data.unassignedTimeCount} lịch có giờ bắt đầu không hợp lệ, chưa xếp vào cột.` : '';
            field('hourly-chart').replaceChildren(...(chart ? Array.from(chart.childNodes) : []));
            field('hourly-detail').textContent = 'Rê, chạm hoặc dùng phím Tab để xem chi tiết từng cột.';
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
