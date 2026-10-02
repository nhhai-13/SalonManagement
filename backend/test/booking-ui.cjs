const { test } = require('node:test');
const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const { createServer } = require('node:net');
const { resolve } = require('node:path');
const { chromium } = require('playwright');

test('Booking UI selects a specific stylist or any stylist and handles no matches', { timeout: 60000 }, async t => {
  const frontend = resolve(__dirname, '../../frontend');
  const port = await new Promise((resolvePort, reject) => {
    const server = createServer();
    server.on('error', reject);
    server.listen(0, '127.0.0.1', () => { const value = server.address().port; server.close(() => resolvePort(value)); });
  });
  const vite = spawn(process.execPath, [resolve(frontend, 'node_modules/vite/bin/vite.js'), '--host', '127.0.0.1', '--port', String(port), '--strictPort'], {
    cwd: frontend, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe']
  });
  let logs = '';
  vite.stdout.on('data', chunk => logs += chunk);
  vite.stderr.on('data', chunk => logs += chunk);
  let browser;
  t.after(async () => {
    await browser?.close();
    if (vite.exitCode === null) { const stopped = new Promise(done => vite.once('exit', done)); vite.kill(); await stopped; }
  });
  const origin = `http://127.0.0.1:${port}`;
  let ready = false;
  for (let attempt = 0; attempt < 100; attempt++) {
    try { if ((await fetch(origin)).ok) { ready = true; break; } } catch {}
    await new Promise(done => setTimeout(done, 100));
  }
  assert.ok(ready, `Vite did not start: ${logs}`);
  browser = await chromium.launch({ headless: true, ...(process.env.PLAYWRIGHT_CHANNEL ? { channel: process.env.PLAYWRIGHT_CHANNEL } : {}) });
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  const ids = {
    one: 'f0d0b0a0-0000-4000-8000-000000000001',
    two: 'f0d0b0a0-0000-4000-8000-000000000002',
    none: 'f0d0b0a0-0000-4000-8000-000000000003'
  };
  const group = [{ id: 'f0d0b0a0-0000-4000-8000-000000000010', name: 'Dịch vụ', displayOrder: 1, services: [
    { id: ids.one, name: 'Dịch vụ một' }, { id: ids.two, name: 'Dịch vụ hai' }, { id: ids.none, name: 'Dịch vụ chưa có thợ' }
  ] }];
  await page.route('**/api/public/catalog-events', route => route.abort());
  await page.route('**/api/public/service-groups', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(group) }));
  await page.route('**/api/public/stylists**', route => {
    const selected = new URL(route.request().url()).searchParams.get('serviceIds')?.split(',') ?? [];
    let matching = [];
    if (selected.length && selected.every(id => [ids.one, ids.two].includes(id))) {
      if (selected.includes(ids.one)) matching.push({ id: 'stylist-mai', name: 'Mai Trần' });
      if (selected.every(id => [ids.one, ids.two].includes(id))) matching.push({ id: 'stylist-linh', name: 'Linh Nguyễn' });
      if (selected.includes(ids.two)) matching = matching.filter(stylist => stylist.id === 'stylist-linh');
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(matching) });
  });

  await page.goto(`${origin}/dat-lich`);
  await page.getByRole('button', { name: /Dịch vụ một Chọn dịch vụ/ }).click();
  await page.getByRole('button', { name: /Mai Trần/ }).waitFor();
  await page.getByRole('button', { name: /Linh Nguyễn/ }).click();
  assert.equal(await page.getByRole('button', { name: /Linh Nguyễn/ }).getAttribute('aria-pressed'), 'true');
  await page.getByRole('button', { name: /Thợ bất kỳ/ }).click();
  assert.equal(await page.getByRole('button', { name: /Thợ bất kỳ/ }).getAttribute('aria-pressed'), 'true');

  await page.getByRole('button', { name: /Dịch vụ hai Chọn dịch vụ/ }).click();
  await page.getByRole('button', { name: /Mai Trần/ }).waitFor({ state: 'detached' });
  await page.getByRole('button', { name: /Linh Nguyễn/ }).waitFor();
  assert.equal(await page.getByRole('button', { name: /Linh Nguyễn/ }).count(), 1);
  await page.getByRole('button', { name: /Dịch vụ chưa có thợ Chọn dịch vụ/ }).click();
  await page.getByText('Chưa có thợ nào có thể thực hiện toàn bộ dịch vụ đã chọn.').waitFor();
});
