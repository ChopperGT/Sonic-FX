# Chenille

Glisser `Assets/Ennemy/Caterpillar/Chenille.prefab` dans le niveau. La flèche bleue Z indique son avant. Les matériaux rose, argent et or, les yeux, antennes, crocs et cornes reprennent l'image de référence.

Ses animations sont procédurales : ondulation des segments, progression par contractions, antennes, rétraction avant la charge et extension rapide. Aucun Animator à installer manuellement.

Dans Caterpillar Controller : régler les distances et vitesses de patrouille, vision, réaction, rétraction, compression, charge, récupération, cooldown. Avec Patrol Points, assigner des objets vides fixes dans la scène, indépendants de la chenille, pour un parcours aller-retour. Sans points, elle patrouille de part et d'autre de son emplacement initial.

Sonic peut la détruire en attaquant en boule la tête de face ou le dernier segment par derrière. Les côtés, le milieu et le dessus sont protégés et infligent un dégât habituel, même si Sonic est en boule. L'angle des zones vulnérables est réglable. Le score par défaut est de 100 et peut être changé dans Enemy Health.

La charge vise Sonic pendant la préparation puis conserve sa direction. La chenille s'arrête devant les murs ou les bords de plateforme. Les contacts rapides sont vérifiés par balayage entre les positions successives.
Chenille : longueur editable

Selectionner Chenille dans la Scene (objet principal), puis le composant Caterpillar Controller.
Nombre de boules du corps : 1 a 32 boules, sans compter la tete. Par defaut : 3 boules + 1 tete.
Le corps, les contacts, l'animation de retraction et la cible arriere suivent le nombre choisi.
Les boules retirees sont masquees et non collidantes, puis reutilisees si on rallonge la chenille.
Le nombre est enregistre avec chaque instance/prefab. La modification fonctionne aussi pendant Play ; quitter Play restaure les valeurs de l'editeur.
Les deux crocs utilisent maintenant un maillage droit pointe vers le bas, distinct des cornes du corps.
