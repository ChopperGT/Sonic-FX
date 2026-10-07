# Plateforme flottante editable

Glisser Plateforme_Flottante.prefab sur une zone Eau_GreenHill ou Lave. Toute l'emprise du bloc doit se trouver sur le liquide. Le bloc s'aligne automatiquement en hauteur ; un placement hors liquide est signale en rouge et desactive son appui en jeu. Les references Eau / Lave sont facultatives.

Selectionner l'enfant Plateforme_Editable (ou le bouton Editer les points du bloc) : memes outils que Cube, points multiples, rangees X/Y/Z, subdivisions et taille des poignees. Garder une surface superieure praticable. Les collisions et le feu suivent le maillage deforme.

Sur le parent, regler angle maximal, vitesse d'inclinaison, retour a plat et acceleration de glissement. Le bord charge par Sonic s'abaisse ; sa position est accompagnee sans ajouter d'impulsion de plateforme. Un saut le libere normalement.

Sur la lave uniquement, la surface du dessus doit toucher la lave pour declencher le feu. Le dessous immerge ne suffit pas. Regler profondeur immergee, delai d'ignition, propagation, maintien, extinction et repos avant repetition. Le feu commence a l'endroit immerge et couvre progressivement le dessus. Il reste actif pendant le cycle meme si le bloc revient a plat, puis redevient normal.

Le feu inflige un degat habituel, avec protection du bouclier, perte des rings et invulnerabilite habituelle. Le bloc protege Sonic de la lave tant qu'il repose sur sa surface, y compris si le bord s'immerge un peu. S'il tombe du bloc, la lave conserve son effet mortel.

Le bois ne recoit plus de couche de feu. Des flammes animees verticales apparaissent et ondulent uniquement sur la zone deja atteinte. Flame Height / Flame Width reglent leurs dimensions, Flames Per Second leur densite. Les flammes suivent le bloc deforme et son inclinaison, puis s'eteignent en fin de cycle. Les instances deja placees sont compatibles.

Le feu utilise maintenant une texture RGBA de 16 images (grille 4 x 4), avec interpolation entre les images et un decalage par flamme. Le bois reste intact. Embers Enabled active/desactive les braises, Embers Per Second regle leur quantite. La texture se trouve dans Textures/Flammes_16.png et son material dans Materials/Flamme.mat.
