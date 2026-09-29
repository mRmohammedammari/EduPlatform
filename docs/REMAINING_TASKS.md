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
- [x] Restauration des cours archiv�s
- [x] Suppression physique contr�l�e des cours archiv�s

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
- [x] Tests E2E de publication et moderation avec donnees persistantes
      (`scripts/e2e/publication-moderation.mjs`, executes en CI)
- [x] Mesure de la couverture de code (Coverlet + rapport CI + seuil-cliquet)
- [ ] Porter la couverture de 47 % a 80 % (manque surtout les controllers `EduPlatform.API`
      et `EduPlatform.BigData`)
- [x] Healthchecks SQL Server, Cassandra, Redis et Kafka
- [x] Logs structures et correlation par SessionId
- [x] Retirer les secrets de `appsettings.json`
- [x] Remplacer les cles JWT de developpement en production
- [x] Cookie de session Secure en HTTPS
- [x] HTTPS complet pour le d�ploiement public (reverse proxy Nginx + TLS)
- [x] Gerer proprement les erreurs 401 et 403

## Priorite 5 - Mise en production

- [x] Ajouter API et Web au `docker-compose.yml`
- [x] SQL Server conteneuris� pour Docker
- [x] Configurer les migrations au deploiement
- [x] Configurer sauvegardes et restauration
- [x] Pipeline CI/CD build, tests et images Docker
- [x] Monitoring health et alertes webhook optionnelles
- [ ] Remplacer le paiement simule par un fournisseur reel *(gele, decision produit)*
- [x] Certificat de r�ussite consultable
- [ ] Certificat TLS emis par une autorite reconnue (les certificats nginx actuels sont
      auto-signes, dev uniquement) - **necessite un nom de domaine public**
- [ ] Retirer l'exposition directe des ports 5053 et 5297 en production
- [ ] Stockage objet / CDN pour les medias (prealable au scale horizontal)

## Priorite 6 - Qualite d'affichage

Analyse detaillee et priorisation : [IMPROVEMENTS.md](IMPROVEMENTS.md).

- [ ] Assainir le HTML des modules rendu par `MarkupString` (XSS stocke) - **securite**
- [ ] Passer l'integration YouTube sur `youtube-nocookie.com` - **RGPD**
- [ ] Afficher `ThumbnailUrl` sur les cartes, le catalogue et la home
- [ ] Afficher les notes et avis sur les cartes de cours
- [ ] Categories de la page d'accueil depuis `CourseCategory` au lieu du code en dur
- [ ] Retirer la statistique fictive « +42% » de la page d'accueil
- [ ] Rapatrier les images Unsplash en local
- [ ] Champ de recherche et pagination dans le catalogue
- [ ] Deplacer le footer dans `MainLayout`
- [ ] Pages CGU, confidentialite et contact
- [ ] Reprise de lecture video et completion automatique a 90 %

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
