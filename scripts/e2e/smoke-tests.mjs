import { chromium } from '@playwright/test';

const baseUrl = process.env.EDUPLATFORM_BASE_URL ?? 'http://localhost:5297';
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage();

try {
  await page.goto(`${baseUrl}/explore`);
  await page.locator('.course-card').first().waitFor();

  const initialCount = await page.locator('.course-card').count();
  assert(initialCount > 0, 'Le catalogue doit contenir au moins un cours.');

  const search = page.locator('input[aria-label="Rechercher un cours"]');
  await search.fill('Blazor');
  await search.press('Enter');
  await page.waitForURL('**/explore?search=Blazor');
  await page.locator('.course-card').first().waitFor();

  assert(await page.locator('.course-card').count() === 1, 'La recherche Blazor doit retourner un seul cours.');
  assert((await page.locator('.course-card h3').first().textContent()).includes('Blazor'), 'Le résultat doit être le cours Blazor.');

  await page.goto(`${baseUrl}/explore`);
  await page.locator('.course-card').first().waitFor();
  await page.waitForTimeout(2500);
  await page.locator('select').first().selectOption('WebDev');
  await page.getByText('1 cours disponibles').waitFor();
  await page.locator('select').nth(1).locator('option[value="Intermediaire"]').waitFor({ state: 'attached' });
  await page.locator('select').nth(1).selectOption('Intermediaire');
  await page.getByText('1 cours disponibles').waitFor();
  await page.locator('.course-card').first().waitFor();

  assert(await page.locator('.course-card').count() === 1, 'Le filtre WebDev/Intermédiaire doit retourner un seul cours.');
  const courseId = await page.locator('.course-card').first().getAttribute('data-course-id');
  assert(courseId, 'Le cours filtré doit exposer son identifiant.');
  await page.goto(`${baseUrl}/courses/${courseId}`);
  await page.locator('h1').first().waitFor();
  assert(await page.locator('h1').first().textContent() === 'ASP.NET Core et Blazor', 'Le détail du cours doit être ouvert.');
  assert(await page.locator('.course-reviews').count() === 1, 'Le détail doit afficher la section des avis.');

  console.log('E2E smoke tests passed: catalogue, recherche, filtres et détail du cours.');
} finally {
  await browser.close();
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}
