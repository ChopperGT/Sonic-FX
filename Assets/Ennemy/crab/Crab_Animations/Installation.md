# Animations du crabe

Ce paquet contient un squelette de 15 articulations, une animation de repos (2 s), une marche en boucle (0,8 s), les textures et un générateur de prefab pour Unity. Les quatre pattes alternent par paires diagonales ; le corps, les bras, les pinces et les yeux ont des mouvements légers. Les articulations sont rigides, adaptées au robot.

## Installer

1. Arrêter Play dans Unity.
2. Ouvrir Crab_Animations.unitypackage et cliquer sur Import pour tous les fichiers.
3. Attendre la compilation, puis ouvrir le menu Tools > Sonic FX > Creer le crabe anime.
4. Le prefab Crab_Anime est créé et sélectionné dans Assets/Ennemy/Crab_Animations/Crab_Anime. Glisser ce prefab dans la scène.
5. Lancer Play. Le crabe joue son animation au repos. Sur son composant Crab Locomotion Animator, cocher Preview Walking pour tester la marche sur place. Décocher pour revenir au repos.

Pour voir le crabe plus grand ou plus petit, régler son Transform > Scale uniformément. Le modèle fourni mesure environ 2,43 unités de haut, est centré au sol et regarde vers son axe local +Z. C’est un nouveau prefab visuel : il ne remplace pas le crabe déjà posé et ne reprend pas automatiquement ses composants de jeu.

## Quand le crabe se déplacera

Le composant CrabLocomotionAnimator mesure le déplacement de sa racine, joue la marche quand il avance et adapte la cadence à la vitesse. Reference Speed règle la vitesse correspondant au cycle normal (0,65 unité/s par défaut). Movement Source permet de suivre un autre objet, par exemple la racine d’un ennemi dont ce modèle est un enfant. Preview Walking doit être décoché pour le fonctionnement automatique.

Ce paquet anime le modèle. Il ne le fait pas patrouiller et n’ajoute pas de poursuite, de collisions, d’attaque ou de dégâts : ces comportements seront à brancher séparément. Le mouvement reste commandé par le script d’ennemi, sans root motion.

## Organisation et vérification

- Le maillage du crabe est extrait du FBX fourni et conserve sa forme, ses UV et ses normales. Ses éléments mécaniques reçoivent des poids rigides sur 15 articulations.
- Le générateur crée un Mesh skinné, deux AnimationClips en boucle, un AnimatorController et le prefab. Chaque nouvelle exécution utilise un dossier distinct ; les créations précédentes restent intactes.
- Les fichiers du générateur sont dans Editor, exclus des builds. Le prefab généré utilise des assets Unity ordinaires.
- Les matériaux et la texture sont copiés dans le paquet avec de nouveaux identifiants, pour ne pas modifier les matériaux existants.
- Validation effectuée : compilation C# contre Unity 6000.5.10f1 sans erreur, reconstruction exacte de la pose de référence, continuité des boucles, calcul du skinning sur 98 poses et aperçu animé hors Unity. Le générateur échantillonne également les clips dans Unity lors de la création pour contrôler le skinning et calculer les limites de rendu.
- L’import du paquet et la lecture des animations dans Unity restent à confirmer dans l’éditeur de l’utilisateur. L’aperçu GIF est un rendu simplifié par face, pas une capture Unity.
