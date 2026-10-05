# Chauve-souris robot

Le prefab prêt à placer est `Assets/Ennemy/Bat/ChauveSouris.prefab`.

1. Glisser le prefab dans la scène, sous un plafond avec collider.
2. Dans **Bat Controller**, cliquer **Accrocher au plafond maintenant**. Vérifier **Environment Layers** si le plafond n'est pas sur la couche Default.
3. Cocher **Endormie au départ** pour commencer au repos, ou décocher pour commencer en surveillance éveillée.
4. Modifier **Watch Center / Watch Size**, ou tirer les poignées de la boîte orange dans la vue Scene. La zone suit le point d'accrochage, indépendamment de la position de Sonic.
5. Le cercle cyan montre le rayon du hurlement. Les autres chauves-souris à portée se réveillent sans déclencher une boucle de hurlements.

Le vol, les ailes repliées, l'accrochage au personnage et le réacteur sont animés par le script Bat Visual. Les meshes et textures sont des assets autonomes : aucun logiciel de modélisation n'est nécessaire pour jouer.

## Réglages

- **Flight Speed / Flee Speed** : vitesse de poursuite et de fuite.
- **Vitesse perdue par chauve-souris** : 5 par défaut. Cumul temporaire sur les vitesses de course et maximale, sans écrire dans la sauvegarde. Sonic perd aussi jusqu'à 5 unités de vitesse horizontale à chaque accrochage.
- **Appuis pour la première** : 8 par défaut. Chaque chauve-souris supplémentaire ajoute 4 appuis. Un remplissage libère une chauve-souris ; appuyer rapidement sur la commande **Boule**, actuellement **R1**. Maintenir la touche ne compte pas comme plusieurs appuis.
- **Perte de progression par seconde** : baisse de la barre entre les appuis.
- **Nombre avant dégâts / Secondes avant dégâts** : 5 et 10. Sous le seuil le compteur se remet à zéro ; au-dessus il inflige des dégâts toutes les 10 secondes, via le système normal de rings, bouclier et mort.
- **Chance de rester alerte après l'eau** : 0,3 = 30 %. Le tirage est indépendant pour chaque chauve-souris. Les autres se rendorment. Elles cherchent un autre plafond proche et sec ; à défaut elles retournent à leur support de départ.
- **Chance de destruction par ennemi** : 0,7 = 70 %, tiré individuellement à chaque contact avec un ennemi du pack (ex. Motobug). Les projectiles et les dégâts périodiques ne déclenchent pas ce tirage.
- **Délai avant de pouvoir raccrocher** : protection temporaire après s'être libéré.
- **Shriek / Shriek Volume** : petit cri fourni, remplaçable par un autre clip.
- **Enemy Health → Score On Defeat** : 100 par défaut, modifiable. Une chauve-souris détruite par un autre ennemi n'accorde pas de points au joueur.

Les chauves-souris accrochées ne sont plus des cibles d'auto-lock. Elles empêchent la boule, le spin dash, le homing, le bounce, le drop dash et l'air dash en boule. Déplacement et saut normal restent disponibles ; les capacités de la sauvegarde ne sont pas modifiées. L'eau et les cascades SonicWater existantes les repoussent automatiquement, y compris une traversée rapide de la cascade.

Si l'installation doit être relancée : **Sonic FX → Ennemis → Créer ou vérifier la chauve-souris**. Un prefab existant n'est pas remplacé, pour préserver ses réglages.
