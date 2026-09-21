import { chromium } from '@playwright/test';

const baseUrl = process.env.EDUPLATFORM_BASE_URL ?? 'http://localhost:5297';
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage();

try {
  await page.goto(`${baseUrl}/explore`);
  await page.locator('.course-card').first().waitFor();
  await page.waitForTimeout(2500);

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

  await page.goto(`${baseUrl}/login`);
  await page.waitForTimeout(2500);
  await page.locator('input[type="email"]').fill('student@eduplatform.com');
  await page.locator('input[type="password"]').fill('Student123!');
  await page.getByRole('button', { name: 'Se connecter' }).click();
  await page.waitForURL('**/courses');
  await page.goto(`${baseUrl}/progress`);
  await page.getByRole('heading', { name: 'Ma progression' }).waitFor();
  assert(await page.getByRole('heading', { name: 'Badges' }).count() === 1, 'La progression authentifiée doit afficher les badges.');

  await loginAs('admin@eduplatform.com', 'Admin123!');
  await page.goto(`${baseUrl}/admin`);
  await page.getByRole('heading', { name: 'Tableau de bord administrateur' }).waitFor();
  await page.goto(`${baseUrl}/admin/reviews`);
  await page.getByRole('heading', { name: 'Modération des avis' }).waitFor();

  await loginAs('instructor@eduplatform.com', 'Instructor123!');
  await page.goto(`${baseUrl}/instructor`);
  await page.getByRole('heading', { name: 'Espace enseignant' }).waitFor();
  await page.waitForTimeout(2500);
  const instructorHeadings = await page.locator('h1, h2').allTextContents();
  assert(instructorHeadings.some((heading) => heading.toLowerCase().includes('cours')), `Le parcours instructeur doit afficher le formulaire de cours. Titres: ${instructorHeadings.join(' | ')}`);

  console.log('E2E smoke tests passed: public, student, instructor and admin flows.');
} finally {
  await browser.close();
}

async function loginAs(email, password) {
  await page.goto(`${baseUrl}/login`);
  await page.waitForTimeout(2500);
  await page.locator('input[type="email"]').fill(email);
  await page.locator('input[type="password"]').fill(password);
  await page.getByRole('button', { name: 'Se connecter' }).click();
  await page.waitForURL('**/courses');
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}
