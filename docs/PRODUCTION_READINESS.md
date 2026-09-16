# Production Readiness

## HTTPS public

1. Placer un reverse proxy (Nginx, Traefik, Azure Application Gateway ou équivalent) devant `web` et `api`.
2. Installer un certificat TLS valide pour les domaines publics.
3. Rediriger HTTP vers HTTPS.
4. Transmettre `X-Forwarded-Proto` et `X-Forwarded-For`.
5. Définir `PublicBaseUrl` avec l'URL HTTPS publique.
6. Vérifier que le cookie de session est `Secure` et `SameSite=Lax`.
7. Ne pas exposer directement les ports internes en production.

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
