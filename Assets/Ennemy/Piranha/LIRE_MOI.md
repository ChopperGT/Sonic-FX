# Piranha pour Sonic-FX

Dans Unity, attendre la compilation. Le prefab complet est cree automatiquement dans `Assets/Ennemy/Piranha/Piranha_Pret/Piranha_Ennemi.prefab`.
Si necessaire : `Tools > Sonic FX > Piranha > Creer le poisson ennemi`.

1. Glisser **Piranha_Ennemi** dans la scene, a l'interieur d'une zone Eau_GreenHill.
2. Placer son centre au moins **1.6 unite sous la surface**, a plus de 1.4 unite des bords et du fond. Garder son Scale a 1 au debut.
3. Facultatif : glisser Eau_GreenHill dans Water et Player_ManiaSonic dans Player. Les references sont trouvees automatiquement en Play si vides.
4. Regler Environment Layers sur toutes les couches du decor solide (terrain, plateformes, murs). Default est deja choisi. Ne pas inclure les couches du joueur ou des ennemis.
5. Lancer Play. Le champ Status explique ce que fait le poisson et pourquoi un saut est refuse.

Le fichier `piranha.prefab` d'origine est conserve. Utiliser le nouveau prefab **Piranha_Ennemi**, qui contient le comportement et les textures.

## Reglages du composant Piranha Controller

| Champ | Effet |
|---|---|
| Patrol Distance | Rayon de patrouille libre autour du point de depart. La sphere cyan montre la zone ; les destinations changent de direction. |
| Swim Speed | Vitesse de nage normale et de remontee vers la surface. |
| Pause Seconds | Pause entre deux trajets de patrouille. |
| Notice Distance | Portee de detection de Sonic. |
| Chase Speed | Vitesse de poursuite avant la morsure (4). |
| Bite Start Distance | Distance necessaire pour preparer la charge (3.5). |
| Charge Speed | Vitesse de l'attaque sous-marine. |
| Patrol Vertical Range | Variation de profondeur pendant la patrouille (1.5). |
| Lose Distance | Distance a laquelle la poursuite s'arrete (25). |
| Reaction Time | Preparation avant la charge. |
| Charge Duration | Duree maximale d'une charge dans la direction visee au depart. |
| Attack Cooldown | Attente entre deux attaques. |
| Jump Height | Hauteur du sommet du saut au-dessus de la surface. |
| Jump Gravity | Gravite du saut : augmenter accelere le saut. |
| Maximum Jump Distance | Distance maximale entre depart et retombee, hauteur comprise (22 par defaut). |
| Maximum Dry Gap | Largeur maximale de terrain sec franchissable entre bassins. Mettre 0 pour rester au-dessus de l'eau. |
| Body Radius | Marge autour du poisson pour les obstacles et les bords (1.4). |

Les distances sont en unites Unity, les vitesses en unites/seconde, les durees en secondes. Le rayon suit l'echelle du poisson.

## Comportement

- Patrouille avec pauses ; ne quitte pas les volumes d'eau en nageant.
- Sonic sous l'eau : poursuite, preparation a courte distance puis charge pour le mordre. Les degats et la destruction par Sonic utilisent le systeme des ennemis existants.
- Sonic au-dessus : cherche un point de depart degage autour de la plateforme, s'en approche puis calcule un saut avec une retombee locale dans l'eau. La plateforme ne bloque plus la detection ; elle reste un obstacle pour la trajectoire. Jump Height est un plafond : le poisson choisit une hauteur plus basse si cela permet de passer pres de Sonic.
- Un plafond, un bord trop etroit, Sonic trop haut, une retombee trop lointaine ou un trou entre bassins trop large empechent le saut.
- Le poisson vise la position de Sonic au depart : Sonic peut esquiver.
- Un obstacle nouveau ou une zone de retombee desactivee pendant le saut declenche un retour le long de la trajectoire deja parcourue. Si ce retour est lui aussi bloque, le poisson attend ; eviter de supprimer/deplacer les bassins pendant une attaque.
- Les volumes d'eau doivent rester horizontaux, comme Eau_GreenHill.

Les nageoires, la queue et la machoire sont animees. Le prefab contient trois clips : Repos, Nage, Morsure.

## Verification

`Tools > Sonic FX > Piranha > Verifier le poisson` teste les limites de l'eau, la trajectoire, les obstacles, les bassins voisins et la charge.
En jeu, tester Sonic immerge, Sonic sautant au-dessus de la surface, puis une plateforme faisant obstacle et un bassin trop eloigne. Verifier aussi qu'une attaque en boule detruit le poisson.

Texture : atlas genere avec l'outil image_gen, metal peint rouge / argent / blanc / bleu ; UV adaptes au maillage fourni. Les pupilles et l'interieur de la bouche utilisent un materiau sombre.
