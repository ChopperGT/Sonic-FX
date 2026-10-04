# Crabe ennemi : installation

Ce dossier complète le crabe animé déjà installé dans Sonic-FX.

## Installer et tester

1. Arrête Play dans Unity.
2. Fais clic droit sur Crab_Combat_Dossier.zip dans Windows, puis Extraire tout.
3. Copie le dossier Crab_Combat contenu dans le dossier extrait.
4. Colle ce dossier dans C:\Users\Lecle\Documents\Git\Sonic-FX\Assets\Ennemy. Il doit être à côté de Crab_Animations.
5. Retourne dans Unity et attends la compilation.
6. Clique dans le menu du haut sur Tools > Sonic FX > Creer le crabe ennemi.
7. Le nouveau prefab Crab_Ennemi est créé et sélectionné dans la fenêtre Project. Glisse-le sur une zone de sol dégagée dans ton niveau contenant Sonic, puis lance Play.

Utilise le nouveau Crab_Ennemi pour tester le comportement. Le précédent Crab_Anime reste le modèle visuel sans cette intelligence artificielle. Aucun composant supplémentaire n'est à ajouter au nouveau prefab.

## Comportement

- Il patrouille de part et d'autre de son point de départ, le long de son axe droit initial, avec des pauses aléatoires.
- Il repère Sonic dans un rayon de 22 unités, si aucun mur ne masque la vue.
- Après 0,3 seconde de réaction, il s'approche et cherche à rester à environ 8 unités. Il recule si Sonic arrive à moins de 5 unités.
- Il lève ses pinces puis tire deux projectiles : pince gauche à 0,42 seconde et pince droite à 0,64 seconde dans l'animation.
- Chaque projectile part vers le haut et retombe en cloche vers la position visée lors du lancement, avec une petite anticipation du déplacement. Il ne poursuit pas Sonic en vol.
- Il attend 1,8 seconde après la récupération avant une nouvelle attaque.
- S'il perd Sonic de vue ou si Sonic s'éloigne trop, il revient vers son point de départ.
- Les mouvements vérifient la présence du sol devant le crabe et la présence de murs. Ce déplacement simple ne calcule pas de chemin autour d'un labyrinthe.

## Réglages dans l'Inspector

Sélectionne Crab_Ennemi dans la Hierarchy et ouvre Crab Combat Controller.

| Champ | Effet |
| --- | --- |
| Patrol Distance | Distance parcourue à gauche et à droite du point de départ |
| Patrol Speed | Vitesse de patrouille |
| Pause Min / Pause Max | Durée des pauses, en secondes |
| Notice Distance | Distance à laquelle Sonic est repéré |
| Reaction Time | Délai avant de réagir, en secondes |
| Approach Speed | Vitesse pour s'approcher de Sonic |
| Stand Off Distance | Distance que le crabe cherche à conserver |
| Too Close Distance | Distance en dessous de laquelle il recule |
| Firing Distance | Distance maximale pour commencer un tir |
| Attack Cooldown | Attente entre deux attaques, en secondes |
| Arc Height | Hauteur du sommet de la trajectoire au-dessus du point de départ ou de la cible, selon le plus haut |
| Projectile Gravity | Accélération verticale des projectiles |
| Environment Layers | Couches contenant le sol et les murs ; Default par défaut |

Pour un premier test, conserve les valeurs fournies. Les distances sont en unités du monde. Si tu changes beaucoup la taille du crabe, adapte aussi les distances et les vitesses à sa nouvelle taille.

## Dégâts et projectile

Le crabe utilise EnemyHealth et les tags/couches d'ennemis existants. Sonic peut le détruire avec les attaques reconnues par le pack. Les projectiles appellent Objects_Interaction.DamagePlayer(), qui gère les anneaux, le bouclier et les périodes de protection.

Le générateur crée aussi Crab_Projectile.prefab, deux maillages 3D, leurs matériaux et Crab_Impact.prefab dans le même dossier que Crab_Ennemi. La boule orange possède deux anneaux métalliques, tourne en vol et laisse une courte traînée. Le tir disparaît au contact du sol, d'un mur ou de Sonic. L'effet d'impact se supprime automatiquement.

Le mouvement des projectiles est calculé par CrabLobProjectile, avec des balayages de sphère pour détecter les collisions. N'ajoute pas de Rigidbody ou de script de gravité sur le projectile. Les fichiers OBJ/MTL fournis séparément sont une version exportable du modèle ; le prefab Unity est créé par le générateur.

## Fichiers créés et validation

Le menu crée un nouveau dossier Assets/Ennemy/Crab_Combat/Crab_Ennemi, ou un nom numéroté si une version existe déjà. Il conserve les textures et animations du crabe animé précédent et ajoute un contrôleur d'attaque distinct. Garde donc le dossier Crab_Animations dans le projet.

Vérifications effectuées hors de l'éditeur : compilation contre Unity 6000.5.10f1 sans erreur ; 144 trajectoires C# testées avec cibles à différentes hauteurs, distances, gravités et hauteurs d'arc ; continuité des poses de tir ; aperçu animé du modèle et contrôle de la taille du projectile par rapport à son volume de collision. L'installation et le comportement complet avec Sonic doivent encore être validés dans Unity.

Si le menu n'apparaît pas après la compilation, regarde les erreurs rouges dans Console et envoie leur texte ou une capture. Ne crée pas les composants manuellement : le menu réalise les branchements.
