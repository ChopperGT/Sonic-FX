# Pierres de magma

Six prefabs sont disponibles directement dans Assets/Structure/PierresMagma : Petite, Moyenne, Grande, Allongee, Pointue, Plate.

Glisser une pierre dans le niveau. Le pivot est à sa base. La taille et la rotation se règlent avec Transform. Chaque pierre possède un collider solide et inflige un dégât habituel de Sonic au contact de son corps : perte du bouclier, perte des rings, ou mort sans protection. La période d'invincibilité après un dégât est respectée, ainsi qu'un délai minimum réglable sur chaque pierre.

Le composant Sonic Magma Rock permet de désactiver les dégâts pour utiliser la pierre comme décor et de régler les couleurs, l'incandescence, les fissures, l'échelle du motif et la pulsation. Le matériau utilise des fissures procédurales en 3D sur toute la roche, sans fichier externe nécessaire.

Ces pierres sont différentes de la lave : elles n'imposent pas une mort instantanée si Sonic a encore une protection.
