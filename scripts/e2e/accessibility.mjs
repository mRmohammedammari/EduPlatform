import AxeBuilder from '@axe-core/playwright';
import { chromium } from '@playwright/test';

const baseUrl = process.env.EDUPLATFORM_BASE_URL ?? 'http://localhost:5297';
const browser = await chromium.launch({ headless: true });
const context = await browser.newContext();
const page = await context.newPage();
const blockingViolations = [];

try {
  await page.goto(`${baseUrl}/`, { waitUntil: 'domcontentloaded' });
  await page.locator('h1').first().waitFor();
  await audit('Accueil');

  await page.goto(`${baseUrl}/explore`, { waitUntil: 'domcontentloaded' });
  await page.locator('.course-card').first().waitFor();
  await audit('Catalogue');

  const courseId = await page.locator('.course-card').first().getAttribute('data-course-id');
  if (!courseId) {
    throw new Error('Aucun cours disponible pour auditer le detail.');
  }

  await page.goto(`${baseUrl}/courses/${courseId}`, { waitUntil: 'domcontentloaded' });
  await page.locator('h1').first().waitFor();
  await audit('Detail du cours');

  if (blockingViolations.length > 0) {
    throw new Error(`${blockingViolations.length} violation(s) axe de severite serious/critical.`);
  }

  console.log('Accessibility audit passed: aucune violation serious/critical sur les pages publiques.');
} finally {
  await browser.close();
}

async function audit(label) {
  const result = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();

  const blocking = result.violations.filter((violation) =>
    violation.impact === 'serious' || violation.impact === 'critical');
  console.log(`${label}: ${result.violations.length} violation(s), ${blocking.length} serious/critical.`);

  for (const violation of result.violations) {
    console.log(`  ${violation.impact ?? 'unknown'} ${violation.id}: ${violation.help}`);
    for (const node of violation.nodes.slice(0, 3)) {
      console.log(`    ${node.target.join(', ')}`);
      if (node.failureSummary) {
        console.log(`      ${node.failureSummary.replace(/\s+/g, ' ').trim()}`);
      }
    }
  }

  blockingViolations.push(...blocking.map((violation) => `${label}: ${violation.id}`));
}
