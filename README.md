# ChatP2P

Messagerie de bureau **pair-à-pair** (sans serveur) inspirée de WhatsApp Desktop — WPF / .NET 10.

## Fonctionnalités
- Discussions directes par IP (TCP, port 5000) + reconnexion automatique aux contacts connus
- Messages : répondre, modifier, supprimer pour moi / pour tous, réactions, message important ★, transférer, infos
- Accusés ✓ / ✓✓ / lus (bleus), indicateur « écrit… », statut en ligne
- Fichiers et images (20 Mo max) : bouton 📎, glisser-déposer, **Ctrl+V** pour coller une image
- Discussions : épingler, sourdine, archiver, bloquer, messages éphémères, filtre « Non lues », recherche
- Recherche dans une discussion (Ctrl+F), export en .txt, vider la discussion
- Historique et réglages **sauvegardés** (`%AppData%\ChatP2P`)
- Thème clair / sombre, fond d'écran personnalisable, séparateurs de jours, sélecteur d'emojis

## Lancer
```
dotnet run --project Chatp2pWPF.csproj
```
Donnez votre IP (Réglages) à votre contact, puis cliquez sur « ＋ » et saisissez la sienne.

## Pistes futures
Groupes, messages vocaux, appels audio/vidéo, chiffrement de bout en bout, découverte automatique du réseau local.
