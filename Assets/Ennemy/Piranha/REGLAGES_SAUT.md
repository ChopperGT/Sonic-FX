# Saut du piranha

Sélectionne ton poisson dans la Hierarchy, hors du mode Play. Dans **Piranha Controller → Saut en cloche** :

- **Hauteur maximale du saut (Y)** : hauteur maximale du centre du poisson AU-DESSUS DE LA SURFACE DE L’EAU, en unités Unity. Le prefab est réglé sur **20**. Mets par exemple **25** pour un pont plus haut. Ce réglage ne change pas la position du poisson dans le niveau.
- **Portée maximale du saut** : distance maximale autorisée. Réglée sur **40** dans le prefab. Pour un saut avec retour, limite aussi la distance entre le départ et le sommet.
- **Launch Search Radius** : rayon autour de Sonic dans lequel le poisson cherche une sortie. Valeur initiale **24**.
- **Allow Return Jump** : autorise un demi-tour au sommet et un retour dans l’eau de départ si une traversée complète du pont n’est pas possible.
- **Jump Gravity** : accélération du saut ; change sa durée et sa vitesse, pas la limite de hauteur.

Le poisson tente d’abord une trajectoire en cloche avec atterrissage dans une eau proche. Si nécessaire et autorisé, il peut faire demi-tour au sommet et replonger par la trajectoire qu’il vient de parcourir. Il cherche un point de départ accessible, nage horizontalement ou plus profondément et contourne les obstacles immergés par des étapes contrôlées.

Si Sonic est trop haut pour la valeur choisie, le poisson continue de nager au lieu de rester figé. Le champ **Status**, en Play, affiche la hauteur minimale requise ou la raison du refus. Les lignes cyan montrent le trajet sous l’eau ; la ligne verte montre le saut ; la ligne magenta indique la limite de hauteur.

Les collisions avec le décor et l’atterrissage dans l’eau restent obligatoires. Vérifie que **Environment Layers** contient les couches du pont et des plateformes. Une eau trop étroite ou un plafond totalement fermé peut toujours rendre l’attaque impossible.

Les instances qui héritent du prefab reçoivent les nouvelles valeurs 20/40. Une valeur remplacée manuellement dans une instance ou un Variant reste prioritaire : règle-la directement dans son Inspector si besoin.
