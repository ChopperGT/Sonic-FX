# Spring large

Prefab : Assets/Structure/SpringLarge/Spring_Large.prefab.

Glisse ce prefab dans la Scene et selectionne son objet racine.

- Etire/reduis Transform > Scale > X : le nombre d'emplacements change automatiquement, sans etirer les etoiles.
- Les poignees orange aux deux extremites et Largeur de base permettent aussi de regler la largeur.
- Emplacements actifs indique le nombre obtenu, entre 1 et 32. La largeur effective avance par emplacements entiers.
- Y/Z modifient normalement la hauteur et la profondeur. Tourne l'objet pour changer la direction de propulsion.
- Force de propulsion, vitesse additive, verrouillage des commandes et duree reprennent Spring_Proprieties et le vrai code Spring du joueur.
- Duree et Compression reglent l'animation de rebond. Le son et son mixer sont copies du Spring original.

La plaque est recomposee avec les sections du modele fourni et conserve ses UV. Les diffuse, specular et emission fournis sont affectes aux deux materiaux ; la derniere etoile jaune apparait lors de l'activation.
Chaque emplacement possede son propre point de lancement et sa cible de homing. Les emplacements retires sont caches et leurs collisions/cibles desactivees. Ils sont reutilises si on agrandit de nouveau.
Une protection empeche deux emplacements voisins de lancer Sonic deux fois au meme instant. Le Spring existant reste inchange.
Les modifications sont independantes entre exemplaires, enregistrables et annulables avec Ctrl+Z.

Installation automatique apres compilation hors Play et hors mode Prefab.
Menu de secours : Tools > Sonic FX > Structures > Installer et verifier Spring large.
