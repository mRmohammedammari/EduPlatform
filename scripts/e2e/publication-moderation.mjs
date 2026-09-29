// Test E2E : cycle complet de publication d'un cours et de moderation d'un avis,
// avec donnees persistantes en base (SQL Server), puis nettoyage integral.
//
// Couvre : brouillon -> soumission -> rejet motive -> resoumission -> approbation
//          -> publication -> visibilite catalogue public (API + UI)
//          -> inscription apprenant -> avis -> signalement -> moderation admin
//          -> depublication -> archivage -> suppression physique.
//
// Usage : node publication-moderation.mjs
//   EDUPLATFORM_BASE_URL (defaut http://localhost:5297)
//   EDUPLATFORM_API_URL  (defaut http://localhost:5053)

import { chromium, request } from '@playwright/test';

const baseUrl = process.env.EDUPLATFORM_BASE_URL ?? 'http://localhost:5297';
const apiUrl = process.env.EDUPLATFORM_API_URL ?? 'http://localhost:5053';

const ACCOUNTS = {
  instructor: { email: 'instructor@eduplatform.com', password: 'Instructor123!' },
  admin: { email: 'admin@eduplatform.com', password: 'Admin123!' },
  student: { email: 'student@eduplatform.com', password: 'Student123!' }
};

const runId = Date.now();
const courseTitle = `E2E publication ${runId}`;
const courseCategory = `E2ECat${runId}`;

const api = await request.newContext();
let browser;
let courseId = null;
let reviewId = null;

try {
  const instructor = await login('instructor');
  const admin = await login('admin');
  const student = await login('student');

  // --- 1. Creation du brouillon -------------------------------------------
  courseId = await step('Creation du cours en brouillon', async () => {
    const response = await api.post(`${apiUrl}/api/courses`, {
      headers: authHeader(instructor),
      data: {
        title: courseTitle,
        description: 'Cours cree par le test E2E de publication et moderation.',
        category: courseCategory,
        level: 'Debutant',
        durationMinutes: 45,
        price: 0
      }
    });
    assertOk(response, 'Le cours brouillon doit etre cree.');
    const created = await response.json();
    assert(created.status === 'Draft', `Statut initial attendu Draft, recu ${created.status}.`);
    assert(created.isPublished === false, 'Un brouillon ne doit pas etre publie.');
    return created.id;
  });

  // --- 2. Soumission refusee sans module ----------------------------------
  await step('Soumission refusee tant que le cours n a aucun module', async () => {
    const response = await api.post(`${apiUrl}/api/courses/${courseId}/submit-for-review`, {
      headers: authHeader(instructor)
    });
    assert(response.status() === 400, `Attendu 400 sans module, recu ${response.status()}.`);
  });

  // --- 3. Ajout d un module ------------------------------------------------
  await step('Ajout d un module au cours', async () => {
    const response = await api.post(`${apiUrl}/api/courses/${courseId}/modules`, {
      headers: authHeader(instructor),
      data: {
        title: 'Module E2E',
        description: 'Contenu pedagogique du module de test.',
        durationMinutes: 45,
        order: 1
      }
    });
    assertOk(response, 'Le module doit etre cree.');
  });

  // --- 4. Soumission puis rejet motive ------------------------------------
  await step('Soumission du cours a la validation', async () => {
    const response = await api.post(`${apiUrl}/api/courses/${courseId}/submit-for-review`, {
      headers: authHeader(instructor)
    });
    assertOk(response, 'Le cours doit etre soumis a la validation.');
    assert((await response.json()).status === 'PendingReview', 'Statut attendu PendingReview.');
  });

  await step('Le cours apparait dans la file de validation admin', async () => {
    const response = await api.get(`${apiUrl}/api/courses/pending`, { headers: authHeader(admin) });
    assertOk(response, 'La file de validation doit etre accessible a l admin.');
    const pending = await response.json();
    assert(
      pending.some((course) => course.id === courseId),
      'Le cours soumis doit figurer dans la file de validation.'
    );
  });

  await step('Rejet sans motif refuse', async () => {
    const response = await api.post(`${apiUrl}/api/courses/${courseId}/reject`, {
      headers: authHeader(admin),
      data: { reason: '' }
    });
    assert(response.status() === 400, `Attendu 400 sans motif de rejet, recu ${response.status()}.`);
  });

  await step('Rejet motive par l admin', async () => {
    const response = await api.post(`${apiUrl}/api/courses/${courseId}/reject`, {
      headers: authHeader(admin),
      data: { reason: 'Motif E2E : description a completer.' }
    });
    assertOk(response, 'Le rejet motive doit aboutir.');
    const rejected = await response.json();
    assert(rejected.status === 'Rejected', 'Statut attendu Rejected.');
    assert(rejected.isPublished === false, 'Un cours rejete ne doit pas etre publie.');
    assert(
      (rejected.rejectionReason ?? '').includes('Motif E2E'),
      'Le motif de rejet doit etre persiste.'
    );
  });

  // --- 5. Resoumission et approbation -------------------------------------
  await step('Resoumission apres correction', async () => {
    const response = await api.post(`${apiUrl}/api/courses/${courseId}/submit-for-review`, {
      headers: authHeader(instructor)
    });
    assertOk(response, 'Un cours rejete doit pouvoir etre resoumis.');
    const resubmitted = await response.json();
    assert(resubmitted.status === 'PendingReview', 'Statut attendu PendingReview apres resoumission.');
    assert(!resubmitted.rejectionReason, 'Le motif de rejet doit etre efface a la resoumission.');
  });

  await step('Un instructeur ne peut pas approuver un cours', async () => {
    const response = await api.post(`${apiUrl}/api/courses/${courseId}/approve`, {
      headers: authHeader(instructor)
    });
    assert(
      response.status() === 403,
      `Attendu 403 pour une approbation par un instructeur, recu ${response.status()}.`
    );
  });

  await step('Approbation par l admin', async () => {
    const response = await api.post(`${apiUrl}/api/courses/${courseId}/approve`, {
      headers: authHeader(admin)
    });
    assertOk(response, 'L approbation admin doit aboutir.');
    const approved = await response.json();
    assert(approved.status === 'Approved', 'Statut attendu Approved.');
    assert(approved.isPublished === true, 'Un cours approuve doit etre publie.');
  });

  // --- 6. Visibilite publique ---------------------------------------------
  await step('Le cours publie est visible dans le catalogue public (API anonyme)', async () => {
    const response = await api.get(`${apiUrl}/api/courses`);
    assertOk(response, 'Le catalogue public doit etre accessible sans authentification.');
    const courses = await response.json();
    assert(
      courses.some((course) => course.id === courseId),
      'Le cours publie doit apparaitre dans le catalogue public.'
    );
  });

  await step('Le cours publie est visible dans l interface web', async () => {
    browser = await chromium.launch({ headless: true });
    const page = await browser.newPage();
    await page.goto(`${baseUrl}/explore?search=${encodeURIComponent(courseTitle)}`);
    await page.locator(`.course-card[data-course-id="${courseId}"]`).waitFor({ timeout: 20000 });
    await page.goto(`${baseUrl}/courses/${courseId}`);
    const heading = page.locator('h1').first();
    await heading.waitFor({ timeout: 20000 });
    assert(
      (await heading.textContent())?.trim() === courseTitle,
      'Le detail du cours publie doit afficher son titre.'
    );
    await page.close();
  });

  // --- 7. Inscription, avis, signalement ----------------------------------
  await step('Inscription de l apprenant au cours publie', async () => {
    const response = await api.post(`${apiUrl}/api/courses/${courseId}/enroll`, {
      headers: authHeader(student)
    });
    assertOk(response, 'L apprenant doit pouvoir s inscrire au cours publie.');
  });

  reviewId = await step('Depot d un avis par l apprenant inscrit', async () => {
    const response = await api.post(`${apiUrl}/api/reviews/course/${courseId}`, {
      headers: authHeader(student),
      data: { rating: 4, comment: `Avis E2E ${runId}` }
    });
    assertOk(response, 'L apprenant inscrit doit pouvoir deposer un avis.');
    return (await response.json()).id;
  });

  await step('Un apprenant ne peut pas signaler son propre avis', async () => {
    const response = await api.post(`${apiUrl}/api/reviews/${reviewId}/report`, {
      headers: authHeader(student),
      data: { reason: 'Auto-signalement interdit.' }
    });
    assert(
      response.status() === 400,
      `Attendu 400 pour un auto-signalement, recu ${response.status()}.`
    );
  });

  await step('Signalement de l avis par un autre utilisateur inscrit', async () => {
    const enroll = await api.post(`${apiUrl}/api/courses/${courseId}/enroll`, {
      headers: authHeader(admin)
    });
    assertOk(enroll, 'Le second utilisateur doit pouvoir s inscrire pour signaler.');

    const response = await api.post(`${apiUrl}/api/reviews/${reviewId}/report`, {
      headers: authHeader(admin),
      data: { reason: `Motif de signalement E2E ${runId}` }
    });
    assert(response.status() === 204, `Attendu 204 pour un signalement, recu ${response.status()}.`);
  });

  await step('Le signalement en double est refuse', async () => {
    const response = await api.post(`${apiUrl}/api/reviews/${reviewId}/report`, {
      headers: authHeader(admin),
      data: { reason: 'Doublon.' }
    });
    assert(response.status() === 409, `Attendu 409 pour un doublon, recu ${response.status()}.`);
  });

  // --- 8. Moderation admin -------------------------------------------------
  await step('L avis signale remonte dans l espace de moderation avec son motif', async () => {
    const response = await api.get(`${apiUrl}/api/admin/reviews`, { headers: authHeader(admin) });
    assertOk(response, 'La liste de moderation doit etre accessible a l admin.');
    const review = (await response.json()).find((item) => item.id === reviewId);
    assert(review, 'L avis depose doit apparaitre dans l espace de moderation.');
    assert(review.courseTitle === courseTitle, 'L avis doit etre rattache au bon cours.');
    assert(review.reports.length === 1, `Un signalement attendu, recu ${review.reports.length}.`);
    assert(
      review.reports[0].reason.includes(`${runId}`),
      'Le motif du signalement doit etre persiste et restitue.'
    );
  });

  await step('Suppression de l avis par l admin', async () => {
    const response = await api.delete(`${apiUrl}/api/admin/reviews/${reviewId}`, {
      headers: authHeader(admin)
    });
    assert(response.status() === 204, `Attendu 204 a la suppression, recu ${response.status()}.`);
    reviewId = null;
  });

  await step('L avis supprime disparait du cours et de la moderation', async () => {
    const moderation = await api.get(`${apiUrl}/api/admin/reviews`, { headers: authHeader(admin) });
    assertOk(moderation, 'La liste de moderation doit rester accessible.');
    assert(
      !(await moderation.json()).some((item) => item.courseTitle === courseTitle),
      'L avis supprime ne doit plus apparaitre en moderation.'
    );

    const courseReviews = await api.get(`${apiUrl}/api/reviews/course/${courseId}`, {
      headers: authHeader(student)
    });
    assertOk(courseReviews, 'Les avis du cours doivent rester accessibles.');
    const payload = await courseReviews.json();
    assert(payload.totalReviews === 0, `Aucun avis attendu, recu ${payload.totalReviews}.`);
    assert(payload.averageRating === 0, 'La moyenne doit repasser a zero.');
  });

  // --- 9. Depublication ----------------------------------------------------
  await step('Depublication par l instructeur', async () => {
    const response = await api.post(`${apiUrl}/api/courses/${courseId}/unpublish`, {
      headers: authHeader(instructor)
    });
    assertOk(response, 'La depublication doit aboutir.');
    assert((await response.json()).isPublished === false, 'Le cours ne doit plus etre publie.');

    const catalog = await api.get(`${apiUrl}/api/courses`);
    assertOk(catalog, 'Le catalogue public doit rester accessible.');
    assert(
      !(await catalog.json()).some((course) => course.id === courseId),
      'Un cours depublie doit disparaitre du catalogue public.'
    );
  });

  console.log(`\nTest E2E publication/moderation reussi (${runId}).`);
} finally {
  await cleanup();
  if (browser) await browser.close();
  await api.dispose();
}

// --- Utilitaires ------------------------------------------------------------

async function login(role) {
  const { email, password } = ACCOUNTS[role];
  const response = await api.post(`${apiUrl}/api/auth/login`, { data: { email, password } });
  assertOk(response, `Le compte ${role} doit pouvoir se connecter.`);
  return (await response.json()).token;
}

function authHeader(token) {
  return { Authorization: `Bearer ${token}` };
}

async function step(label, action) {
  const result = await action();
  console.log(`  ok  ${label}`);
  return result;
}

// Nettoyage best-effort : le test ne doit jamais laisser de donnees derriere lui,
// meme s il a echoue en cours de route.
async function cleanup() {
  try {
    const admin = await login('admin');

    if (reviewId) {
      await api.delete(`${apiUrl}/api/admin/reviews/${reviewId}`, { headers: authHeader(admin) });
    }

    if (courseId) {
      await api.post(`${apiUrl}/api/courses/${courseId}/archive`, { headers: authHeader(admin) });
      const deletion = await api.delete(`${apiUrl}/api/courses/${courseId}?confirm=DELETE`, {
        headers: authHeader(admin)
      });
      if (deletion.status() !== 204) {
        console.error(`  !!  Nettoyage du cours ${courseId} incomplet (${deletion.status()}).`);
      } else {
        console.log('  ok  Nettoyage : cours archive puis supprime.');
      }
    }
  } catch (error) {
    console.error(`  !!  Nettoyage impossible : ${error.message}`);
  }
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

function assertOk(response, message) {
  if (!response.ok()) {
    throw new Error(`${message} (HTTP ${response.status()})`);
  }
}
