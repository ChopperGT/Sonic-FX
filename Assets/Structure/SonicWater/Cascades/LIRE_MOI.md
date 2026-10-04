# Cascades reliees a l'eau

## Placer une cascade

1. Hors Play, ouvre Assets → Structure → SonicWater → Cascades.
2. Glisse Cascade_GreenHill.prefab dans ton niveau. Autre possibilite : GameObject → Sonic FX → Cascade.
3. Avec W, place son pivot au SOMMET de la chute, au bord de la plateforme ou de la falaise. Le rideau descend sous ce point.
4. Dans Sonic Waterfall, glisse ton objet Eau_GreenHill depuis la Hierarchy dans Eau a l'arrivee. Tu peux aussi cliquer sur Relier le bassin situe sous la cascade.
5. Laisse Ajuster la hauteur a l'eau coche : le bas rejoint automatiquement la surface du bassin. La projection du pivot doit etre a l'interieur du bassin.
6. Regle Largeur. Oriente la cascade avec Rotation Y. Garde Rotation X et Z a zero et de preference Scale a (1,1,1).

Le prefab doit etre place dans la scene pour accepter une reference a un bassin de la scene.

## Reglages utiles

- Largeur : largeur du rideau d'eau.
- Hauteur manuelle : utilisee sans bassin ou si l'ajustement automatique est decoche.
- Courbure vers l'avant : donne un leger bombement a la chute.
- Vitesse d'ecoulement : vitesse de l'animation visuelle, independante de la force physique.
- Reprendre la couleur de l'eau : utilise la couleur du materiau du bassin.
- Intensite de l'ecume / Etendue de l'ecume : apparence de l'arrivee.
- Force du courant : acceleration vers le bas pendant la traversee (45 par defaut).
- Vitesse de chute du courant : limite de la vitesse ajoutee par le courant (28 par defaut). Une chute deja plus rapide n'est pas freinee.
- Pousser Sonic vers le bas : active ou desactive cette poussee.
- Epaisseur de traversee : epaisseur de la zone qui reagit a Sonic.

Il ne faut pas ajouter de collider solide : Sonic doit pouvoir traverser le rideau.

## Lien avec l'eau existante

La cascade produit les eclaboussures du systeme d'eau existant. Au contact du bassin, celui-ci continue de gerer le ralentissement, les sauts, le filtre sous-marin et la respiration. Traverser le rideau seul ne lance pas le compte a rebours de noyade.

L'ecume au pied s'affiche seulement quand la chute atteint le bassin choisi. Elle est decoupee aux limites rectangulaires du bassin. Le systeme ne creuse pas le terrain : il faut une ouverture ou placer la cascade devant la falaise. Il ne simule pas le remplissage des bassins.

Une fois satisfait, duplique la cascade avec Ctrl + D. Chaque instance a ses propres dimensions, son bassin et sa force de courant.

## Verification

Tools → Sonic FX → Eau → Creer et verifier la cascade construit le prefab s'il manque et verifie les shaders, l'ajustement au bassin, les traversees rapides et la poussee descendante. Un prefab existant n'est pas remplace.

## Etirer directement dans la Scene

Hors Play, selectionne Cascade_GreenHill dans la Hierarchy. Avec Gizmos active dans Scene, le cadre cyan affiche des petites poignees a gauche, a droite, en haut et en bas. Tire une poignee pour etendre ce cote : le bord oppose reste en place. La largeur et le pivot se mettent a jour ensemble, y compris si la cascade est orientee avec Rotation Y.

Tirer le bas decoche automatiquement Ajuster la hauteur a l'eau, pour permettre une hauteur libre. Tirer le haut ou les cotes conserve cet ajustement. Recoche-le pour ramener le bas sur le bassin. Ctrl + Z permet d'annuler.
## Profondeur Z et point de chute

Selectionne la cascade dans Scene, avec Gizmos actif.

- Au milieu sur le cote, la poignee bleue Profondeur / courbure Z regle le bombement du rideau, sans deplacer son arrivee.
- Au pied, Point de chute affiche les trois axes. Tire la fleche bleue pour deplacer l'arrivee sur Z, la rouge pour X, la verte pour Y. L'ecume et la zone de courant suivent l'arrivee.
- Dans l'Inspector, Chute droite (courbure a zero) supprime le bombement. Le rideau relie alors directement son sommet a son point de chute, meme si ce dernier est decale.
- Decalage du point de chute (X / Z) permet aussi une saisie numerique.

Le deplacement sur X/Z conserve l'ajustement automatique au bassin. Un deplacement manuel sur Y desactive cet ajustement, pour ne pas annuler ton geste. Recoche Ajuster la hauteur a l'eau si tu veux ramener le pied sur la surface du bassin. Le bouton Relier le bassin sous le point de chute utilise desormais la position de l'arrivee.

Ctrl + Z annule les changements. Les cascades existantes conservent leur forme tant que tu ne modifies pas ces nouveaux reglages.