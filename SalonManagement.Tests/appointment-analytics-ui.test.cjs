const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');

test('report formatting, invalid dates and network failures preserve successful results', async () => {
    const elements = new Map();
    const makeNode = () => ({ textContent: '', hidden: false, childNodes: [], attributes: {}, events: {},
        classList: { add() {}, remove() {} },
        setAttribute(key, value) { this.attributes[key] = value; },
        appendChild(child) { this.childNodes.push(child); },
        replaceChildren(...children) { this.childNodes = children; },
        addEventListener(event, handler) { this.events[event] = handler; } });
    const element = id => {
        if (!elements.has(id)) elements.set(id, makeNode());
        return elements.get(id);
    };
    let submit;
    const button = {};
    Object.assign(element('analytics-form'), {
        action: '/api/appointment-analytics', querySelector: () => button,
        addEventListener: (_, handler) => { submit = handler; }
    });
    const hourlyBuckets = Array.from({ length: 24 }, (_, hour) => ({ start: `${String(hour).padStart(2, '0')}:00`,
        end: `${String(hour + 1).padStart(2, '0')}:00`, count: hour === 10 ? 23 : 0 }));
    const dayTypes = ['Ngày thường', 'Thứ bảy', 'Chủ nhật'].map((label, index) => ({
        label, dayCount: [22, 5, 4][index], total: 10, averagePerDay: 10 / [22, 5, 4][index],
        noShowCount: index, cancelledCount: 0, noShowRate: index * 10, cancellationRate: 0,
        hourlyBuckets: hourlyBuckets.map(bucket => ({ ...bucket, count: bucket.start === '10:00' ? 10 : 0,
            averagePerDay: bucket.start === '10:00' ? 10 / [22, 5, 4][index] : 0, isPeak: bucket.start === '10:00' }))
    }));
    let response = { ok: true, json: async () => ({ startDate: '2026-09-01', endDate: '2026-09-30', hourlyBuckets, dayTypes,
        total: 80, noShowCount: 6, cancelledCount: 4, noShowRate: 7.5, cancellationRate: 5, isSmallSample: false }) };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../SalonManagement/wwwroot/js/appointment-analytics.js'), 'utf8'), {
        document: { getElementById: element, createElementNS: () => makeNode(), createElement: () => makeNode() }, Intl, URLSearchParams,
        FormData: class { *[Symbol.iterator]() { yield ['StartDate', '2026-09-01']; yield ['EndDate', '2026-09-30']; } },
        fetch: async () => { if (response instanceof Error) throw response; return response; }
    });
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(element('no-show-rate').textContent, '7,5 %');
    assert.equal(element('cancellation-rate').textContent, '5,0 %');
    assert.equal(element('no-show-count').textContent, '6/80 lịch hẹn');
    assert.equal(element('viewing-range').textContent, '01/09/2026 - 30/09/2026');
    assert.equal(element('hourly-chart-section').hidden, false);
    const columns = element('hourly-chart').childNodes.filter(n => n.attributes.role === 'button');
    assert.equal(columns.length, 24);
    columns[10].events.mouseenter();
    assert.equal(element('hourly-detail').textContent, '10:00 - 11:00: 23 lượt');
    columns[9].events.click();
    assert.equal(element('hourly-detail').textContent, '09:00 - 10:00: 0 lượt');
    columns[10].events.focus();
    assert.equal(element('hourly-detail').textContent, '10:00 - 11:00: 23 lượt');
    const lastChart = element('hourly-chart').childNodes;
    const cards = element('comparison-charts').childNodes;
    assert.equal(cards.length, 3);
    const charts = cards.map(card => card.childNodes.find(node => node.attributes['data-axis-max']));
    assert.equal(new Set(charts.map(chart => chart.attributes['data-axis-max'])).size, 1);
    const peakColumns = charts.map(chart => chart.childNodes.filter(node => node.attributes.role === 'button')[10]);
    assert.equal(peakColumns[1].childNodes.some(node => node.attributes.fill === '#b45309'), true);
    peakColumns[1].events.click();
    assert.equal(cards[1].childNodes.some(node => node.textContent.includes('Thứ bảy · 10:00 - 11:00: 2,0 lượt/ngày (10 lượt) · Cao điểm')), true);
    assert.equal(cards[1].childNodes.some(node => node.textContent === 'Cao điểm: 10:00'), true);

    response = { ok: false, status: 400, json: async () => ({ errors: { EndDate: ['Khoảng ngày không hợp lệ'] } }) };
    await submit({ preventDefault() {} });
    assert.equal(element('end-error').textContent, 'Khoảng ngày không hợp lệ');
    assert.equal(element('no-show-rate').textContent, '7,5 %');
    assert.equal(element('viewing-range').textContent, '01/09/2026 - 30/09/2026');
    assert.equal(element('hourly-chart').childNodes, lastChart);
    assert.equal(element('comparison-charts').childNodes, cards);
    response = new Error('offline');
    await submit({ preventDefault() {} });
    assert.equal(element('no-show-rate').textContent, '7,5 %');
    assert.equal(button.disabled, false);

    response = { ok: true, json: async () => ({ startDate: '2026-10-01', endDate: '2026-10-01', total: 0,
        noShowRate: null, cancellationRate: null, emptyReason: 'Không có lịch hẹn trong khoảng ngày này', isSmallSample: false }) };
    await submit({ preventDefault() {} });
    assert.equal(element('metric-cards').hidden, true);
    assert.equal(element('empty-reason').hidden, false);
    assert.equal(element('empty-reason').textContent, 'Không có lịch hẹn trong khoảng ngày này');
    assert.equal(element('hourly-chart-section').hidden, true);
    assert.equal(element('hourly-chart').childNodes.length, 0);

    response = { ok: true, json: async () => ({ startDate: '2026-10-01', endDate: '2026-10-01', total: 2,
        noShowCount: 0, cancelledCount: 0, noShowRate: 0, cancellationRate: 0, isSmallSample: true,
        hourlyBuckets: hourlyBuckets.map(bucket => ({ ...bucket, count: bucket.start === '09:00' ? 2 : 0 })) }) };
    await submit({ preventDefault() {} });
    assert.equal(element('no-show-rate').textContent, '0,0 %');
    assert.equal(element('metric-cards').hidden, false);
    assert.equal(element('empty-reason').hidden, true);
    assert.equal(element('sample-warning').hidden, false);
    const newColumns = element('hourly-chart').childNodes.filter(n => n.attributes.role === 'button');
    newColumns[9].events.click();
    assert.equal(element('hourly-detail').textContent, '09:00 - 10:00: 2 lượt');

    response = { ok: true, json: async () => ({ startDate: '2026-10-02', endDate: '2026-10-02', total: 0,
        emptyReason: 'Không có lịch hẹn trong khoảng ngày này', dayTypes: dayTypes.map((group, index) => ({
            ...group, dayCount: index === 0 ? 1 : 0, total: 0, averagePerDay: index === 0 ? 0 : null,
            noShowRate: null, cancellationRate: null, hourlyBuckets: index === 0 ? [] : null,
            emptyReason: index === 0 ? 'Không có lịch hẹn trong nhóm ngày này' : `Khoảng ngày chưa có ${group.label.toLowerCase()}`
        })) }) };
    await submit({ preventDefault() {} });
    assert.equal(element('day-comparison').hidden, false);
    const emptyCards = element('comparison-charts').childNodes;
    assert.equal(emptyCards[1].childNodes.some(node => node.textContent === 'Khoảng ngày chưa có thứ bảy'), true);
    assert.equal(emptyCards[2].childNodes.some(node => node.textContent === 'Khoảng ngày chưa có chủ nhật'), true);
});
