# Taches restantes EduPlatform

## Priorite 1 - Fonctionnement de base

- [x] Verifier les migrations et les donnees SQL Server
- [x] Appliquer la migration Cassandra `002_add_activity_session_id.cql`
- [x] Corriger et redemarrer Kafka
- [x] Valider le parcours inscription -> contenu -> progression -> test
- [x] Bloquer l'acces au contenu si l'utilisateur n'est pas inscrit
- [x] Ajouter des donnees de cours, modules, videos et questions de test

## Priorite 2 - Gestion pedagogique

- [x] Dashboard professeur
- [x] Creation et modification des cours
- [x] Creation et modification des modules
- [x] Gestion du texte pedagogique
- [x] Upload et stockage des videos
- [x] Gestion des questions et reponses
- [x] Publication des cours
- [x] Archivage doux des cours
- [x] Restauration des cours archivés
- [x] Suppression physique contrôlée des cours archivés

## Priorite 3 - Suivi et administration

- [x] Dashboard administrateur
- [x] Gestion des utilisateurs et des roles
- [x] Analytics par cours et par etudiant
- [x] Historique complet des sessions
- [x] Notifications in-app
- [x] Export CSV des rapports de cours

## Priorite 4 - Qualite et securite

- [x] Tests unitaires de base
- [x] Smoke tests d'integration API/Web
- [x] Tests d'integration automatises avec environnement ephemere Docker
- [x] Healthchecks SQL Server, Cassandra, Redis et Kafka
- [x] Logs structures et correlation par SessionId
- [x] Retirer les secrets de `appsettings.json`
- [x] Remplacer les cles JWT de developpement en production
- [x] Cookie de session Secure en HTTPS
- [ ] HTTPS complet pour le déploiement public
- [x] Gerer proprement les erreurs 401 et 403

## Priorite 5 - Mise en production

- [x] Ajouter API et Web au `docker-compose.yml`
- [x] SQL Server conteneurisé pour Docker
- [x] Configurer les migrations au deploiement
- [x] Configurer sauvegardes et restauration
- [x] Pipeline CI/CD build, tests et images Docker
- [x] Monitoring health et alertes webhook optionnelles
- [ ] Remplacer le paiement simule par un fournisseur reel
- [x] Certificat de réussite consultable

## Deja realise

- [x] Authentification JWT et session Web
- [x] Catalogue et detail des cours
- [x] Inscription aux cours
- [x] Contenu ecrit et video par URL
- [x] Progression et historique d'activite
- [x] Session utilisateur et cookie de session
- [x] Tests QCM
- [x] Recommandations ML.NET
- [x] Cache Redis
- [x] Interface chatbot, volontairement repoussee
