// NODE_PATH must point at a directory containing playwright. Run CatalogProbe first.
const { chromium } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
(async () => {
  const out = path.resolve('artifacts/catalog-mobile');
  fs.mkdirSync(out, { recursive: true });
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const results = [];
  try {
    for (let run = 0; run < 5; run++) {
      const context = await browser.newContext({ viewport: { width: 360, height: 800 }, deviceScaleFactor: 1, isMobile: true, hasTouch: true });
      const page = await context.newPage();
      const cdp = await context.newCDPSession(page);
      await cdp.send('Network.enable');
      await cdp.send('Network.setCacheDisabled', { cacheDisabled: true });
      await cdp.send('Network.emulateNetworkConditions', {
        offline: false, latency: 150, downloadThroughput: 1600000 / 8, uploadThroughput: 750000 / 8, connectionType: 'cellular4g'
      });
      const responses = [];
      page.on('response', r => responses.push({ url: r.url(), status: r.status() }));
      await page.goto('http://127.0.0.1:5183/Services/Public', { waitUntil: 'load' });
      await page.locator('.card').last().waitFor();
      const result = await page.evaluate(async () => {
        await document.fonts.ready;
        await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
        const overflow = [...document.querySelectorAll('body *')].filter(e => {
          const r = e.getBoundingClientRect(); return r.width && (r.right > innerWidth + 1 || r.left < -1);
        }).map(e => e.tagName + '.' + e.className);
        return { completeMs: performance.now(), loadMs: performance.getEntriesByType('navigation')[0].loadEventEnd,
          width: innerWidth, scrollWidth: document.documentElement.scrollWidth, overflow,
          cards: document.querySelectorAll('.card').length, groups: document.querySelectorAll('section').length,
          bytes: performance.getEntriesByType('navigation')[0].transferSize + performance.getEntriesByType('resource').reduce((n, r) => n + r.transferSize, 0) };
      });
      if (result.overflow.length || result.width !== 360 || result.scrollWidth > 360) throw Error(JSON.stringify(result));
      if (result.cards !== Number(process.env.CATALOG_PROBE_SERVICES || 100)) throw Error('Wrong service count');
      if (result.groups !== Number(process.env.CATALOG_PROBE_GROUPS || 10)) throw Error('Wrong group count');
      result.requests = responses.length;
      const clipped = await page.locator('.card h3, .card p').evaluateAll(elements => elements.some(e =>
        e.scrollWidth > e.clientWidth + 1 || e.scrollHeight > e.clientHeight + 1));
      if (clipped) throw Error('Clipped service name, description, duration or price');
      if (await page.getByText('Duration: 240 min', { exact: true }).count() !== result.cards ||
          await page.getByText('Price: 20.000.000 VND', { exact: true }).count() !== result.cards) throw Error('Missing price/duration');
      if (responses.some(r => r.status >= 400 || !r.url.startsWith('http://127.0.0.1:5183/'))) throw Error('Unexpected resource: ' + JSON.stringify(responses));
      if (run === 0) {
        await page.screenshot({ path: path.join(out, '360px.png'), fullPage: false });
        await page.locator('.card').first().screenshot({ path: path.join(out, 'long-name.png') });
        await page.getByLabel('Search by service name').fill('cat toc');
        await Promise.all([page.waitForURL('**/*search=cat*'), page.getByRole('button', { name: 'Search' }).click()]);
        if (await page.locator('.card').count() !== 1) throw Error('Search failed');
        if (await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)) throw Error('Search overflow');
        await page.getByRole('link', { name: 'Clear search' }).click();
        await page.waitForURL(url => !url.searchParams.has('search'));
        if (await page.locator('.card').count() !== result.cards) throw Error('Clear failed');
        result.searchAndClear = 'passed';
      }
      results.push({ run: run + 1, ...result });
      await context.close();
    }
    const report = { network: '4G simulated: 1.6 Mbps down, 750 Kbps up, 150 ms latency; cold browser cache', database: 'EF InMemory, synthetic data; NOT production acceptance', browser: browser.version(), results, allUnder2Seconds: results.every(r => r.completeMs < 2000) };
    fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(report, null, 2));
    console.log(JSON.stringify(report, null, 2));
    if (!report.allUnder2Seconds) process.exitCode = 1;
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exit(1); });
