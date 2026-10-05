# Medailles par map

Hors Play, ouvrir la map puis **Tools > Sonic FX > Niveau > Ajouter les reglages des medailles**.
La commande cree et selectionne **Reglages des medailles**. Elle reprend les seuils actuels du joueur du niveau ; si l'objet existe deja, elle le selectionne sans en creer un second.

Dans **Sonic Level Medals**, modifier les cinq temps en secondes : **Arc-en-ciel**, **Diamant**, **Or**, **Argent**, **Bronze**. La conversion en minutes/secondes apparait en dessous. Les seuils sont croissants : un seuil ne peut pas etre inferieur a celui de la medaille precedente. Une egalite avec le temps du joueur accorde la medaille. Au-dela du seuil Bronze, aucune medaille.

Enregistrer la scene. Utiliser un seul composant actif par map : tous les personnages utilisent ses temps, pour le bilan, les records et le menu pause. Le composant ne change pas le bonus de points lie au temps.

Ajout manuel possible : creer un objet vide dans la map puis **Add Component > Sonic FX > Niveau > Medailles des records** (valeurs initiales : 60 / 75 / 90 / 120 / 150 secondes).

Les maps sans ce composant conservent leurs anciens reglages dans **Level Progress Control**.

Le palier Bronze utilise une icone cuivre avec le centre Sonic, une apparition animee, un anneau tournant, un reflet, une coche et une rotation de piece. Les anciens niveaux conservent leurs quatre seuils : Bronze est initialement egal au seuil Argent multiplie par 1,25. Ce seuil est ensuite editable.

