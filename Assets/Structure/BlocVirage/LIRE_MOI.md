# Bloc de virage Green Hill

Un bloc plein à sol plat, dont le contour reste modifiable. Aucun plugin à installer.

1. Attendre la compilation Unity, hors Play.
2. Glisser `Bloc_Virage_GreenHill.prefab` dans le niveau depuis `Assets/Structure/BlocVirage`.
   Autre accès : `GameObject > Sonic FX > Bloc virage editable`.
3. Sélectionner le bloc et activer les Gizmos dans Scene.
4. Cliquer sur un point orange puis déplacer ses flèches X/Z pour modifier le contour.
5. Dans l'Inspector, régler `Arrondi de ce coin`. Zéro garde un coin net.
6. Cliquer sur un petit `+` pour ajouter un point. `Supprimer ce point` retire le point sélectionné.
7. Les poignées cyan et les champs `Largeur X / Longueur Z` redimensionnent le bloc.
8. La poignée bleue sous le bloc règle son épaisseur. Le dessus reste à la même hauteur.

`Ctrl + Z` annule les changements. `Inverser le virage gauche / droite` crée son miroir.
Le matériau GreenHillTile et le Mesh Collider sont déjà assignés.

## Raccord avec le niveau

Le dessus du bloc est à Y local = 0. Placer l'objet à la hauteur du sol voisin. Garder Scale = (1,1,1) et utiliser les poignées. Les coins des ouvertures ont un arrondi nul par défaut pour que les raccords restent droits.

Ce bloc crée des plateformes et remplit l'intérieur d'un virage. Il ne génère pas de mur relevé : utiliser `Virage_Releve_GreenHill` pour la partie courbe qui remonte vers le mur.

Le contour est une seule boucle de 3 à 32 points. Les points ne doivent ni se superposer, ni faire croiser les côtés. En cas de contour invalide, un avertissement apparaît et la dernière géométrie valide est conservée pendant l'édition. Corriger les points avant de quitter la scène ou de lancer le jeu.

Un grand arrondi est limité à 49 % de la plus courte arête voisine pour éviter les chevauchements. Ajouter des points permet de dessiner des formes plus libres. Ce bloc n'effectue pas de fusion automatique avec les objets voisins.

## Vérification

`Tools > Sonic FX > Structures > Creer et verifier le bloc virage` crée le prefab s'il manque et vérifie géométrie, collision, contours concaves, miroir, arrondis et redimensionnement. Le menu ne modifie pas le niveau ouvert.

## Sélection multiple et courbes libres

- Maj + clic ou Ctrl + clic sur un point orange ajoute ou retire ce point de la sélection.
- Les points sélectionnés deviennent jaunes. Déplacer le gizmo central déplace tout le groupe en X/Z, en conservant les écarts entre les points.
- Les boutons Tout sélectionner / Désélectionner sont dans l'Inspector.
- Arrondi des points applique la même valeur à tous les coins sélectionnés. Un tiret indique des valeurs différentes.
- Sur chaque coin arrondi, la poignée rose Courbe permet de tirer directement le contour extérieur ou intérieur dans le plan du sol. Elle déforme l'arrondi indépendamment des points d'entrée et de sortie de cette courbe.
- Pour un réglage numérique, sélectionner un seul point puis modifier Déformation courbe X / Z.
- Réinitialiser la forme des arrondis sélectionnés rétablit la courbe initiale, sans déplacer les coins.
- Les courbes ne sont visibles que si Arrondir le contour est coché et si l'arrondi du coin est supérieur à zéro.
- Ctrl + Z annule les modifications. Les collisions suivent les courbes. En cas de croisement, la dernière forme valide est conservée : corriger le contour avant d'enregistrer ou de quitter la scène.
