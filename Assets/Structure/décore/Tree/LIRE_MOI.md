# Palmiers Green Hill

## Feuilles reactives

Les trois prefabs possedent un composant `Palm Leaf Reaction`.
La case **Animer les feuilles au passage de Sonic** active ou desactive l'effet,
y compris pendant le jeu. Les feuilles reagissent dans le sens du passage ;
plus Sonic va vite, plus elles bougent. Une chute sur la couronne les pousse
vers le bas. Les points d'attache restent fixes, puis les feuilles reviennent
au repos avec une oscillation amortie.

Reglages : **Distance de reaction**, **Vitesse pour l'effet maximum**,
**Deplacement maximum des feuilles**, **Souplesse du retour** et **Amortissement**.
Selectionner le palmier affiche en vert sa zone de reaction dans Scene.

Le Mesh Collider utilise uniquement les faces du tronc et des parties boisees.
Les surfaces des feuilles ne sont plus incluses dans la collision, meme lorsque
la case d'animation est desactivee. Les palmiers deja poses qui utilisent ces
prefabs heritent de la mise a jour. Aucun placement de niveau n'est modifie.

Installation manuelle : `Tools > Sonic FX > Decor > Installer les feuilles reactives`.

Les prefabs palm_A, palm_B et palm_C recoivent automatiquement leurs nouvelles textures apres la compilation, hors du mode Play.

- Palmes : vert vif dessus, vert fonce dessous.
- Tronc et base des palmes : brun dore.
- Anneaux : jaune dore.

Atlas commun : Textures/Palmiers_GreenHill_Atlas.png.
Materiau commun et copies des maillages avec UV adaptes : GreenHill/.
Les positions des sommets, les triangles, les normales et les transforms des prefabs sont conserves. Le FBX source n'est pas modifie.

Commande manuelle : Tools > Sonic FX > Decor > Appliquer les textures des palmiers.
Les instances de prefabs avec des surcharges de materiau/maillage peuvent conserver leurs anciens reglages ; dans ce cas, retablir uniquement ces references depuis le prefab.

Texture creee avec l'outil image_gen. Prompt : atlas carre 2x2, vert citron pour le dessus des palmes, vert emeraude pour le dessous, brun orange pour le tronc, jaune dore pour les anneaux ; fibres discretes, style arcade tropical, albedo sans ombres ni reflets peints. UV adaptes separement aux trois modeles fournis.
