# Caméléon Green Hill

Deux prefabs sont disponibles dans Assets/Ennemy/Chameleon : Cameleon_Sol et Cameleon_Mur.

## Placement

- Sol : glisser Cameleon_Sol dans le niveau, les pieds au niveau du sol. Son axe bleu Z indique où il regarde.
- Mur : placer Cameleon_Mur à moins de 4 unités de la paroi, sélectionner la racine, puis cliquer « Coller et orienter sur le mur ». Si plusieurs parois sont proches, glisser le Collider voulu dans « Mur support » avant de cliquer. Son ventre (axe Y local) se tourne vers l'extérieur.
- « Colle au mur » et « Camoufle au depart » sont indépendants. Le camouflage est réservé au mode mural. Un caméléon au sol reste visible.
- Les murs et plateformes doivent appartenir aux couches cochées dans Environment Layers (Default au départ). Exclure les couches de Sonic et des ennemis.

## Comportement

Au mur, camouflé, il ne patrouille pas et son objet Cible_Homing est désactivé. Il copie la texture du mur, notamment la projection du matériau GreenHillTile. Sur les autres meshes, la projection utilise les UV du triangle touché si disponibles, avec projection plane de secours. Ce n'est pas une invisibilité : sa silhouette reste présente.

Sonic dans la vision : un projectile par attaque, séparé par Attack Cooldown. Si Sonic arrive à Jump Distance, il bondit, se révèle puis reste au sol. Au sol, il patrouille, se rapproche et lance sa langue à portée. Les attaques utilisent les dégâts existants du jeu ; Sonic peut détruire l'ennemi normalement.

Réglages principaux : View Distance / View Angle, Reaction Time, Patrol Radius, Ground Speed et Wall Speed, Jump Distance / Jump Height, Tongue Range, Attack Cooldown. Le champ Etat en Play indique l'action en cours. Les gizmos jaunes indiquent la vision et le cercle rouge la portée proche.

Modèle articulé, texture de carapace, matériaux métalliques, projectile, marche, mâchoire, langue et queue sont inclus. Les animations sont procédurales : aucun Animator à installer. Le matériau de camouflage est partagé par toutes les surfaces visibles pendant le camouflage, puis les matériaux d'origine sont restaurés.

Pour vérifier/recréer des prefabs absents : menu Sonic FX > Ennemis > Creer ou verifier le cameleon. Les prefabs existants ne sont pas écrasés. Tester les deux prefabs en Play avec Sonic devant eux ; au mur, comparer avec et sans camouflage et vérifier l'auto-lock après le bond.

Camouflage : les terrains avec un materiau GreenHillTile et sans TerrainLayers sont pris en charge. Les colliders enfants peuvent reprendre le materiau du renderer parent. En Play, le composant affiche le nom du support et de la texture trouvee. Wall Material Override sur Chameleon Camouflage permet de fournir un materiau explicitement pour un support particulier. L'immobilite est normale tant qu'il est camoufle ; rapprocher Sonic a moins de Jump Distance, dans sa vision, pour declencher le bond.

Orientation : l'alignement est automatique au lancement quand Colle au mur est coche. Le bouton Coller et orienter sur le mur permet de voir cette pose hors Play. Les pattes pointent vers la paroi et la tete vers le haut ; le Rigidbody conserve cette orientation. Apres le bond, il revient debout pour le comportement au sol.

