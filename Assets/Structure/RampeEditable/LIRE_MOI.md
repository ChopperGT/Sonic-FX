# Rampe C editable

## Mur sous la rampe

Le champ **Profondeur du mur (metres)** ajoute une profondeur sous la base d'origine. Le dessous devient horizontal et les murs descendent jusqu'a cette base, avec le materiau et les collisions de la rampe. La piste ne change pas. Une poignee bleue sous la rampe permet de regler la meme valeur dans Scene. Mettre 0 retablit le dessous d'origine ; Ctrl + Z annule une modification. La valeur en metres tient compte de l'echelle de l'objet.

Apres compilation hors Play, l'installation ajoute automatiquement l'editeur au prefab existant `Assets/Structure/ramp_C.prefab`. Les instances liees a ce prefab en beneficient aussi. Le FBX d'origine et les materiaux sont conserves.

1. Selectionner une rampe dans la Hierarchy et activer Gizmos dans Scene.
2. Dans le composant Sonic Editable Ramp, laisser Afficher les points coche.
3. Cliquer sur un point orange puis deplacer ses fleches X, Y ou Z.
4. Maj + clic ou Ctrl + clic ajoute/retire des points de la selection. Le gizmo deplace tout le groupe.
5. Selection rapide : Y + selectionne le haut. X - / X + et Z - / Z + selectionnent les extremites locales. Les boutons milieu selectionnent la tranche centrale pour modifier la courbure.
6. Dimensions locales (metres) etire toute la grille. La rotation actuelle de ramp_C signifie que ses axes locaux peuvent differer des axes du monde.
7. Ctrl + Z annule. Retrouver la forme originale reinitialise uniquement la deformation.

La grille commence avec 27 points de deformation progressive, pas les sommets individuels du modele. Les points du milieu influencent une zone plus large. Les UV et les materiaux sont conserves ; la texture suit l'etirement. Le Mesh Collider suit la geometrie. Chaque instance conserve sa propre grille dans la scene : enregistrer la scene apres modification.

## Ajouter des points

Dans **Sonic Editable Ramp > Ajouter des points**, choisir l'axe local X, Y ou Z, puis cliquer sur **Ajouter une rangee de points**. La rampe garde sa forme actuelle, meme si elle a deja ete deformee. La nouvelle rangee est selectionnee et peut etre deplacee avec ses fleches. Cliquer sur un point orange pour ne deplacer que celui-ci, ou utiliser Maj/Ctrl pour une selection multiple.

**Position dans la structure (%)** regle l'emplacement de l'ajout entre les deux extremites, avant deformation. Apres chaque ajout, le placement automatique choisit le milieu du plus grand intervalle libre pour repartir les points, sans les concentrer vers la derniere rangee ajoutee. **Placer l'ajout pres de la selection** permet de choisir volontairement une zone locale. Une position deja utilisee ne peut pas etre ajoutee deux fois : deplacer le curseur ou utiliser le placement automatique. Le Cube utilise le meme placement.

La grille autorise jusqu'a 16 rangees par axe. Ajouter des points sur X ou Z permet une deformation plus locale de la piste ; Y ajoute des controles en hauteur. Les points des extremites restent attaches aux extremites. **Ctrl + Z** annule aussi les ajouts. **Retrouver la forme originale** retire tous les points supplementaires et restaure la grille initiale ; la profondeur du mur est conservee.

Une grille repliee ou ecrasee affiche un avertissement et garde temporairement la derniere geometrie valide. Corriger les points avant d'enregistrer ou de lancer le jeu. Cette deformation ne fusionne pas automatiquement la rampe avec les blocs voisins.

## Polygones et fluidite des pentes

Dans **Sonic Editable Ramp > Fluidite de la surface**, regler **Subdivisions des polygones** :

- **0** : maillage d'origine, 220 triangles.
- **1** : 880 triangles.
- **2** : 3 520 triangles, valeur par defaut.
- **3** : 14 080 triangles, pour les courbes plus marquees.
- **4** : 56 320 triangles, precision maximale.

Les triangles sont subdivises avant la deformation : les nouveaux sommets suivent reellement la courbe des points. L'affichage et tous les Mesh Colliders de la rampe utilisent ce meme maillage. Les points de controle, la profondeur, les materiaux et les UV restent editables. La densite augmente la precision d'une courbe ; pour arrondir une transition volontairement anguleuse, il faut aussi placer les points de controle en courbe.

Le maillage source subdivise est mis en cache : deplacer un point ne refait pas toute la subdivision. Ctrl + Z annule un changement de densite. Les reglages sont propres a chaque rampe et sont enregistres dans la scene. Le modele d'origine n'est pas modifie.

Verification : **Tools > Sonic FX > Structures > Verifier le lissage des polygones de ramp_C**. Les tests utilisent uniquement une scene de previsualisation.

Pour relancer l'installation et les tests : Tools > Sonic FX > Structures > Installer et verifier ramp_C editable. Quitter Play et le mode Prefab avant l'installation. Les tests utilisent une scene temporaire et ne modifient pas le niveau ouvert.
