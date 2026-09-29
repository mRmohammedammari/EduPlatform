# Statut de la Roadmap - EduPlatform

**Derniere mise a jour : 2026-09-29**
**Methode :** relecture du code, des tests et de la CI (et non des cases cochees precedentes).
Le document precedent, date du 2026-09-18, sous-estimait fortement l'avancement reel.

---

## Vue d'ensemble

| Phase | Avancement | Statut |
|-------|-----------|--------|
| 1. Fondations et securite | 100 % | Terminee |
| 2. Authentification client Blazor | 100 % | Terminee |
| 3. Completion interface web | 100 % | Terminee |
| 4. Fonctionnalites avancees | 70 % | En cours |
| 5. Tests et qualite | 75 % | En cours |
| 6. DevOps et deploiement | 85 % | En cours |
| 7. Monitoring et observabilite | 45 % | En cours |
| 8. Monetisation | 10 % | Gelee (decision produit) |
| 9. Pedagogie avancee | 0 % | Optionnelle |
| 10. Optimisations et performance | 25 % | A venir |
| 11. Mobile | 0 % | Futur |

**Progression globale (phases 1 a 7, perimetre MVP) : environ 85 %.**

---

## Phase 1 - Fondations et securite : 100 %

Livre : `.gitignore`, `appsettings.template.json`, `.env.example`, secrets hors du depot,
scripts d'initialisation Cassandra / Kafka / SQL Server, `init-platform.ps1`, backup/restore,
4 migrations EF appliquees, docker-compose complet avec healthchecks, 11 guides de documentation.

---

## Phase 2 - Authentification client Blazor : 100 %

Livre : `AuthStateService`, JWT + refresh token rotatif (table `RefreshTokens`, 30 jours),
rafraichissement silencieux, `CreateAuthorizedClient`, gestion 401/403 avec redirection,
protection par role (Student / Instructor / Admin), « se souvenir de moi », revocation au logout.

---

## Phase 3 - Completion interface web : 100 %

21 pages Razor livrees. Etudiant : profil, progression avec badges, QCM avec revision,
chat IA, recommandations, notifications, certificat. Instructeur : dashboard, analytics,
CRUD cours/modules/QCM, upload thumbnail et video, previsualisation, gestion des inscrits.
Admin : dashboard, utilisateurs et roles, workflow d'approbation, categories, moderation des avis,
export CSV.

8 composants partages : `CourseCard`, `LoadingState`, `AlertMessage`, `StarRating`, `SearchBar`,
`Modal`, `Toast`, `Pagination`.

> Reserve : plusieurs de ces composants sont sous-utilises et des donnees deja disponibles
> ne sont pas affichees (vignettes, notes, pagination du catalogue).
> Detail et priorisation dans [IMPROVEMENTS.md](IMPROVEMENTS.md).

---

## Phase 4 - Fonctionnalites avancees : 70 %

### Livre

- Upload de thumbnail (jpg/png/webp) et de video de module (mp4/webm/ogg, 100 Mo max) avec validation
- Lecteur video HTML5 pour les fichiers heberges, iframe pour YouTube
- Notifications in-app : table, service, page dediee, marquage lu/non-lu
- Certificat de reussite consultable en ligne
- Avis apprenants : note, commentaire, moyenne, affichage sur le detail du cours
- Moderation : liste admin paginee, suppression avec confirmation, signalement motive, historique

### Reste a faire

- [ ] Stockage objet / CDN (actuellement disque local, bloque le scale horizontal)
- [ ] Transcodage video et streaming adaptatif (HLS/DASH)
- [ ] Sous-titres (aucun champ de stockage VTT)
- [ ] Certificat au format PDF telechargeable + verification par QR code
- [ ] Notifications temps reel (SignalR) et preferences de notification
- [ ] Ressources telechargeables par cours (PDF, code source)
- [ ] Distribution des notes (barres 5/4/3/2/1) et analytics de feedback instructeur

---

## Phase 5 - Tests et qualite : 75 %

### Livre

- 24 tests automatises (xUnit + EF InMemory) : `AuthServiceTests`, `PaymentGatewayTests`,
  `CourseMediaValidatorTests`, `ReviewsControllerTests`, `AdminReviewsControllerTests`
- Couverture de code mesuree par Coverlet, rapport HTML publie en artefact CI,
  seuil-cliquet applique dans le workflow
- E2E Playwright `smoke-tests.mjs` : catalogue public, recherche, filtres, detail,
  parcours etudiant, instructeur et admin, cycle creation/archivage/suppression
- E2E Playwright `publication-moderation.mjs` : cycle brouillon -> soumission -> rejet motive
  -> resoumission -> approbation -> publication -> visibilite publique -> avis -> signalement
  -> moderation -> depublication, avec nettoyage garanti
- Les deux suites E2E tournent en CI contre une stack Docker ephemere

### Etat de la couverture (2026-09-29)

| Assembly | Lignes |
|----------|--------|
| EduPlatform.Core | 96,1 % |
| EduPlatform.Data | 66,0 % |
| EduPlatform.API | 7,5 % |
| EduPlatform.BigData | 0 % |
| **Global** | **47,1 %** (branches 25,0 %) |

### Reste a faire

- [ ] Atteindre l'objectif de 80 % : l'essentiel de l'ecart porte sur les controllers
      `EduPlatform.API` (Courses, Admin, Progress, Tests, Certificates, Chatbot) et sur
      `EduPlatform.BigData` (producers/consumers Kafka, analytics)
- [ ] Tests d'integration avec `WebApplicationFactory` (aujourd'hui les tests controllers
      instancient les controllers directement)
- [ ] Tests des repositories Cassandra et du `CacheService`
- [ ] Tests d'accessibilite (axe/Lighthouse) dans la CI

---

## Phase 6 - DevOps et deploiement : 85 %

### Livre

- Pipeline GitHub Actions : restore, build Release, tests unitaires avec couverture et seuil,
  rapport de couverture en artefact, validation `docker compose config`, build des images
  API et Web, stack ephemere + suites E2E
- `Dockerfile.api`, `Dockerfile.web`, `docker-compose.yml` complet (SQL Server, Cassandra,
  Kafka, Zookeeper, Redis, API, Web, nginx)
- Reverse proxy nginx avec TLS, redirection HTTP -> HTTPS, `X-Forwarded-*`, HSTS hors Development
- Migrations appliquees au deploiement, scripts de sauvegarde et restauration

### Reste a faire

- [ ] Certificat TLS emis par une autorite reconnue (les certificats actuels sont auto-signes,
      generes par `scripts/nginx/generate-dev-cert.ps1`, dev uniquement)
- [ ] Retirer l'exposition directe des ports 5053 et 5297 en production
- [ ] Publication des images vers un registre et deploiement effectif sur un hebergeur

Procedure detaillee : [PRODUCTION_READINESS.md](PRODUCTION_READINESS.md).

---

## Phase 7 - Monitoring et observabilite : 45 %

### Livre

- Healthchecks SQL Server, Cassandra, Redis et Kafka, exposes sur `/health`
- Logs structures avec correlation par `SessionId`
- `scripts/monitor-health.ps1` avec alertes webhook optionnelles

### Reste a faire

- [ ] Serilog (le README l'annonce, il n'est pas installe)
- [ ] Metriques Prometheus et tableau de bord Grafana
- [ ] Tracing distribue (OpenTelemetry)
- [ ] Agregation centralisee des logs

---

## Phase 8 - Monetisation : 10 % (gelee)

Le paiement est **simule** et ne doit pas etre utilise en production. Chantier volontairement
mis de cote. Le plan d'integration d'un fournisseur reel est ecrit dans
[PRODUCTION_READINESS.md](PRODUCTION_READINESS.md).

---

## Phase 10 - Optimisations et performance : 25 %

### Livre

- Cache Redis (cours, listes, statistiques) avec invalidation ciblee

### Reste a faire

- [ ] Rate limiting (aucun `AddRateLimiter` dans `Program.cs`)
- [ ] Pagination cote serveur du catalogue (tous les cours sont renvoyes d'un bloc)
- [ ] Optimisation des requetes EF (`AsNoTracking`, projections)
- [ ] Decoupage de `app.css` (1536 lignes), lazy loading des images, mode sombre

---

## Problemes connus

| # | Probleme | Gravite |
|---|----------|---------|
| 1 | `Learn.razor` rend le contenu des modules via `MarkupString` sans assainissement : un instructeur peut injecter du script chez ses apprenants (XSS stocke) | Elevee |
| 2 | `Course.ThumbnailUrl` est uploade mais affiche nulle part | Moyenne |
| 3 | Integration YouTube via `youtube.com` et non `youtube-nocookie.com` : cookies de suivi avant consentement | Moyenne (RGPD) |
| 4 | Images de la page d'accueil chargees depuis Unsplash en dur, sans repli | Moyenne |
| 5 | Le catalogue public charge tous les cours sans pagination | Moyenne |
| 6 | Pas de page CGU / confidentialite / contact | Moyenne (mise en ligne publique) |
| 7 | Couverture de code a 45 % contre 80 % vises | Moyenne |

Analyse complete et plan de correction : [IMPROVEMENTS.md](IMPROVEMENTS.md).

---

## Prochaines actions recommandees

1. **Securite** : assainir le HTML des modules (probleme 1), passer a `youtube-nocookie.com`
2. **Coherence d'affichage** : lot 1 de [IMPROVEMENTS.md](IMPROVEMENTS.md) (vignettes, notes
   sur les cartes, categories dynamiques, recherche dans le catalogue, footer global)
3. **Couverture** : tests des controllers `EduPlatform.API` pour remonter le seuil-cliquet
4. **Mise en ligne** : certificat TLS reconnu et fermeture des ports directs
5. **Medias** : stockage objet / CDN, prealable au scale horizontal
