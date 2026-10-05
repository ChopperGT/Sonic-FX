# Lave

Glisser `Assets/Structure/SonicLava/Lave.prefab` dans le niveau, ou utiliser GameObject > Sonic FX > Zone de lave.

La position Y correspond à la surface. Dans le composant Sonic Lava Volume, régler Largeur (X), Longueur (Z) et Profondeur. Les poignées orange de la vue Scene permettent également d'étendre chaque côté ; la profondeur descend sous la surface. Rotation et échelle sont prises en compte.

L'aspect propose trois couleurs, une taille de motif, une vitesse d'animation et une intensité lumineuse. Aucun asset externe n'est nécessaire.

Le premier contact du corps de Sonic déclenche sa mort, même avec des rings, un bouclier ou de l'invincibilité. Le système de mort existant retire une seule vie, puis gère le checkpoint ou le game over. Les grands triggers d'interaction du personnage sont exclus ; les traversées rapides sont détectées entre les frames physiques. La lave n'est pas un sol solide.

Ne pas superposer une zone d'eau et une zone de lave. Aucun changement n'est appliqué aux volumes d'eau existants ni aux niveaux.
