const { chromium } = require('playwright');
const fs = require('node:fs');
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const errors = []; page.on('pageerror', e => errors.push(e.message));
    await page.route(/https:\/\/(fonts\.googleapis|fonts\.gstatic|cdn\.jsdelivr)/, r => r.abort());
    await page.goto('http://127.0.0.1:5184/booking');
    const check = name => page.locator(`.service-checkbox[data-name="${name}"]`);
    const wait = async (fn, message) => { for (let i = 0; i < 100; i++) { if (await fn()) return; await page.waitForTimeout(50); } throw Error(message); };
    await check('Demo Cắt tóc').check();
    await wait(async () => await page.locator('#stylist-options button').count() === 3, 'Expected three stylists');
    await check('Demo Gội đầu').check();
    await wait(async () => await page.locator('#stylist-options button').count() === 2, 'Expected two stylists');
    const today = await page.locator('#booking-date').inputValue();
    const tomorrow = new Date(today + 'T12:00:00Z'); tomorrow.setUTCDate(tomorrow.getUTCDate() + 1);
    await page.locator('#booking-date').fill(tomorrow.toISOString().slice(0, 10));
    await page.getByRole('button', { name: 'Demo Thợ A', exact: false }).click();
    await wait(async () => await page.locator('#slot-options button').count() > 0, 'A slots');
    if (await page.locator('#slot-options').getByRole('button', { name: '09:00 – 10:00', exact: true }).count()) throw Error('A shows B-only slot');
    await page.locator('#slot-options').getByRole('button', { name: '10:00 – 11:00', exact: true }).click();
    await page.getByRole('button', { name: 'Demo Thợ B', exact: false }).click();
    await wait(async () => await page.locator('#slot-options').getByRole('button', { name: '09:00 – 10:00', exact: true }).count() === 1, 'B slots');
    if (await page.locator('#selected-slot').inputValue()) throw Error('Old slot retained');
    await page.getByRole('button', { name: 'Demo Thợ A', exact: false }).click();
    await check('Demo Nhuộm tóc').check();
    await wait(async () => await page.locator('#stylist-options button').count() === 1, 'Dye should remove A');
    if (await page.locator('#selected-stylist').inputValue() || await page.locator('#slot-options button').count()) throw Error('Invalid selection retained');
    await check('Demo Không có thợ').check();
    await wait(async () => (await page.locator('#stylist-message').innerText()).includes('Không có thợ nào'), 'Empty state');
    await page.getByRole('button', { name: 'Bỏ chọn tất cả', exact: false }).click();
    if (await page.locator('#stylist-selection').isVisible()) throw Error('Panel should hide');
    // Delayed old request must not overwrite a newer service selection.
    await page.route('**/booking/stylists?*', async route => {
      const response = await route.fetch();
      if (new URL(route.request().url()).searchParams.getAll('serviceIds').length === 1) await new Promise(r => setTimeout(r, 500));
      await route.fulfill({ response }).catch(() => {});
    });
    await check('Demo Cắt tóc').check(); await check('Demo Nhuộm tóc').check();
    await wait(async () => await page.locator('#stylist-options button').count() === 1, 'Latest response');
    await page.waitForTimeout(700);
    if (await page.locator('#stylist-options button').count() !== 1) throw Error('Stale response rendered');
    await page.getByRole('button', { name: 'Demo Thợ B', exact: false }).click();
    await wait(async () => await page.locator('#slot-options button').count() > 0, 'Final slots');
    const later = new Date(tomorrow); later.setUTCDate(later.getUTCDate() + 8);
    await page.locator('#booking-date').fill(later.toISOString().slice(0, 10));
    await wait(async () => (await page.locator('#slot-message').innerText()).includes('không còn khung giờ'), 'Date change must refresh slots');
    if (await page.locator('#slot-options button').count()) throw Error('Old slots persisted after date change');
    await page.locator('#booking-date').fill(tomorrow.toISOString().slice(0, 10));
    await wait(async () => await page.locator('#slot-options button').count() > 0, 'Date restored');
    fs.mkdirSync('artifacts/booking', { recursive: true });
    await page.locator('#stylist-selection').screenshot({ path: 'artifacts/booking/stylist-selection.png' });
    if (errors.length) throw Error(JSON.stringify(errors));
    console.log('PASS: skills intersection, stylist-specific slots, change selection, invalidation, empty state, clear, stale-response protection; no JS errors.');
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exit(1); });
