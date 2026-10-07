# Plateforme arrondie editable

Prefab : Assets/Structure/PlateformeArrondie/Plateforme_Arrondie.prefab.

Glisse le prefab dans la Scene puis selectionne son objet racine.
Les points orange utilisent le meme systeme que Cube et ramp_C.

- Clique un point puis deplace ses fleches. Maj/Ctrl + clic : selection de plusieurs points.
- Dimensions locales : X = largeur, Y = epaisseur, Z = longueur. Des valeurs X/Z differentes donnent un ovale.
- Selection rapide X/Y/Z : deplacer tout un cote ou le dessus.
- Ajouter une rangee : choisis X/Y/Z et le pourcentage, puis clique le bouton. La forme reste conservee. Jusqu'a 16 rangees par axe.
- Taille des points : meme reglage que les autres structures.
- Subdivisions des polygones : 1 par defaut, 2/3 pour des deformations plus courbes. Les collisions utilisent la meme geometrie.
- Profondeur du mur : prolonge le dessous sans bouger le dessus.
- Retrouver la forme originale : revient au disque et a la grille initiale. Ctrl+Z annule une modification.

La grille est une cage autour de la plateforme : ses coins peuvent etre hors de la surface ronde. Ils permettent de deformer toute la forme progressivement.
Chaque instance conserve ses propres points. Les grilles repliees/ecrasees sont refusees pour conserver la derniere collision valide.
La surface est de 16 x 16 metres avec 2 metres d'epaisseur au depart ; le pivot est au centre du dessus pour l'alignement.
Le materiau GreenHill du Cube est reutilise, avec projection qui suit les surfaces.

Installation automatique apres compilation, hors Play et hors mode Prefab.
Menu de secours : Tools > Sonic FX > Structures > Installer et verifier Plateforme arrondie.
