# Plateforme suspendue

Le prefab existant `Assets/Structure/chain.prefab` est installe automatiquement apres la compilation, hors Play et hors mode Prefab.

Place ce prefab dans ta scene, puis selectionne sa racine **chain**. Le composant **Sonic Swing Platform** contient tous les reglages :

- **Nombre de maillons** : ajoute ou retire des anneaux, sans etirer la planche.
- **Longueur Y (metres)** : meme reglage en longueur, arrondie au maillon entier le plus proche.
- **Largeur X (metres)** et **Profondeur Z (metres)** : agrandissent ou retrecissent uniquement la plateforme autour de son centre. Les chaines et l'epaisseur restent identiques, les collisions suivent automatiquement. Dans la vue Scene, tire les quatre points rouges (X) et bleus (Z) sur les bords de la planche. Ctrl+Z annule.
- **Amplitude (degres)** : angle maximal de chaque cote. Zero = immobile.
- **Aller-retour (secondes)** : plus la valeur est petite, plus le mouvement est rapide.
- **Direction X/Z (degres)** : 0 = X local, 90 = Z local. La rotation Y de l'objet oriente l'ensemble.
- **Decalage de depart (degres)** : permet de desynchroniser plusieurs plateformes.
- **Transporter Sonic** : utilise le mecanisme de plateforme mobile deja present dans le jeu.

Dans la vue Scene, active **Gizmos**, selectionne la racine, et tire la poignee bleue **Longueur Y** sous la planche. Des maillons entiers sont ajoutes ou retires. Ctrl+Z annule une modification. Utilise cette poignee au lieu d'etirer Transform Scale Y ; une echelle uniforme redimensionne l'ensemble.

Le bouton **Apercu du balancement** montre le mouvement hors Play. Arreter l'apercu ou deselectionner l'objet remet la plateforme au repos. La boule d'attache reste fixe, les maillons se balancent et la planche reste horizontale.

Les enfants **Attache**, **Maillons** et **floor** sont geres automatiquement. Les reglages se font sur la racine. Le composant Moving Platform Control de floor est volontairement desactive : il fournit au joueur le deplacement calcule par le balancement.

Si l'installation automatique n'a pas eu lieu : **Tools > Sonic FX > Structures > Installer et verifier chain balancante**.
