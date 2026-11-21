# ChatP2P Desktop — Clone WhatsApp Desktop en P2P (.NET 10 / WPF)

Messagerie de bureau **pair-à-pair sans serveur** inspirée de WhatsApp Desktop.

## ✨ Fonctionnalités incluses

### Interface WhatsApp Desktop (4 zones)
- **Rail de navigation gauche** : Discussions 💬, Appels 📞, Statuts / Actus ⭕, Nouveau groupe 👥, Découverte LAN 📡, Messages importants ⭐, Archivées 🗄, Bascule Thème 🌓, Réglages ⚙ et Avatar utilisateur.
- **Panneau de gauche** : barre de recherche, 4 filtres rapides (`Toutes`, `Non lues`, `Favoris`, `Groupes`), liste des contacts/groupes avec point vert en ligne, heure et badge non lus, barre de connexion rapide IP + Port.
- **Zone centrale** : en-tête avec avatar/statut/boutons d'appels, bandeau de message épinglé 📌, recherche dans la conversation (`Ctrl+F`), séparateurs de jours, bulles riches, lecteur vocal intégré, sondages interactifs, barre d'enregistrement vocal.
- **Panneau droit pliable (`ℹ Infos du contact`)** : grand avatar, bio, IP, code de sécurité P2P, boutons rapides Audio/Vidéo/Recherche, membres du groupe, interrupteurs (`Favori`, `Épingler`, `Sourdine`, `Éphémères`, `Bloquer`), médias et fichiers partagés, export `.txt`, vider.

### Messagerie & Interactions
- Messages texte P2P directs, groupes multi-participants (`👥`)
- Répondre avec citation, modifier, supprimer pour moi / pour tous
- Réactions emoji (👍❤️😂😮😢🙏🔥🎉), épingler un message en haut, transférer, marquer important ★, infos du message
- Messages éphémères (auto-suppression 1 min)
- Accusés de réception ✓ / ✓✓ / bleus et indicateur « en train d'écrire… »
- **Messages vocaux (🎤)** : enregistrement micro natif Windows (`winmm.dll`) + lecteur intégré (`▶/⏸`, barre de progression, vitesse `1x / 1.5x / 2x`)
- **Sondages interactifs (📊)** : création et votes en temps réel avec pourcentages
- **Appels Audio & Vidéo (📞 / 📹)** : fenêtre d'appel dédiée (sonnerie, accepter/refuser, chrono, muet, caméra, haut-parleur) et historique dans l'onglet Appels
- **Statuts / Stories 24h (⭕)** : publication texte/image avec couleur et visionneuse plein cadre
- **Fichiers (📎, jusqu'à 50 Mo)** : Photos/Vidéos, Documents, Glisser-déposer, `Ctrl+V` pour coller une image, Capture d'écran intégrée (`📷`)

### Réseau & Sauvegarde
- P2P TCP (port configurable, défaut `5000`) + découverte automatique sur le réseau local (UDP `5001`)
- Reconnexion automatique aux contacts connus au démarrage
- Sauvegarde locale persistante dans `%AppData%\ChatP2P` (discussions, réglages, appels, statuts)

## 🚀 Lancer le projet
```powershell
dotnet run --project Chatp2pWPF.csproj
```
Pour tester en local sur le même PC ou sur 2 PCs : ouvrez 2 instances (avec un port différent si même PC, ou l'IP LAN entre deux PCs) et cliquez sur **Connecter** ou **📡 Scanner le LAN**.

## 📤 Pousser sur GitHub
Double-cliquez sur `push-github.bat` ou exécutez dans le dossier :
```powershell
git add -A
git commit -m "Votre message"
git push -u origin master
```
