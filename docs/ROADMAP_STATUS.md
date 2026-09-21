# ?? EduPlatform - Statut de la Roadmap

**Dernière mise à jour:** 2026-09-18

---

## ?? Vue d'ensemble de la progression

```
Phase 1: Fondations et Sécurité       [??????????] 100% ? TERMINÉE
Phase 2: Auth Client Blazor           [??????????] 100% ? TERMINÉE
Phase 3: Complétion Interface Web     [??????????] 55% ? EN COURS
Phase 4: Fonctionnalités Avancées     [??????????]  0% ? à VENIR
Phase 5: Tests et Qualité             [??????????]  0% ? à VENIR
Phase 6: DevOps et Déploiement        [??????????]  0% ? à VENIR
Phase 7: Monitoring et Observabilité  [??????????]  0% ? à VENIR
Phase 8: Monétisation                 [??????????]  0% ?? OPTIONNEL
Phase 9: Fonctionnalités Pédagogiques [??????????]  0% ?? OPTIONNEL
Phase 10: Optimisations               [??????????]  0% ? à VENIR
Phase 11: Mobile                      [??????????]  0% ?? FUTUR
```

**Progression globale:** 18%

---

## ?? SUCCÈS RéCENTS

- ? **Frontend Blazor complet et testé** (13 pages protégées, connexion/déconnexion/refresh token/"se souvenir de moi")
- ? **Authentification robuste** : JWT + refresh token rotatif (table RefreshTokens), intercepteur HTTP centralisé
- ? **API Backend opérationnelle** sur http://localhost:5053
- ? **Swagger UI accessible** sur http://localhost:5053/swagger
- ? **Kafka topics créés** (5 topics avec configuration compléte)
- ? **Cassandra initialisé** (keyspace + 4 tables)
- ? **Docker infrastructure** fonctionnelle (Kafka, Cassandra, Redis, Zookeeper)
- ? **Documentation compléte** (11 guides créés)
- ? **Scripts d'initialisation** testés et validés

---

## ? Phase 1: Fondations et Sécurité (90% - EN COURS)

**Durée estimée:** 2-3 semaines  
**Priorité:** ?? CRITIQUE  
**Statut:** ? Presque terminée - API opérationnelle, Frontend — démarrer

### 1.1 Sécurité et Configuration

- [x] **Gestion des secrets**
  - [x] Créer .gitignore complet
  - [x] Créer template appsettings.template.json
  - [x] Créer fichier .env.example
  - [ ] Configurer User Secrets dans l'API (optionnel - clés par défaut dans appsettings.json)
  - [x] Documenter le processus de configuration des secrets (STARTUP.md)
  - [x] Tester la configuration avec des secrets locaux

- [x] **Variables d'environnement**
  - [x] Créer fichier .env.example
  - [x] Documenter toutes les variables requises
  - [x] Configurer profil Development (launchSettings.json)

### 1.2 Infrastructure et Scripts

- [x] ? **Cassandra**
  - [x] Créer script d'initialisation keyspace (scripts/cassandra/init.cql)
  - [x] Créer script de création tables (user_activity, test_results, chat_messages, chatbot_sessions, course_progress)
  - [x] Script de seed data pour tests (scripts/cassandra/seed-data.cql)
  - [x] Documentation compléte (docs/CASSANDRA_SETUP.md)
  - [x] Tester les scripts d'initialisation ? VALIDÉ
  - [x] Keyspace initialisé et opérationnel

- [x] ? **Kafka**
  - [x] Script de création des topics (scripts/kafka/init-topics.sh)
  - [x] Script PowerShell pour Windows (scripts/kafka/init-topics.ps1) ? TESTé
  - [x] Configuration des partitions et réplication
  - [x] Documentation compléte (docs/KAFKA_SETUP.md)
  - [x] Tester la création des topics ? 5 topics créés avec succès
  - [x] Topics opérationnels : user-activity-events, test-results-events, notifications-events, course-enrollment-events, chatbot-interaction-events
  - [ ] ?? Consumer Kafka à corriger (timeout au démarrage - temporairement désactivé)

- [x] **SQL Server**
  - [x] Scripts de seed data (scripts/sqlserver/seed-data.sql)
  - [x] Exécuter les migrations EF Core (4 migrations appliquées, dont AddRefreshTokens)
  - [x] Insérer les données de seed
  - [ ] Scripts de backup/restore
  - [x] Documentation des migrations (DATABASE_SETUP.md)

- [x] ? **Docker**
  - [x] Script de démarrage complet (scripts/init-platform.ps1)
  - [x] Docker Compose opérationnel ? 4 conteneurs actifs
  - [x] Infrastructure testée : Cassandra, Kafka, Zookeeper, Redis
  - [ ] Configurer les healthchecks (amélioration optionnelle)
  - [ ] Ajouter API et Web au docker-compose (Phase 2)

### 1.3 Documentation de base

- [x] ? README.md principal
- [x] ? Guide de démarrage rapide
- [x] ? Architecture technique détaillée
- [x] ? Documentation API compléte
- [x] ? Guides Cassandra et Kafka
- [x] ? Guide de tests
- [x] ? Guide de déploiement
- [x] ? Guide de sécurité
- [x] ? Roadmap détaillée

---

## ? Phase 2: Authentification Client Blazor (100% - TERMINÉE)

**Durée estimée:** 1 semaine  
**Priorité:** ?? HAUTE

### 2.1 Système d'authentification

- [x] **AuthenticationStateProvider**
  - [x] Créer AuthStateService (Équivalent CustomAuthStateProvider)
  - [x] Gestion du token JWT dans localStorage/sessionStorage
  - [x] Rafraîchissement automatique du token (refresh token rotatif, 30 jours, table RefreshTokens)
  - [x] Gestion de l'expiration (IsTokenExpired + refresh silencieux à l'initialisation)

- [x] **Services d'authentification**
  - [x] AuthStateService côté Blazor
  - [x] Intercepteur HTTP pour ajouter JWT (HttpClientFactoryExtensions.CreateAuthorizedClient, utilisé par toutes les pages protégées)
  - [x] Gestion des erreurs 401/403 (redirection vers /login, révocation du refresh token au logout)

- [x] **Protection des routes**
  - [x] Vérification IsAuthenticated/Role par page + redirection automatique
  - [x] Redirections automatiques vers /login
  - [x] Gestion des rôles (Student, Instructor, Admin)

### 2.2 Interface utilisateur

- [x] Bouton de déconnexion (avec révocation serveur du refresh token)
- [x] Affichage profil utilisateur dans le header/menu
- [x] Persistance de la session (testée après reload complet du navigateur)
- [x] "Se souvenir de moi" (localStorage si coché, sessionStorage sinon)

---

## ?? Phase 3: Complétion Interface Web (80% - EN COURS)

**Durée estimée:** 2-3 semaines  
**Priorité:** ?? HAUTE

### 3.1 Navigation et Layout — 100% ?
- [x] NavMenu remplacé (PublicHeader/NavSidebar), plus de Counter/Weather
- [x] Liens Cours, Profil, Chat, Tests, Activité, Notifications
- [x] Menu différencié par rôle (Student/Instructor/Admin)
- [x] Indicateur utilisateur connecté (nom + avatar dans le header)

### 3.2 Pages Étudiants — ~80%
- [x] Page Profil : infos perso, historique des cours, statistiques
- [x] Page Profil : Édition du profil (prénom/nom + changement de mot de passe)
- [x] Page Progression : stats agrégées par cours
- [x] Page Progression : graphiques, badges/achievements
- [x] Page Tests/QCM : liste, passage, résultats
- [x] Page Tests/QCM : révision détaillée des réponses (bonne réponse mise en surbrillance)
- [x] Page Chat IA : interface compléte
- [x] Page Chat IA : historique persistant par utilisateur/session
- [x] Page Recommandations : affichage cours recommandés
- [x] Page Recommandations : explication du pourquoi, filtres/tri

### 3.3 Pages Instructeurs — ~90%
- [x] Dashboard Instructeur : vue d'ensemble des cours créés
- [x] Dashboard Instructeur : analytics avancées (inscriptions, tentatives, score moyen, taux de réussite)
- [x] Création/édition de cours : formulaire, modules, QCM (onglets), workflow de validation admin
- [x] Création/édition de cours : upload de thumbnail (fichier réel jpg/png/webp) avec aperçu
- [x] Création/édition de cours : prévisualisation complète du cours
- [x] Gestion des Étudiants : liste des inscrits avec progression + messagerie (notifications)

### 3.4 Pages Administrateur — ~85%
- [x] Dashboard Admin : métriques globales + cours en attente de validation
- [x] Dashboard Admin : utilisateurs actifs, inscriptions récentes et alertes opérationnelles (revenus non applicable)
- [x] Gestion Utilisateurs : liste, modification des rôles, recherche/filtres, suspension/réactivation
- [x] Gestion Cours : workflow d'approbation (soumission ? validation admin ? publication)
- [x] Gestion Cours : modération et catalogue de catégories structurées

### 3.5 Composants Réutilisables — 90%
- [x] CourseCard partagé entre l'exploration et les recommandations
- [x] LoadingState, AlertMessage et StarRating partagés
- [x] SearchBar partagé avec soumission clavier et filtrage par query string
- [x] Modal de confirmation réutilisable pour les actions administratives
- [x] Toast réutilisable pour les retours d'action
- [ ] Pagination

---

## ?? Prochaines Actions Recommandées

### Priorité HAUTE — Combler les vrais manques de la Phase 3

1. **Instructeurs : previsualisation complete des cours**
   - Voir le cours comme un apprenant avant publication

2. **Admin : moderation et gestion des categories/tags**

3. **Composants réutilisables** (90%)
  - Terminé : CourseCard, LoadingState, AlertMessage, StarRating et SearchBar
  - Terminé : Modal de confirmation
  - Restant : Pagination

4. **Phase 4 : évaluations de cours**
  - Notes, commentaires et modération des avis

### Semaine prochaine

5. **Phase 4** : upload de médias (thumbnails/vidéos), système d'Évaluation des cours
6. **Phase 5** : tests unitaires (couverture actuelle : `EduPlatform.Tests` limité — quelques tests de paiement)

---

## ?? Métriques de Progression

| Catégorie | Items Complétés | Items Totaux | % |
|-----------|----------------|--------------|---|
| **Documentation** | 10 | 10 | 100% |
| **Scripts Infrastructure** | 7 | 10 | 70% |
| **Configuration** | 3 | 6 | 50% |
| **Tests** | 0 | 0 | 0% |
| **Features** | 0 | 0 | 0% |

---

## ?? Objectifs par Sprint

### Sprint 1 (Semaine du 2026-08-28) - ACTUEL
- [x] Créer toute la documentation
- [x] Créer les scripts d'infrastructure
- [ ] Configurer les secrets
- [ ] Initialiser la plateforme
- [ ] Tester l'infrastructure

### Sprint 2 (Semaine du 2026-09-04)
- [ ] Compléter Phase 1 — 100%
- [ ] Commencer Phase 2 (Auth Blazor)
- [ ] Créer AuthenticationStateProvider
- [ ] Mettre à jour NavMenu

### Sprint 3 (Semaine du 2026-09-11)
- [ ] Compléter Phase 2
- [ ] Commencer Phase 3 (Pages Web)
- [ ] Créer page Profil
- [ ] Créer page Progression

---

## ?? Problèmes Connus

*Aucun problème identifié pour le moment*

---

## ?? Notes et Idées

### Améliorations Futures
- Ajouter des healthchecks dans docker-compose
- Créer des scripts de backup automatiques
- Ajouter des tests d'intégration pour l'infrastructure
- Créer un dashboard de monitoring

### Décisions Techniques
- ? Utiliser BCrypt pour le hashing de mots de passe
- ? JWT avec expiration 24h
- ? Cassandra pour les time-series data
- ? Kafka pour l'event streaming

---

## ?? Support

Pour toute question sur la roadmap ou la progression:
- Consulter [ROADMAP.md](ROADMAP.md) pour les détails complets
- Consulter la documentation dans `docs/`
- Créer une issue sur GitHub

**Dernière mise à jour:** 2026-08-28
