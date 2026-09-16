# Production Readiness

## HTTPS public

Statut : mis en place localement via un reverse proxy Nginx (voir `docker-compose.yml`, service `nginx`, et `nginx/nginx.conf`).

1. Placer un reverse proxy (Nginx, Traefik, Azure Application Gateway ou équivalent) devant `web` et `api`.
2. Installer un certificat TLS valide pour les domaines publics.
3. Rediriger HTTP vers HTTPS.
4. Transmettre `X-Forwarded-Proto` et `X-Forwarded-For`.
5. Définir `PublicBaseUrl` avec l'URL HTTPS publique.
6. Vérifier que le cookie de session est `Secure` et `SameSite=Lax`.
7. Ne pas exposer directement les ports internes en production.

### Passage en production réelle

- Remplacer `nginx/certs/eduplatform.crt` et `eduplatform.key` (générés par `scripts/nginx/generate-dev-cert.ps1`, auto-signés, dev uniquement) par un certificat émis par une autorité reconnue (Let's Encrypt via certbot, ou certificat fourni par le cloud provider).
- Retirer l'exposition directe des ports `5053` (api) et `5297` (web) dans `docker-compose.yml` en production ; seul `nginx` (443/80) doit être exposé publiquement.
- `EduPlatform.API/Program.cs` et `EduPlatform.Web/Program.cs` appliquent déjà `UseForwardedHeaders` (avec `KnownNetworks`/`KnownProxies` vidés car le proxy est sur le réseau Docker interne) et `UseHsts` hors environnement Development.

## Paiement réel

1. Choisir un fournisseur compatible avec le pays et la devise.
2. Créer un service `IPaymentGateway` côté API.
3. Créer une session de paiement côté serveur, sans recevoir ni stocker le numéro de carte.
4. Rediriger l'utilisateur vers la page hébergée du fournisseur.
5. Valider le paiement avec un webhook signé.
6. Rendre le webhook idempotent avec un identifiant de transaction unique.
7. Créer l'inscription uniquement après confirmation serveur.
8. Enregistrer transaction, montant, devise et statut dans une table dédiée.
9. Tester les scénarios succès, échec, annulation, remboursement et webhook répété.

Le paiement actuel reste volontairement simulé et ne doit pas être utilisé en production.
