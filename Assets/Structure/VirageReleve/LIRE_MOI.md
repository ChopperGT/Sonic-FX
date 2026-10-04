# Virage releve Green Hill

## Installation dans le niveau

1. Hors Play, glisse Assets → Structure → VirageReleve → Virage_Releve_GreenHill.prefab dans la scene.
2. Place-le avec W et oriente-le avec E. Le pivot est au bord interieur de l'entree, au niveau du sol plat.
3. Dans Scene, la fleche verte indique le sens d'entree et la fleche jaune la sortie.
4. Le Mesh Collider est deja present, non Convex, non Trigger. N'ajoute pas de Rigidbody.

Le bloc forme par defaut un virage de 90 degres. L'interieur est plat. Sur l'exterieur, une courbe continue remonte jusqu'a la verticale. Sonic peut rester au sol ou prendre la pente pour rejoindre le mur. Sa tenue au mur depend de sa vitesse et du controleur existant ; la structure ne force pas son deplacement.

## Modifier la forme

Sur le composant Sonic Banked Turn :

- Sens du virage : Droite ou Gauche.
- Angle du virage : 90 pour un quart de tour, 180 pour un demi-tour.
- Rayon interieur : agrandit ou resserre le virage.
- Largeur du sol plat : espace ou Sonic peut courir sans monter.
- Largeur de la pente : espace horizontal entre le plat et le sommet du mur. Augmente-la pour une montee plus douce.
- Hauteur du mur : hauteur du bord exterieur.
- Epaisseur du socle : epaisseur sous le sol.
- Longueur des segments / Segments de la pente : finesse du maillage. Les valeurs par defaut privilegient une collision douce.

Dans Scene, la poignee bleue regle la hauteur du mur. La poignee cyan regle la largeur de la pente. Ctrl + Z annule. Les modifications mettent a jour a la fois le modele et sa collision.

Garde de preference Transform → Scale a (1,1,1), et utilise les dimensions du composant. Tu peux dupliquer le bloc avec Ctrl + D ; chaque copie est independante.

## Raccords et materiaux

Aligne le sol plat de l'entree avec ta plateforme, sans decalage vertical ni superposition. Le mur exterieur existe des l'entree : prevoyez assez de largeur ou un raccord approprie sur les cotes. L'angle du virage n'ajoute pas une montee au niveau du sol : il tourne horizontalement, la montee se situe sur le bord exterieur.

Le materiau GreenHillTile existant est assigne au Mesh Renderer. Tu peux le remplacer pour une autre zone ; le modele possede des UVs.

Conserve le composant Sonic Banked Turn actif : il genere le maillage au chargement et apres chaque modification. Les dimensions et les materiaux sont sauvegardes normalement avec la scene et les prefabs. Il n'est pas necessaire d'exporter un FBX pour les changer.

## Verification

Tools → Sonic FX → Structures → Creer et verifier le virage releve verifie le maillage, les normales et les collisions sur plusieurs points, dans les deux sens. Le menu ne remplace pas un prefab deja cree.

Pour le test en jeu : commencer sur la bande plate, prendre de la vitesse, puis se diriger progressivement vers l'exterieur. Le controleur de Sonic est conserve tel quel.

## Supprimer le trou et son bord interieur

Selectionne ton virage dans la Hierarchy, puis clique sur Combler l'interieur sans rebord dans Sonic Banked Turn (ou coche Combler le trou interieur).

Le sol plat se prolonge jusqu'au centre du virage, a la meme hauteur. Il partage le maillage et le Mesh Collider du virage. L'ancienne paroi verticale autour du trou est retiree. Aucun deplacement ni remplacement du prefab n'est necessaire. Ctrl + Z annule, ou decoche l'option pour retrouver le trou.

Si tu avais deja ajoute des blocs pour boucher ce trou, retire leurs colliders ou enleve ces blocs lorsqu'ils se superposent au nouveau sol, sinon leurs anciennes aretes restent presentes. Cette option comble l'interieur du virage ; elle ne realigne pas automatiquement les autres plateformes du niveau.
## Lisser les cotes du virage

Selectionne le bloc dans la Hierarchy, puis coche Lisser les cotes dans Sonic Banked Turn → Raccords lateraux.

La pente et le mur apparaissent progressivement a l'entree, gardent leur hauteur au centre, puis reviennent au sol a la sortie. Les extremites sont plates, avec une transition de pente arrondie. Le maillage de collision suit exactement la nouvelle geometrie : ce n'est pas seulement un effet d'eclairage.

Etendue du lissage regle la proportion du virage utilisee de chaque cote : 0.3 signifie 30 % a l'entree et 30 % a la sortie ; 0.5 etend la transition jusqu'au milieu. Augmente cette valeur pour une transition plus longue. Le sens gauche/droite et le remplissage interieur restent compatibles.

La case est decochee par defaut pour conserver tes placements existants. Decoche-la ou utilise Ctrl + Z pour revenir a la forme precedente. Le socle conserve son epaisseur : aligne toujours le niveau du sol plat avec les plateformes voisines.
# Prolonger l'entree et la sortie

Dans le composant Sonic Banked Turn, modifier **Longueur de l'entree** et **Longueur de la sortie**. Les deux valeurs sont independantes, en unites locales. Zero conserve la forme precedente.

Dans Scene, tirer le cube vert a l'entree ou le cube jaune a la sortie. Le virage lui-meme ne se deplace pas. Ctrl + Z annule une modification.

Les prolongements sont droits, dans la direction de la piste, avec le meme profil. Si Lisser les cotes est coche, ils sont plats ; sinon ils conservent la pente et le mur. Combler le trou interieur prolonge aussi le sol interieur. Le maillage et le Mesh Collider sont reconstruits ensemble, sans paroi cachee aux raccords.
