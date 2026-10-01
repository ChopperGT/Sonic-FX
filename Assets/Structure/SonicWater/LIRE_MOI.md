# Eau pour Sonic-FX

## Ajouter une zone

1. Attends la fin de la compilation Unity.
2. Dans Project, ouvre `Assets/Structure/SonicWater`.
3. Glisse `Eau_GreenHill.prefab` dans ta scene, ou utilise `GameObject > Sonic FX > Zone d'eau`.
4. Selectionne la racine `Eau_GreenHill`. Sa position Y correspond a la surface de l'eau. Regle Width (largeur), Length (longueur) et Depth (profondeur sous la surface). Les poignees du cadre bleu permettent aussi le redimensionnement.
5. Place la zone dans un bassin existant et lance Play. Le fond et les berges doivent avoir leurs propres collisions : l'eau n'est pas une plateforme solide.

Duplique la zone avec Ctrl+D pour composer d'autres bassins. Garde X et Z de la rotation a zero pour une surface horizontale ; tu peux tourner autour de Y. Le cadre bleu represente le volume sous l'eau. Tu peux changer la couleur, la transparence, les reflets et le courant dans `Eau_GreenHill.mat` (dupliquer le materiau pour des couleurs differentes selon les bassins).

## Comportement

- Eclaboussure a l'entree. La taille augmente avec la vitesse descendante et reste plafonnee.
- Vitesse et acceleration de Sonic reduites a 55 %, gravite a 80 %, puissance du saut a 105 %. Ces reglages donnent un saut un peu plus haut et plus lent.
- 30 secondes d'air ; la jauge reste cachee tant qu'il reste plus de 10 secondes.
- A 10 secondes : nombre visible et musique de noyade fournie. A zero : mort puis retour au checkpoint via HurtControl.
- La tete hors de l'eau recharge immediatement l'air et arrete la musique d'alerte.
- La pause suspend le compteur et la musique d'alerte.
- Passer d'un volume d'eau a un autre sans respirer ne recharge pas l'air. Les ralentissements ne se cumulent pas.
- Les valeurs de mouvement sont restaurees a la sortie, a la mort ou a la desactivation du composant. Le controle automatique du tube suspend les effets de l'eau.

## Reglages utiles

Tous les reglages sont dans Sonic Water Volume. Breathing Height vaut 0.9 unite au-dessus du pivot de Sonic : ajuste-le si ton personnage est d'une autre taille. Air Seconds et Warning Seconds reglent le delai et l'alerte. Splash Multiplier regle la taille des eclaboussures.

Le fichier audio fourni dure environ 16.42 secondes : la lecture commence au debut a l'alerte et s'arrete a la mort ou a la reprise d'air. Music Start Time permet de choisir un autre point de depart dans la piste.

La musique du niveau est identifiee via le mixeur Music de LoadSound et temporairement mise en sourdine. Si ton niveau utilise une autre organisation audio, glisse sa source audio dans Level Music. Le reglage de volume du mixeur est conserve lorsqu'il est trouve.

Le script SonicWaterPlayer est ajoute automatiquement a Sonic pendant Play. Aucun changement manuel des scripts du personnage n'est requis.

## Verification

Commande : `Tools > Sonic FX > Eau > Verifier la logique de l'eau`. Les tests utilisent des objets temporaires en mode edition et verifient les entrees/sorties, chevauchements, respiration, pause, seuil de 10 secondes, mort, reinitialisation et restauration des reglages.

Teste aussi visuellement et a l'oreille en Play : entree lente puis chute rapide, saut sous l'eau, attente jusqu'a 10 secondes, remontee pour respirer, puis noyade complete et retour au checkpoint.

En cas de prefab non genere : `Tools > Sonic FX > Eau > Creer le prefab d'eau`.


## Filtre visuel sous-marin

La camera principale recoit automatiquement une teinte bleutee et une legere ondulation lorsqu'elle passe dans un volume d'eau. Les valeurs reviennent progressivement a la normale a la sortie. Le filtre suit la position de la camera, y compris en vue FPS, plutot que celle de Sonic : une camera restee au-dessus de la surface conserve une vue normale.

Dans Sonic Water Volume > Filtre visuel sous-marin :
- Underwater Visuals : activer/desactiver le filtre pour cette zone.
- Underwater Tint : couleur de la teinte.
- Tint Strength : intensite du bleu (0.3 par defaut).
- Wave Distortion : force de l'ondulation (0.004 par defaut ; 0 la desactive).
- Wave Interval : temps entre les vagues (5 secondes par defaut).
- Visual Transition Seconds : duree d'apparition/disparition (0.35 seconde).

Les vagues s'arretent pendant la pause. Les compteurs dessines en interface superposee restent lisibles.

## Profondeur du bassin

Depth vaut maintenant 100 par defaut. Pour une zone deja placee, verifie cette valeur hors du mode Play : une surcharge de scene peut conserver 10. La limite inferieure du volume doit etre sous le fond du bassin, sinon Sonic est considere comme sorti de l'eau et reprend son air. Garde Scale Y a 1 pour que Depth corresponde aux unites du monde.

Les ondulations sont renforcees (amplitude x3) et durent 60 % de chaque intervalle. Wave Distortion permet toujours de les ajuster.
