# Palmiers Green Hill

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
