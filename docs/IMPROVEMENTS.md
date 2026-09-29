# Pistes d'amélioration UI/UX - EduPlatform

Date : 2026-09-29. Analyse du code existant (`EduPlatform.Web`), hors périmètre paiement.

Chaque item porte une priorité et un coût estimé :
P1 = incohérence ou régression visible, P2 = gain fort, P3 = confort.
Coût : S (< 2 h), M (0,5 - 1 j), L (> 1 j).

## Mise a jour d'etat (2026-09-29)

Les constats ci-dessous proviennent de l'audit initial et plusieurs sont maintenant resolus.
Livres depuis : vignettes et notes sur les cartes/home, categories dynamiques, recherche/filtres/tri
et pagination cote navigateur partageables par URL, footer global, statistiques reelles, image hero
locale, balises SEO/Open Graph, programme visible aux visiteurs, avis repositionnes, distribution
des notes, HTML assaini contre le XSS, YouTube nocookie, reprise/progression video a 90 %, poster,
vitesse, sauts, progression des modules et navigation vers le module suivant.

Les ecarts encore ouverts a prioriser sont : pages legales, tests axe/Lighthouse, sous-titres video, objectifs/prerequis de cours,
stockage objet/CDN/transcodage, decoupage de `app.css`, optimisation SQL et choix editorial pour
les avis/t�moignages. La roadmap d'exploitation reste la reference pour production, Cassandra et CI.

> La pagination SQL serveur du catalogue est livree via `/api/courses/paged` (pageSize borne a 50);
> la recherche, les filtres, le tri et la page restent partageables dans l'URL.
> Les sections detaillees ci-dessous sont conservees comme trace de l'audit initial. En particulier,
> les constats 1.1 a 1.6 et les lots 1, 2 (XSS/YouTube) et 4 sont resolus; ils ne sont pas des taches
> ouvertes. Le backlog courant est uniquement la liste � ecarts encore ouverts � ci-dessus et
> `docs/ROADMAP_STATUS.md`.

---

## 1. Écarts fonctionnels déjà payés mais non exposés

Ces points sont les plus rentables : le backend existe déjà, seul l'affichage manque.

| # | Constat | Fichiers | Prio | Coût |
|---|---------|----------|------|------|
| 1.1 | `Course.ThumbnailUrl` est uploadé par l'instructeur mais **jamais affiché**. `Home.razor`, `Courses.razor` et `CourseCard.razor` utilisent tous un dégradé généré par `GetCoverStyle(category)`. L'instructeur téléverse une image qui n'apparaît nulle part. | `Shared/CourseCard.razor`, `Pages/Home.razor`, `Pages/Courses.razor`, `Pages/CourseDetail.razor` | P1 | S |
| 1.2 | `SearchBar.razor` n'est branché que dans `PublicHeader.razor`, donc **absent de la barre d'outils du catalogue** : une fois sur `/explore`, l'utilisateur voit deux `<select>` mais aucun champ de recherche à côté des filtres, et le terme recherché n'est pas réaffiché dans la page. La recherche n'est pas non plus combinable avec les filtres (elle réinitialise l'URL). | `Layout/PublicHeader.razor`, `Pages/Courses.razor` | P1 | S |
| 1.3 | `Pagination.razor` n'est utilisé que dans `AdminReviews` et `AdminUsers`. Le catalogue public charge **tous** les cours d'un coup. | `Pages/Courses.razor` | P1 | M |
| 1.4 | Les notes et avis existent (`CourseReview`, `StarRating`) mais **n'apparaissent pas** sur les cartes de cours ni sur la home. Un visiteur ne voit la note qu'après avoir ouvert le détail. | `Shared/CourseCard.razor`, `Pages/Home.razor` | P1 | M |
| 1.5 | Les catégories de la home sont **codées en dur** (3 tuiles) et renvoient vers `?category=Informatique` / `?category=Economie`. La table `CourseCategory` structurée existe pourtant. | `Pages/Home.razor` | P1 | S |
| 1.6 | Le bloc « PROGRESS +42% cette semaine » de la home est une **valeur fictive en dur**, affichée à tous les visiteurs. | `Pages/Home.razor` (`art-window`) | P1 | S |

---

## 2. Page d'accueil

### 2.1 Contenu et crédibilité

- **Images externes Unsplash en dur** : 4 balises `<img src="https://images.unsplash.com/...">`. Dépendance réseau tierce, pas de repli si Unsplash est injoignable, aucun `loading="lazy"`, aucune `width`/`height` (donc décalage de mise en page au chargement). À rapatrier dans `wwwroot/img/`. **P1 / S**
- **« 4 derniers cours » comme mise en avant** : `FeaturedCourses` = `OrderByDescending(CreatedAt).Take(4)`. Une vraie mise en avant devrait se baser sur la note moyenne, le nombre d'inscrits, ou un indicateur `IsFeatured` administrable. **P2 / M**
- **Aucune preuve sociale réelle** : la bande « SQL / ML.NET / Kafka / Blazor / Cloud-ready » affiche la stack technique au visiteur, ce qui ne lui parle pas. À remplacer par des témoignages d'apprenants (les avis existent déjà en base). **P2 / M**
- **Statistiques dégradées en « — »** : si `api/courses/stats` échoue, les 4 compteurs affichent un tiret sans explication. Prévoir des valeurs de repli ou masquer la bande. **P3 / S**

### 2.2 Structure et parcours

- **Pas de bandeau « reprendre où vous en étiez »** pour un utilisateur connecté : la home est identique connecté / déconnecté, alors que `AuthState` est déjà injecté dans `Home.razor` mais **jamais utilisé**. C'est le gain de conversion le plus évident. **P1 / M**
- **Aucun bloc « nouveautés » ni « les plus suivis »** : une seule grille de cours sur toute la page. **P2 / M**
- **Le footer est écrit dans `Home.razor`** et n'existe donc que sur la home. Il devrait être dans `MainLayout.razor` pour apparaître sur tout le site. **P2 / S**
- **Pas de page « À propos », « Contact », « CGU », « Confidentialité »** : les liens du footer pointent tous vers des pages applicatives. Bloquant pour une mise en ligne publique (RGPD). **P2 / M**

### 2.3 Technique

- **Pas de balises SEO** : `App.razor` ne contient ni `<meta name="description">`, ni Open Graph, ni `<title>` par page. Le partage sur réseaux sociaux affichera une vignette vide. **P2 / S**
- **`@rendermode InteractiveServer` sur la home** : la page publique la plus visitée ouvre un circuit SignalR pour chaque visiteur anonyme. Un rendu statique avec interactivité ciblée réduirait fortement la charge serveur. **P2 / M**

---

## 3. Présentation des cours

### 3.1 Carte de cours (`CourseCard.razor`)

- **Pas de vignette réelle** (voir 1.1), **pas de note** (voir 1.4).
- **Pas de nom d'instructeur** ni d'avatar : `Course.InstructorId` existe mais n'est pas résolu côté affichage.
- **Pas de nombre d'inscrits** ni de prix, alors que `Course.Price` et `Enrollments` existent.
- **Description non tronquée** : `<p>@Description</p>` sans limite, donc hauteurs de cartes irrégulières dans une même grille. Prévoir un `line-clamp: 3`.
- **Accessibilité** : `<article @onclick>` n'est pas focusable au clavier, pas de `role="button"`, pas de `tabindex`, pas de gestion de la touche Entrée. Le bouton interne « Voir le cours » double le clic du conteneur.
- **Pas d'état visuel « déjà inscrit »** : rien ne distingue un cours suivi d'un cours nouveau.
- **P1 / M** pour l'ensemble.

### 3.2 Catalogue (`Courses.razor`)

- Champ de recherche absent de la barre d'outils (1.2), pas de pagination (1.3).
- **Pas de tri** (récent / mieux noté / plus suivi / durée).
- **Pas de filtre par durée ni par prix**.
- **Pas d'état « squelette »** pendant le chargement : simple texte « Chargement des cours... ».
- **Les filtres ne sont pas dans l'URL** : impossible de partager ou de mettre en favori une recherche filtrée, et le retour arrière du navigateur perd la sélection.
- **Pas de message d'erreur** si l'API répond en échec : on affiche « Aucun cours disponible », ce qui est trompeur.
- **P1 / M**.

### 3.3 Détail du cours (`CourseDetail.razor`, 452 lignes)

- **Ordre des sections illogique** : le bloc « Avis des apprenants » est rendu **avant** la liste des modules et avant le bouton d'inscription. Un visiteur voit les avis avant même de savoir ce que contient le cours.
- **Pas de programme détaillé pour le visiteur non inscrit** : la liste des modules n'est affichée que si `IsEnrolled || IsPreview`. C'est pourtant exactement l'information qui déclenche l'inscription.
- **Pas d'objectifs pédagogiques, prérequis, public visé** : ces champs n'existent même pas sur le modèle `Course`.
- **Pas de distribution des notes** (barres 5/4/3/2/1), seule la moyenne est affichée alors que la roadmap la mentionne.
- **Avis non paginés** : `@foreach (var review in Reviews.Reviews)` affiche tout.
- **Pas d'aperçu vidéo gratuit** du premier module avant inscription.
- **Formulaire d'avis rudimentaire** : `<select>` pour la note au lieu d'étoiles cliquables, alors que `StarRating` existe (en lecture seule uniquement).
- **Le fichier fait 452 lignes** et mélange hero, avis, modules, inscription et paiement : à découper en sous-composants.
- **P1 / L**.

---

## 4. Vidéos et espace d'apprentissage (`Learn.razor`)

### 4.1 Lecteur

- **`<video controls>` natif brut** : aucun contrôle personnalisé, pas de réglage de vitesse, pas de saut +10 s / −10 s, pas de mémorisation du volume.
- **Pas d'attribut `poster`** : le lecteur affiche un rectangle noir avant lecture.
- **Pas de sous-titres** : aucun `<track kind="captions">`, aucun champ pour stocker un fichier VTT. Bloquant pour l'accessibilité.
- **Pas de reprise de lecture** : la position n'est jamais sauvegardée, l'apprenant recommence au début à chaque visite.
- **Pas de progression automatique** : le module n'est marqué terminé que via un bouton manuel. L'événement `timeupdate` / `ended` du lecteur devrait déclencher la complétion à 90 %.
- **Pas d'enchaînement** : aucun bouton « Module suivant », aucune lecture automatique du suivant.
- **Pas de gestion d'erreur** : si l'URL vidéo est cassée, le lecteur reste muet, aucun message.
- **YouTube intégré sans `youtube-nocookie.com`** : `GetYouTubeEmbedUrl` utilise le domaine standard, qui dépose des cookies de suivi avant tout consentement. **Point RGPD.**
- **P1 / L**.

### 4.2 Contenu et mise en page

- **Risque XSS** : `@((MarkupString)SelectedModule.Description)` rend du HTML brut fourni par l'instructeur, sans assainissement. Un instructeur malveillant peut injecter du script chez tous ses apprenants. **À traiter en priorité sécurité.** **P1 / M**
- **Pas de barre de progression du cours** dans la barre latérale : on ne voit ni les modules terminés, ni le pourcentage global.
- **Pas d'indicateur « terminé »** par module dans la liste latérale.
- **Pas de prise de notes ni de marque-pages**.
- **Pas de ressources téléchargeables** (PDF, code source) : la roadmap le prévoit, rien n'est implémenté.
- **Pas de vue mobile** : `learning-layout` est une grille latérale + contenu, sans repli en une colonne ni menu repliable.
- **P2 / M**.

### 4.3 Livraison des médias

- **Vidéos servies directement par l'application** depuis le disque local : pas de CDN, pas de streaming adaptatif (HLS/DASH), pas de transcodage multi-résolutions. Une vidéo de 100 Mo est téléchargée intégralement par chaque apprenant.
- **Pas de protection du lien** : l'URL de la vidéo est directe, donc partageable hors plateforme.
- **Empêche le scale horizontal** : le stockage sur disque local force une seule instance Web.
- **P2 / L** (chantier déjà identifié dans la roadmap).

---

## 5. Transverse

### 5.1 Design system

- **`app.css` : 1536 lignes dans un seul fichier**, sans découpage ni convention de nommage. Classes mélangées : `market-*`, `explore-*`, `course-*`, `btn-*`, `learning-*`.
- **Variables CSS incomplètes** : `:root` définit 9 variables, mais les dégradés, ombres, rayons et espacements sont écrits en dur partout.
- **Duplication de la logique de couleur** : `GetCoverStyle(category)` est réimplémentée à l'identique dans `Home.razor`, `Courses.razor` et `Recommendations.razor`.
- **Pas de mode sombre**.
- **`font-family: 'Segoe UI', sans-serif`** : rendu incohérent hors Windows.
- **P2 / M**.

### 5.2 Accessibilité

- Conteneurs cliquables non focusables (cartes de cours, tuiles de catégories).
- Pas de `aria-label` sur les boutons icône.
- Contraste non vérifié sur les textes clairs des sections hero.
- Pas de gestion du focus après navigation.
- Aucun test axe / Lighthouse dans la CI.
- **P2 / M**.

### 5.3 Responsive

- Aucun `@media` vérifié sur les grilles principales (`market-course-grid`, `CourseList-grid`, `learning-layout`).
- La home enchaîne 6 sections conçues en deux colonnes.
- **P2 / M**.

### 5.4 États et retours

- **Messages de chargement en texte brut** à plusieurs endroits (`<p>Chargement du cours...</p>`) alors que `LoadingState` existe.
- **`Toast` existe mais est peu utilisé** : la plupart des retours passent par `alert-error` / `alert-success` en ligne.
- **Pas de page 404 personnalisée**.
- **P3 / S**.

---

## 6. Ordre d'attaque recommandé

**Lot 1 - Cohérence immédiate (1 à 2 jours, gros effet visuel)**
1. Afficher `ThumbnailUrl` partout, avec repli sur le dégradé actuel (1.1)
2. Ajouter `SearchBar` dans la barre d'outils du catalogue, combinable avec les filtres, le tout dans l'URL (1.2, 3.2)
3. Catégories de la home depuis `CourseCategory` (1.5)
4. Supprimer le « +42% » fictif (1.6)
5. Notes et avis sur les cartes de cours (1.4)
6. Footer déplacé dans `MainLayout` (2.2)

**Lot 2 - Sécurité et conformité (0,5 à 1 jour)**
7. Assainir le HTML des modules (4.2, XSS)
8. `youtube-nocookie.com` (4.1)
9. Images locales au lieu d'Unsplash (2.1)

**Lot 3 - Parcours d'inscription (2 à 3 jours)**
10. Réordonner `CourseDetail` : programme avant avis (3.3)
11. Programme visible pour le visiteur non inscrit (3.3)
12. Bandeau « reprendre » sur la home pour l'utilisateur connecté (2.2)
13. Pagination et tri du catalogue (1.3, 3.2)

**Lot 4 - Expérience vidéo (3 à 5 jours)**
14. Reprise de lecture + complétion automatique à 90 % (4.1)
15. Progression et pastilles « terminé » dans la barre latérale (4.2)
16. Bouton « module suivant » (4.1)
17. `poster`, sous-titres, vitesse de lecture (4.1)

**Lot 5 - Fondations (en continu)**
18. Découpage de `app.css` et variables complètes (5.1)
19. Responsive et accessibilité (5.2, 5.3)
20. Stockage objet / CDN / transcodage (4.3)
