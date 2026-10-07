# Geyser de lave

Glisser `Geyser_Lave.prefab` dans la scene sur un volume de lave. Son enfant
`Zone_Declenchement` active l'eruption lorsque Sonic entre dans son Box Collider.
Les dimensions et la position de cette zone se reglent avec les outils Unity.

Selectionner le geyser : **Aligner le geyser sur la lave** le met exactement a la
surface si un volume est proche. Le champ **Lave** peut aussi etre renseigne.
**Exiger de la lave** est actif par defaut.

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

## Eruption et degats

Regler sur le geyser le delai d'avertissement, l'intervalle entre les projectiles,
la pause entre les eruptions, le rayon du geyser et la hauteur du jet.
Un emplacement pour le son est prevu. Les rochers deviennent solides et infligent
un degat normal au contact une fois poses. Les protections habituelles de Sonic
(bouclier, invincibilite) restent actives. La duree des rochers 0 les conserve
jusqu'a la fin du niveau. Une valeur positive les retire apres ce delai.

La zone de declenchement permet **Une seule activation**, ou des activations
repetees avec la recharge du geyser. **Repeter tant que Sonic reste dedans**
permet une eruption a chaque recharge. `Zone_Geyser.prefab` est une zone separee :
glisser le geyser dans son champ **Geyser a activer**.

Pour une trigger zone existante qui expose un UnityEvent, choisir
`SonicLavaGeyser -> TriggerEruption()` comme evenement. La zone integree est deja
reliee et n'exige aucun autre script du moteur.

Verification : menu **Sonic FX > Lave > Verifier le geyser**. Les tests et l'apercu
utilisent une scene temporaire et ne modifient pas le niveau ouvert.
