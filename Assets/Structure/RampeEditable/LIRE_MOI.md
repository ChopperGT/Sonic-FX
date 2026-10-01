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

Les 27 points sont une grille de deformation progressive, pas les sommets individuels du modele. Les points du milieu influencent une zone plus large. Les UV et les materiaux sont conserves ; la texture suit l'etirement. Le Mesh Collider suit la geometrie. Chaque instance conserve sa propre grille dans la scene : enregistrer la scene apres modification.

Une grille repliee ou ecrasee affiche un avertissement et garde temporairement la derniere geometrie valide. Corriger les points avant d'enregistrer ou de lancer le jeu. Cette deformation ne fusionne pas automatiquement la rampe avec les blocs voisins.

Pour relancer l'installation et les tests : Tools > Sonic FX > Structures > Installer et verifier ramp_C editable. Quitter Play et le mode Prefab avant l'installation. Les tests utilisent une scene temporaire et ne modifient pas le niveau ouvert.
