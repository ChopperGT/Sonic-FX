# Geyser de lave

Glisser `Geyser_Lave.prefab` librement dans la scene, avec ou sans lave. Son enfant
`Zone_Declenchement` active l'eruption lorsque Sonic entre dans son Box Collider.
Les dimensions et la position de cette zone se reglent avec les outils Unity.

Selectionner le geyser : **Aligner le geyser sur la lave** le met exactement a la
surface si un volume est proche. Le champ **Lave** peut aussi etre renseigne.
Cet alignement est facultatif et ne conditionne jamais le declenchement.

## Zones de chute

Deplacer les enfants `Zone_Chute_1`, `Zone_Chute_2`, etc., ou leurs poignees dans la
scene. **Ajouter une zone de chute** ajoute un nouvel emplacement. Pour en retirer
une, supprimer son enfant et retirer l'entree vide dans la liste Zones de chute.

Chaque zone propose : rayon de dispersion, nombre de rochers, modele impose
(sinon un des six modeles magma), taille minimale/maximale, duree du vol et hauteur
de l'arc. Le rayon 0 vise exactement le centre. Les marques rouges suivent le sol
et indiquent la position et la taille de chaque rocher, avant son lancement et
jusqu'a son impact. Ajuster les couches et distances de recherche si necessaire.
Decocher **Projeter la cible sur le sol** permet d'imposer la hauteur manuellement.

Pour regler facilement les rochers, selectionner le geyser et utiliser
**Taille des rochers par zone** dans son Inspector. Chaque zone a ses propres
tailles minimale et maximale : 0,5 donne une demi-taille, 1 la taille du modele,
2 le double. Le bouton **Taille fixe** rend les deux valeurs identiques ; sinon
la taille est choisie aleatoirement entre ces deux valeurs. Les collisions et
les marques rouges suivent automatiquement la taille des rochers.

**Afficher l'apercu des rochers** montre leur forme directement dans la vue Scene
lorsque le geyser ou une zone de chute est selectionne (bouton Gizmos actif).
Le contour cyan represente la taille minimale, le contour orange la maximale.
Pour une taille fixe, seul le contour orange est affiche. L'apercu se pose sur
le sol au centre de la zone et se met a jour lorsque les tailles changent.
Si plusieurs modeles peuvent etre projetes, **Modele de l'apercu** permet de les
comparer sans modifier leur selection aleatoire en jeu. Aucun rocher reel n'est
cree par l'apercu, qui est masque pendant le jeu.

## Eruption et degats

Regler sur le geyser le delai d'avertissement, l'intervalle entre les projectiles,
la pause entre les eruptions, le rayon du geyser et la hauteur du jet.
Un emplacement pour le son est prevu. Les rochers deviennent solides et infligent
un degat normal au contact une fois poses. Les protections habituelles de Sonic
(bouclier, invincibilite) restent actives. La duree des rochers 0 les conserve
jusqu'a la fin du niveau. Une valeur positive les retire apres ce delai.

Le bruit de debris fourni est joue une seule fois a l'atterrissage de chaque
rocher, en audio 3D a sa position d'impact. Dans **Son des impacts**, changer
le son ou son volume, ou decocher **Jouer le son a l'atterrissage** pour le couper.
Un champ de son vide utilise le bruit fourni par defaut. Le son suit le meme
groupe audio que le geyser et termine sa lecture meme si le rocher disparait.

La zone de declenchement permet **Une seule activation**, ou des activations
repetees avec la recharge du geyser. **Repeter tant que Sonic reste dedans**
permet une eruption a chaque recharge. `Zone_Geyser.prefab` est une zone separee :
glisser le geyser dans son champ **Geyser a activer**.

Pour une trigger zone existante qui expose un UnityEvent, choisir
`SonicLavaGeyser -> TriggerEruption()` comme evenement. La zone integree est deja
reliee et n'exige aucun autre script du moteur.

Verification : menu **Sonic FX > Lave > Verifier le geyser**. Les tests et l'apercu
utilisent une scene temporaire et ne modifient pas le niveau ouvert.
