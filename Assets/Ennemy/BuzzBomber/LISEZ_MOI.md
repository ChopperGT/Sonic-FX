# Guêpe robot — Sonic-FX

Ce pack reprend les formes et les couleurs de l'image fournie : casque bleu, grands yeux, crocs, antennes jaunes, thorax noir, abdomen segmenté, quatre ailes et deux réacteurs.

## Installation

1. Arrête le mode Play dans Unity.
2. Décompresse le ZIP dans un dossier temporaire, par exemple sur le Bureau.
3. Copie **le seul dossier `BuzzBomber`** dans `Assets/Ennemy` de ton projet. Garde les fichiers `.meta` avec leurs fichiers.
4. Attends la compilation.
5. Dans Unity, choisis **Tools → Sonic FX → Creer la guepe robot**.
6. Dans le dossier `Guepe_Generee` qui apparaît, glisse **Guepe_Robot_Parcours** dans la scène.
7. Place le parent `Guepe_Robot_Parcours` au niveau du sol de ta zone. Les points du parcours sont initialement 4 à 5 unités au-dessus de ce parent.
8. Déplie le groupe et sélectionne `Ennemi_Guepe`. Tu peux glisser `Player_ManiaSonic` de la Hierarchy dans son champ **Player** ; sinon le script tente de le trouver par le tag Player en jeu.
9. Lance Play et approche Sonic dans une zone dégagée.

Chaque utilisation du menu crée un nouveau dossier : tes prefabs et tes réglages existants ne sont pas écrasés. Il suffit de l'utiliser une fois pour commencer. Pour ajouter d'autres guêpes, duplique le groupe complet dans la scène avec Ctrl+D.

## Modifier sa promenade

Dans la **Hierarchy**, déplie :

```
Guepe_Robot_Parcours
  Parcours_MODIFIER_LES_POINTS
    Point_1
    Point_2
    Point_3
    Point_4
  Ennemi_Guepe
```

- Sélectionne un **Point**, appuie sur **W**, puis déplace ses flèches. L'ennemi vole entre les points ; les lignes bleues montrent le parcours quand les Gizmos sont activés.
- Sur chaque point, **Speed** est la vitesse pour aller vers ce point et **Pause** la durée d'arrêt à l'arrivée, en secondes.
- Sélectionne `Parcours_MODIFIER_LES_POINTS` pour utiliser **Ajouter un point a la fin**.
- Pour retirer un point, supprime son objet puis clique **Nettoyer les points supprimes** sur le parcours.
- La liste **Points** définit l'ordre. Tu peux réordonner ses éléments dans l'Inspector.
- **AllerRetour** : 1 → 2 → 3 → 4 → 3 → 2 → 1.
- **Boucle** : 1 → 2 → 3 → 4 → 1.
- Déplace ou tourne **le groupe entier** pour replacer le système dans ton monde. Déplacer seulement l'ennemi ne déplace pas sa route.
- Fais les modifications hors Play pour les conserver. Tu peux aussi déplacer les points pendant Play pour essayer ; ces changements sont temporaires.

Le vol suit des segments droits avec une rotation progressive. Il s'arrête devant un obstacle ; il ne calcule pas un détour autour des murs. Place les segments dans des passages dégagés et laisse une marge sous l'abdomen et autour des ailes.

## Comportement et réglages

Sur `Ennemi_Guepe`, composant **Buzz Controller** :

| Champ | Effet | Valeur initiale |
|---|---|---|
| Notice Distance | Distance de détection de Sonic, toutes directions | 24 |
| Lose Distance | Distance à laquelle il abandonne Sonic | 32 |
| Lost Sight Delay | Délai avant d'abandonner après perte de vue | 2 s |
| Maximum Chase Distance | Distance maximale depuis sa position de détection | 14 |
| Approach Speed | Vitesse pour s'approcher et revenir | 4 |
| Stand Off Distance | Distance horizontale qu'il essaie de garder | 9 |
| Firing Distance | Portée maximale autorisant un tir | 17 |
| Attack Cooldown | Pause entre les salves | 2,2 s |
| Projectile Speed | Vitesse des projectiles | 14 |
| Environment Layers | Couches du terrain et des murs | Default |

Il repère Sonic, s'approche en gardant son altitude de vol, s'arrête, prépare son canon puis tire deux projectiles. Le point visé est fixé au début de la préparation : Sonic peut esquiver. Les projectiles vont en ligne droite et sont arrêtés par les obstacles. Il revient au parcours s'il perd Sonic. Sa destruction et les dégâts utilisent `EnemyHealth` et `Objects_Interaction` du projet, comme pour le crabe ; le pack n'a pas besoin des scripts du crabe.

Les couleurs des Gizmos : **jaune = détection**, **rouge = portée de tir**, **vert/gris = ligne de vue en Play**.

Dans **Diagnostic pendant Play**, lis **Status**, **Sonic Distance**, **Sees Sonic** et **Obstacle**. Si le terrain utilise une couche autre que Default, ajoute cette couche à Environment Layers. Exclue Player, Enemies et EnemyTrigger de ce masque.

## Modèle, texture et animations

Le menu génère des meshes Unity `.asset`, les matériaux, les prefabs, le contrôleur Animator et quatre clips `.anim` : **SurPlace**, **Vol**, **Attaque**, **Destruction**. Il n'y a pas de rig Humanoid à configurer : les articulations sont des objets enfants animés, adaptés à ce robot.

`Textures/Buzz_Atlas.png` est l'atlas de couleur : bleu et noir en haut, argent et jaune en bas. Les UV utilisent une marge dans chaque case pour éviter les débordements. Les yeux et les parties lumineuses utilisent des matériaux distincts.

Les événements du clip **Attaque** appellent `FireBuzzProjectile(0)` à 0,52 s, `FireBuzzProjectile(1)` à 0,74 s et `FinishBuzzAttack` à 1,16 s. Garde ces événements si tu modifies le clip.

Le pack vise le projet Sonic-FX actuel : Unity 6000.5.10f1, shader Standard, scripts BumperEngine déjà présents et tags/couches du projet. Le script de construction reste dans `Editor`, il n'est pas inclus dans un build du jeu.

## Création de la texture

Texture créée avec l'outil intégré imagegen, puis copiée dans ce pack sans modification. Prompt :

> Create a square production game albedo texture atlas, full image edge to edge, strict 2 columns by 2 rows each exactly half of image. NO borders between tiles, NO text, NO objects, NO perspective, NO lighting baked, NO gradients. Top left quadrant: vivid cyan blue enamel painted metal, extremely subtle fine scuffs, nearly uniform blue. Top right: charcoal black painted metal, extremely subtle fine grain. Bottom left: pale silver white brushed aluminum, very subtle horizontal brushing. Bottom right: saturated golden yellow enamel, tiny subtle scuffs. Stylized clean retro robotic flying insect game enemy palette, smooth clean surfaces, scratches low contrast never busy. Four completely flat square material swatches touching directly at the precise center of image. 1024x1024.
