import { chromium, request } from '@playwright/test';

const baseUrl = process.env.EDUPLATFORM_BASE_URL ?? 'http://localhost:5297';
const apiUrl = process.env.EDUPLATFORM_API_URL ?? 'http://localhost:5053';
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage();

try {
  await page.goto(`${baseUrl}/explore`);
  await page.locator('.course-card').first().waitFor();
  await page.waitForTimeout(2500);

  const initialCount = await page.locator('.course-card').count();
  assert(initialCount > 0, 'Le catalogue doit contenir au moins un cours.');

  const firstPageResponse = await page.request.get(
    `${apiUrl}/api/courses/paged?page=1&pageSize=1&sort=recent`
  );
  assert(firstPageResponse.ok(), 'Le endpoint de catalogue pagine doit repondre.');
  const firstPage = await firstPageResponse.json();
  assert(firstPage.items.length === 1, 'pageSize=1 doit renvoyer un seul element.');
  assert(firstPage.totalItems >= initialCount, 'totalItems doit compter tout le catalogue filtre.');
  assert(firstPage.totalPages >= 2, 'Le catalogue de test doit fournir plusieurs pages.');

  const secondPageResponse = await page.request.get(
    `${apiUrl}/api/courses/paged?page=2&pageSize=1&sort=recent`
  );
  assert(secondPageResponse.ok(), 'La seconde page du catalogue doit repondre.');
  const secondPage = await secondPageResponse.json();
  assert(secondPage.items.length === 1, 'La seconde page doit renvoyer son element.');
  assert(secondPage.items[0].id !== firstPage.items[0].id, 'Les pages serveur ne doivent pas se recouvrir.');

  const outOfRangeResponse = await page.request.get(
    `${apiUrl}/api/courses/paged?page=999&pageSize=1&sort=recent`
  );
  assert(outOfRangeResponse.ok(), 'Une page hors limites doit rester une reponse valide.');
  const outOfRange = await outOfRangeResponse.json();
  assert(outOfRange.page === outOfRange.totalPages, 'La page demandee doit etre bornee a la derniere page.');
  assert(outOfRange.items.length === 1, 'La derniere page bornee doit contenir son cours.');

  const search = page.locator('input[aria-label="Rechercher un cours"]');
  await search.fill('Blazor');
  await search.press('Enter');
  await page.waitForURL('**/explore?search=Blazor');
  await page.getByText('1 cours disponibles').waitFor();
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
  await page.locator('button.btn-primary').first().click();
  await page.getByRole('alert').waitFor();
  assert((await page.getByRole('alert').textContent()).includes('titre'), 'La création vide doit afficher la validation métier.');

  const testCourseTitle = `E2E course ${Date.now()}`;
  await page.locator('input[placeholder="Titre du cours"]').fill(testCourseTitle);
  await page.locator('input[placeholder="Informatique"]').fill('E2E');
  await page.locator('textarea').first().fill('Cours créé par le smoke test.');
  await page.locator('input[type="number"]').first().fill('30');
  await page.locator('button.btn-primary').first().click();
  await page.locator('h3').filter({ hasText: testCourseTitle }).waitFor();

  const createdCourseId = await cleanupInstructorCourse(testCourseTitle);
  assert(createdCourseId, 'Le cours E2E créé doit être nettoyé.');

  await loginAs('admin@eduplatform.com', 'Admin123!');
  await page.goto(`${baseUrl}/admin/users`);
  await page.getByRole('heading', { name: 'Gestion des utilisateurs' }).waitFor();
  await page.goto(`${baseUrl}/admin/reviews`);
  await page.getByRole('heading', { name: 'Modération des avis' }).waitFor();

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

async function cleanupInstructorCourse(title) {
  const apiUrl = process.env.EDUPLATFORM_API_URL ?? 'http://localhost:5053';
  const api = await request.newContext();
  const login = await api.post(`${apiUrl}/api/auth/login`, {
    data: { email: 'instructor@eduplatform.com', password: 'Instructor123!' }
  });
  assert(login.ok(), 'Le compte instructeur doit permettre le nettoyage E2E.');
  const auth = await login.json();
  const courses = await api.get(`${apiUrl}/api/courses/mine`, {
    headers: { Authorization: `Bearer ${auth.token}` }
  });
  assert(courses.ok(), 'Les cours instructeur doivent être accessibles pour le nettoyage E2E.');
  const course = (await courses.json()).find((item) => item.title === title);
  assert(course, 'Le cours E2E créé doit être retrouvé dans les cours instructeur.');
  const archive = await api.post(`${apiUrl}/api/courses/${course.id}/archive`, {
    headers: { Authorization: `Bearer ${auth.token}` }
  });
  assert(archive.ok(), 'Le cours E2E doit être archivable.');
  const adminLogin = await api.post(`${apiUrl}/api/auth/login`, {
    data: { email: 'admin@eduplatform.com', password: 'Admin123!' }
  });
  assert(adminLogin.ok(), 'Le compte admin doit permettre le nettoyage E2E.');
  const adminAuth = await adminLogin.json();
  const deletion = await api.delete(`${apiUrl}/api/courses/${course.id}?confirm=DELETE`, {
    headers: { Authorization: `Bearer ${adminAuth.token}` }
  });
  assert(deletion.ok(), 'Le cours E2E archivé doit être supprimable.');
  await api.dispose();
  return course.id;
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}
