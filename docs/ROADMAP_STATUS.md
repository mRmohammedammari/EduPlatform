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
| 5. Tests et qualite | 80 % | En cours |
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
protection par role (Student / Instructor / Admin), Â« se souvenir de moi Â», revocation au logout.

---

## Phase 3 - Completion interface web : 100 %

21 pages Razor livrees. Etudiant : profil, progression avec badges, QCM avec revision,
chat IA, recommandations, notifications, certificat. Instructeur : dashboard, analytics,
CRUD cours/modules/QCM, upload thumbnail et video, previsualisation, gestion des inscrits.
Admin : dashboard, utilisateurs et roles, workflow d'approbation, categories, moderation des avis,
export CSV.

8 composants partages : `CourseCard`, `LoadingState`, `AlertMessage`, `StarRating`, `SearchBar`,
`Modal`, `Toast`, `Pagination`.

Lot UI 1-4 livre depuis le premier audit : vignettes et notes sur les cartes, categories dynamiques,
recherche/tri et filtres partageables par URL, pagination SQL serveur, footer global, SEO/Open Graph, accueil
personnalise, programme public, sanitisation HTML, lecteur avec reprise/progression a 90 %,
controles de vitesse/saut, YouTube nocookie et responsive de l'espace d'apprentissage.
Reste distinct : pages legales et tests accessibilite.

---

## Phase 4 - Fonctionnalites avancees : 70 %

### Livre

- Upload de thumbnail (jpg/png/webp) et de video de module (mp4/webm/ogg, 100 Mo max) avec validation
- Lecteur video HTML5 pour les fichiers heberges, iframe pour YouTube
- Notifications in-app : table, service, page dediee, marquage lu/non-lu
- Certificat de reussite consultable en ligne
- Avis apprenants : note, commentaire, moyenne, affichage sur le detail du cours
- Moderation : liste admin paginee, suppression avec confirmation, signalement motive, historique
- Progression persistante par module : reprise de lecture et completion automatique a 90 %
- Inscription Free sans passer par la passerelle de paiement; plans payants encore simules

### Reste a faire

- [ ] Stockage objet / CDN (actuellement disque local, bloque le scale horizontal)
- [ ] Transcodage video et streaming adaptatif (HLS/DASH)
- [ ] Sous-titres (aucun champ de stockage VTT)
- [x] Certificat au format PDF telechargeable + URL de verification publique
- [x] QR code SVG integre au certificat et lie a la verification publique
- [x] Notifications temps reel (SignalR)
- [ ] Ressources telechargeables par cours (PDF, code source)
- [ ] Distribution des notes (barres 5/4/3/2/1) et analytics de feedback instructeur

---

## Phase 5 - Tests et qualite : 80 %

### Livre

- 44 tests automatises (xUnit + EF InMemory) : `AuthServiceTests`, `PaymentGatewayTests`,
  `CourseMediaValidatorTests`, `ReviewsControllerTests`, `AdminReviewsControllerTests`
- Couverture de code mesuree par Coverlet, rapport HTML publie en artefact CI,
  seuil-cliquet applique dans le workflow
- E2E Playwright `smoke-tests.mjs` : catalogue public, recherche, filtres, detail,
      contrat de pagination serveur, parcours etudiant, instructeur et admin, cycle creation/archivage/suppression
- E2E Playwright `publication-moderation.mjs` : brouillon -> rejet motive -> approbation
      -> publication -> inscription Free -> QCM/resultat Cassandra -> analytics/certificat
      -> avis/signalement/moderation -> depublication, avec nettoyage SQL et Cassandra
- Les deux suites E2E tournent en CI contre une stack Docker ephemere
- Audit Axe WCAG 2.1 A/AA automatise en CI sur accueil, catalogue, detail, progression, espace instructeur, dashboard admin, gestion utilisateurs et moderation admin; bloque les violations serious/critical

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
- [ ] Completer l'audit par Lighthouse

---

## Phase 6 - DevOps et deploiement : 85 %

### Livre

- Pipeline GitHub Actions : restore, build Release, tests unitaires avec couverture et seuil,
  rapport de couverture en artefact, validation `docker compose config`, build des images
  API et Web, stack ephemere + suites E2E
- `Dockerfile.api`, `Dockerfile.web`, `docker-compose.yml` complet (SQL Server, Cassandra,
  Kafka, Zookeeper, Redis, API, Web, nginx)
- Kafka : listeners distincts hote/interne Docker, volume persistant nomme et consumer API
- Readiness API Compose sur `/health/details`; liveness ASP.NET sur `/health`
- Reverse proxy nginx avec TLS, redirection HTTP -> HTTPS, `X-Forwarded-*`, HSTS hors Development
- Migrations appliquees au deploiement, scripts de sauvegarde et restauration

### Reste a faire

- [ ] Certificat TLS emis par une autorite reconnue (les certificats actuels sont auto-signes,
      generes par `scripts/nginx/generate-dev-cert.ps1`, dev uniquement)
- [ ] Retirer l'exposition directe des ports 5053 et 5297 en production
- [ ] Publication des images vers un registre et deploiement effectif sur un hebergeur

Procedure detaillee : [PRODUCTION_READINESS.md](PRODUCTION_READINESS.md).

---

## Phase 7 - Monitoring et observabilite : 70 %

### Livre

- Healthchecks Docker SQL Server, Cassandra, Redis, Kafka, API readiness et Web
- `/health/details` verifie SQL par `CanConnectAsync`, Cassandra/Redis/Kafka par TCP
- Logs structures avec correlation par `SessionId`
- `scripts/monitor-health.ps1` avec alertes webhook optionnelles
- Endpoint `/metrics` compatible Prometheus avec compteurs HTTP de base
- Profil Compose Prometheus/Grafana avec datasource et dashboard API provisionnes

### Reste a faire

- [ ] Tracing distribue (OpenTelemetry)
- [ ] Agregation centralisee des logs

---

## Phase 8 - Monetisation : 10 % (gelee)

Le paiement est **simule** et ne doit pas etre utilise en production. Chantier volontairement
mis de cote. Le plan d'integration d'un fournisseur reel est ecrit dans
[PRODUCTION_READINESS.md](PRODUCTION_READINESS.md).

---

## Phase 10 - Optimisations et performance : 45 %

### Livre

- Cache Redis (cours, listes, statistiques) avec invalidation ciblee
- Pagination SQL serveur du catalogue avec recherche, filtres, tri et nombre total de resultats
- Rate limiting global par adresse IP avec réponse 429 et en-tête `Retry-After`

### Reste a faire

- [ ] Optimisation des requetes EF (`AsNoTracking`, projections)
- [ ] Decoupage de `app.css` (2188 lignes), lazy loading des images, mode sombre

---

## Problemes connus

| # | Probleme restant | Gravite |
|---|----------|---------|
| 1 | Anciennes installations Cassandra peuvent avoir `test_results` avec une cle incompatible; migration export/import a planifier avant upgrade | Elevee pour ces bases |
| 2 | Couverture mesuree precedemment a 47,1 %; controllers API et BigData restent peu/non couverts | Moyenne |
| 3 | Pas de pages CGU, confidentialite et contact avant ouverture publique | Moyenne |
| 4 | Le health readiness teste la connectivite TCP mais pas une lecture/ecriture applicative Cassandra/Redis/Kafka | Moyenne |
| 5 | Medias locaux, ports API/Web exposes par Compose et TLS local auto-signe | Elevee en production |
| 6 | Paiement reel gelee par decision produit | Bloquant uniquement si la monetisation est activee |

Analyse complete et plan de correction : [IMPROVEMENTS.md](IMPROVEMENTS.md).

---

## Prochaines actions recommandees

1. **Donnees** : preparer une migration des anciennes tables Cassandra `test_results` si elles existent.
2. **Qualite** : augmenter couverture des controllers API/BigData; integration WebApplicationFactory,
      tests CQL/Redis et accesibilite axe/Lighthouse.
3. **Production** : TLS d'autorite reconnue, fermer ports 5053/5297, publier images et deployer.
4. **Observabilite** : Serilog, Prometheus/Grafana, OpenTelemetry et logs centralises.
5. **Echelle/produit** : stockage objet/CDN, transcodage, pagination serveur, pages legales.
