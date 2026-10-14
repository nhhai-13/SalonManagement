(() => {
    const form = document.getElementById('analytics-form');
    const field = id => document.getElementById(id);
    const rate = value => new Intl.NumberFormat('vi-VN', {
        minimumFractionDigits: 1, maximumFractionDigits: 1
    }).format(value) + ' %';
    const date = value => value.split('-').reverse().join('/');
    let pending = false;

    const average = value => new Intl.NumberFormat('vi-VN', { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(value);

    function renderChart(data, options = {}) {
        const buckets = data.hourlyBuckets || [];
        const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        const value = bucket => options.comparison ? bucket.averagePerDay : bucket.count;
        const max = Math.max(1, ...buckets.map(value));
        const top = options.top || Math.max(5, Math.ceil(max / 5) * 5);
        svg.setAttribute('viewBox', options.comparison ? '0 0 700 380' : '0 0 1240 340');
        svg.setAttribute('data-axis-max', top);
        svg.setAttribute('width', '100%');
        svg.setAttribute('style', 'width:100%;height:auto;display:block');
        svg.setAttribute('role', 'group');
        svg.setAttribute('aria-label', options.label || 'Số lượt hẹn theo khung giờ');
        const x0 = options.comparison ? 50 : 65, y0 = options.comparison ? 300 : 275;
        const plotHeight = options.comparison ? 230 : 220, columnWidth = options.comparison ? 26 : 47;
        const xEnd = options.comparison ? 675 : 1195;
        const barWidth = options.comparison ? 19 : 34;
        function node(tag, attrs, text, parent = svg) {
            const item = document.createElementNS('http://www.w3.org/2000/svg', tag);
            for (const [key, value] of Object.entries(attrs)) item.setAttribute(key, value);
            if (text !== undefined) item.textContent = text;
            parent.appendChild(item);
            return item;
        }
        node('text', { x: 5, y: 20, 'font-size': options.comparison ? 20 : 14 }, options.comparison ? 'Lượt / ngày' : 'Số lượt hẹn');
        for (let i = 0; i <= 5; i++) {
            const y = y0 - plotHeight * i / 5;
            node('line', { x1: x0, x2: xEnd, y1: y, y2: y, stroke: '#d9dee5' });
            node('text', { x: x0 - 10, y: y + 5, 'text-anchor': 'end', 'font-size': options.comparison ? 20 : 13 }, options.comparison ? average(top * i / 5) : String(top * i / 5));
        }
        node('line', { x1: x0, x2: x0, y1: y0 - plotHeight, y2: y0, stroke: '#495057' });
        buckets.forEach((bucket, index) => {
            const x = x0 + index * columnWidth + (options.comparison ? 3 : 8);
            const height = value(bucket) / top * plotHeight;
            const detail = options.comparison
                ? `${options.label} · ${bucket.start} - ${bucket.end}: ${average(value(bucket))} lượt/ngày (${bucket.count} lượt)${bucket.isPeak ? ' · Cao điểm' : ''}`
                : `${bucket.start} - ${bucket.end}: ${bucket.count} lượt`;
            const group = node('g', { tabindex: 0, role: 'button', 'aria-label': detail, cursor: 'pointer' });
            node('title', {}, detail, group);
            node('rect', { x, y: 40, width: barWidth, height: y0 - 10, fill: 'transparent' }, undefined, group);
            node('rect', { x, y: y0 - Math.max(1, height), width: barWidth, height: Math.max(1, height),
                rx: 2, fill: options.comparison && bucket.isPeak ? '#b45309' : bucket.count ? '#315bd6' : '#adb5bd' }, undefined, group);
            if (!options.comparison || bucket.count > 0)
                node('text', { x: x + barWidth / 2, y: y0 - height - 8, 'text-anchor': 'middle', 'font-size': options.comparison ? 16 : 13 }, options.comparison ? average(value(bucket)) : String(bucket.count), group);
            if (!options.comparison || index % 3 === 0)
                node('text', { x: x + barWidth / 2, y: y0 + 25, 'text-anchor': 'middle', 'font-size': options.comparison ? 20 : 12 }, bucket.start, group);
            const show = () => { (options.detailElement || field('hourly-detail')).textContent = detail; };
            ['mouseenter', 'focus', 'click'].forEach(event => group.addEventListener(event, show));
            group.addEventListener('keydown', event => {
                if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); show(); }
            });
        });
        node('text', { x: (x0 + xEnd) / 2, y: options.comparison ? 365 : 330, 'text-anchor': 'middle', 'font-size': options.comparison ? 20 : 14 }, 'Khung giờ bắt đầu (UTC+7)');
        return svg;
    }

    function renderComparison(data) {
        const groups = data.dayTypes || [];
        const top = Math.max(1, Math.ceil(Math.max(0, ...groups.flatMap(group => (group.hourlyBuckets || []).map(bucket => bucket.averagePerDay))) * 2) / 2);
        function html(tag, text, parent) {
            const item = document.createElement(tag);
            if (text !== undefined) item.textContent = text;
            parent?.appendChild(item);
            return item;
        }
        const cards = groups.map(group => {
            const card = html('section');
            card.setAttribute('class', 'border rounded p-2');
            card.setAttribute('aria-label', group.label);
            html('h4', group.label, card).setAttribute('class', 'h6');
            html('p', `${group.dayCount} ngày · ${group.total} lịch hẹn`, card).setAttribute('class', 'small');
            const detail = html('p', 'Rê hoặc chạm cột để xem chi tiết.', card);
            detail.setAttribute('class', 'small');
            detail.setAttribute('role', 'status');
            if (group.emptyReason) detail.textContent = group.emptyReason;
            else {
                card.appendChild(renderChart(group, { comparison: true, top, label: group.label, detailElement: detail }));
                const peaks = group.hourlyBuckets.filter(bucket => bucket.isPeak).map(bucket => bucket.start);
                if (peaks.length) html('p', `Cao điểm: ${peaks.join(', ')}`, card).setAttribute('class', 'small fw-bold');
            }
            return card;
        });
        const rows = [
            ['Số ngày', group => String(group.dayCount)],
            ['Số lịch hẹn', group => String(group.total)],
            ['Lượt trung bình / ngày', group => group.averagePerDay === null ? '—' : average(group.averagePerDay)],
            ['Tỉ lệ không đến', group => group.noShowRate === null ? '—' : `${rate(group.noShowRate)} (${group.noShowCount}/${group.total})`],
            ['Tỉ lệ huỷ', group => group.cancellationRate === null ? '—' : `${rate(group.cancellationRate)} (${group.cancelledCount}/${group.total})`]
        ].map(([label, format]) => {
            const row = html('tr');
            html('th', label, row).setAttribute('scope', 'row');
            groups.forEach(group => html('td', format(group), row));
            return row;
        });
        return { cards, rows, visible: groups.length > 0 };
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
            const comparison = renderComparison(data);
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
            field('comparison-charts').replaceChildren(...comparison.cards);
            field('comparison-table').replaceChildren(...comparison.rows);
            field('day-comparison').hidden = !comparison.visible;
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
