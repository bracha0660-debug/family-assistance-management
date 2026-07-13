import { chromium } from 'playwright';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const fixture = path.resolve(__dirname, '../evidence/history-ui-section20.html');
const outDir = path.resolve(__dirname, '../evidence');

async function main() {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 900, height: 700 } });
  await page.goto(`file:///${fixture.replace(/\\/g, '/')}`);

  await page.locator('#shot-amount').screenshot({
    path: path.join(outDir, 'screenshot-amount-75-to-350.png'),
  });
  await page.locator('.history-value-transition').first().screenshot({
    path: path.join(outDir, 'screenshot-rtl-previous-right.png'),
  });
  await page.locator('#shot-icon-row').screenshot({
    path: path.join(outDir, 'screenshot-history-icon-row-end.png'),
  });
  await page.locator('#shot-window').screenshot({
    path: path.join(outDir, 'screenshot-opened-vertical-window.png'),
  });

  // Assert DOM order: previous text then arrow then new
  const texts = await page.locator('.history-value-transition').first().evaluate((el) => {
    const kids = [...el.children].map((c) => (c.textContent || '').trim());
    const dir = el.getAttribute('dir');
    return { kids, dir };
  });
  if (texts.dir !== 'rtl') throw new Error(`expected dir=rtl, got ${texts.dir}`);
  if (!texts.kids[0].includes('75')) throw new Error(`expected previous 75 first in DOM, got ${texts.kids[0]}`);
  if (texts.kids[1] !== '←') throw new Error(`expected arrow, got ${texts.kids[1]}`);
  if (!texts.kids[2].includes('350')) throw new Error(`expected new 350 third in DOM, got ${texts.kids[2]}`);

  await browser.close();
  console.log('PASS evidence screenshots written to frontend/evidence/');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
