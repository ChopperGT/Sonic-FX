# Cube editable

Le prefab existant **Assets/Structure/Cube.prefab** est installe automatiquement apres compilation hors Play. Sa texture, sa forme, son emplacement et son identifiant de prefab sont conserves. Le maillage ProBuilder est copie dans **CubeEditable/Cube_Source.asset** ; l'edition se fait ensuite avec **Sonic Editable Cube**, comme pour ramp_C. Une copie du prefab initial est conservee hors du projet dans le dossier de rapports Codex.

1. Placer ou selectionner **Cube** dans la Hierarchy. Dans Scene, activer **Gizmos** et **Afficher les points**.
2. Cliquer sur un point orange puis tirer ses fleches X, Y ou Z. Tous les cotes, le dessus et le dessous peuvent etre deformes.
3. **Maj/Ctrl + clic** selectionne plusieurs points ; les fleches deplacent le groupe. **Selection rapide** selectionne une face ou une tranche centrale sur X, Y ou Z.
4. **Ajouter des points** : **Rangee parallele automatique** est active par defaut. Selectionner une ligne de points avec Maj/Ctrl + clic : l'ajout se fait perpendiculairement a cette ligne, pour creer une nouvelle rangee parallele. Avec un seul point, la face regardee dans Scene determine l'axe (Y sur un mur, Z sur le dessus). Une fleche verte indique la direction d'ajout. Choisir la position puis **Ajouter une rangee de points** ; la nouvelle rangee est selectionnee, la forme actuelle est conservee et les ajouts suivants gardent cet axe. Pour choisir une autre direction, regler **Axe de la nouvelle rangee** sur X/Y/Z (cela desactive l'automatisme). Jusqu'a 16 rangees par axe.
5. **Subdivisions des polygones** : 0 = cube original (12 triangles), 1 = 48, 2 = 192, **3 = 768 par defaut**, 4 = 3 072. Les sommets supplementaires suivent les courbes des points et les Mesh Colliders suivent la meme surface.
6. **Dimensions locales (metres)** etire la grille complete. **Profondeur du mur** et sa poignee bleue prolongent la base sans deformer le dessus.
7. **Ctrl + Z** annule les modifications, les ajouts et la densite. **Retrouver la forme originale** restaure les 27 points initiaux ; profondeur et densite restent reglables.

Chaque exemplaire conserve ses propres points dans la scene. Enregistrer la scene apres edition. Une forme repliee ou ecrasee est refusee et la derniere collision valide est conservee. Le composant ProBuilder est remplace sur ce prefab pour eviter qu'il reecrive la geometrie generee ; utiliser les points du Cube pour l'editer.

Installation manuelle : **Tools > Sonic FX > Structures > Installer et verifier Cube editable**.

Tests seuls : **Tools > Sonic FX > Structures > Verifier Cube editable**. Les verifications utilisent une scene de previsualisation, jamais le niveau ouvert.
