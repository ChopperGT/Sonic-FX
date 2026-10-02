# Monitor 1 vie

Glisse `Monitor_Vie.prefab` dans ton niveau. Il se casse avec les memes attaques
que les monitors Ring et Bouclier et ajoute immediatement une vie au compteur.
Cette vie est enregistree dans l'aventure en cours.

Le composant `Life Monitor Icon` affiche l'icone du personnage selectionne.
Sonic (jeune) utilise l'icone du HUD. Pour les futurs personnages, renseigne
leurs sprites dans `Character Icons` (tails, amy, shadow). En attendant, l'icone
Sonic sert de remplacement.

`Monitor Data > Score On Destroy` permet d'ajouter des points en plus de la vie.
Il vaut 0 par defaut pour ce monitor.

Si le prefab n'apparait pas apres compilation, menu Unity :
`Tools > Sonic FX > Monitors > Installer le monitor 1 vie`.
