const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');

test('report formatting, invalid dates and network failures preserve successful results', async () => {
    const elements = new Map();
    const element = id => {
        if (!elements.has(id)) elements.set(id, { textContent: '', hidden: false, classList: { add() {}, remove() {} } });
        return elements.get(id);
    };
    let submit;
    const button = {};
    Object.assign(element('analytics-form'), {
        action: '/api/appointment-analytics', querySelector: () => button,
        addEventListener: (_, handler) => { submit = handler; }
    });
    let response = { ok: true, json: async () => ({ startDate: '2026-09-01', endDate: '2026-09-30',
        total: 80, noShowCount: 6, cancelledCount: 4, noShowRate: 7.5, cancellationRate: 5, isSmallSample: false }) };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../SalonManagement/wwwroot/js/appointment-analytics.js'), 'utf8'), {
        document: { getElementById: element }, Intl, URLSearchParams,
        FormData: class { *[Symbol.iterator]() { yield ['StartDate', '2026-09-01']; yield ['EndDate', '2026-09-30']; } },
        fetch: async () => { if (response instanceof Error) throw response; return response; }
    });
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(element('no-show-rate').textContent, '7,5 %');
    assert.equal(element('cancellation-rate').textContent, '5,0 %');
    assert.equal(element('no-show-count').textContent, '6/80 lịch hẹn');
    assert.equal(element('viewing-range').textContent, '01/09/2026 - 30/09/2026');

    response = { ok: false, status: 400, json: async () => ({ errors: { EndDate: ['Khoảng ngày không hợp lệ'] } }) };
    await submit({ preventDefault() {} });
    assert.equal(element('end-error').textContent, 'Khoảng ngày không hợp lệ');
    assert.equal(element('no-show-rate').textContent, '7,5 %');
    assert.equal(element('viewing-range').textContent, '01/09/2026 - 30/09/2026');
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

    response = { ok: true, json: async () => ({ startDate: '2026-10-01', endDate: '2026-10-01', total: 2,
        noShowCount: 0, cancelledCount: 0, noShowRate: 0, cancellationRate: 0, isSmallSample: true }) };
    await submit({ preventDefault() {} });
    assert.equal(element('no-show-rate').textContent, '0,0 %');
    assert.equal(element('metric-cards').hidden, false);
    assert.equal(element('empty-reason').hidden, true);
    assert.equal(element('sample-warning').hidden, false);
});
