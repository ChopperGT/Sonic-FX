# HUD de jeu

Le HUD se crée automatiquement sur Sonic au lancement du niveau. Il remplace l'ancien affichage des anneaux.

- Ligne 1 : score à 0, en attendant le futur système de score.
- Ligne 2 : temps du niveau, en minutes, secondes et millisecondes. Il s'arrête pendant la pause et la mort. Il continue après un retour au checkpoint et repart à zéro au chargement d'un niveau.
- Ligne 3 : anneaux actuellement possédés, en rouge à zéro.
- Ligne 4 : vies restantes avec le logo Sonic fourni.

## Vies et sauvegarde

Une nouvelle partie commence avec 3 vies. Chaque mort retire une seule vie : 3 → 2 → 1 → 0. Les premières morts utilisent le retour au checkpoint existant.

À zéro, Sonic ne réapparaît plus. L'écran Game Over permet de revenir au menu avec Entrée, Croix sur PS5, Start ou le bouton à l'écran. La sauvegarde de cette aventure est effacée : Continuer disparaît. Les personnages et modes déjà débloqués restent débloqués.

Continuer reprend le dernier niveau avec les vies sauvegardées. Les anciennes sauvegardes sans compteur de vies sont reprises avec 3 vies.

Un test lancé directement depuis un niveau dans l'éditeur utilise 3 vies et ne modifie pas une autre aventure sauvegardée. Pour tester la sauvegarde des vies, commencer depuis LogoScreen → Mode Histoire → Nouvelle partie.

## Modifier l'apparence

Dans Project : Assets → Menu → GameplayHUD → Resources → SonicHudSettings.

- Scale : taille générale du HUD.
- Margin : distance du bord gauche (X) et du haut (Y).
- Sonic Icon / Ring Icon : les deux icônes.
- Number Font : police des compteurs.

Faire les changements hors Play, puis relancer le niveau.

L'installation se fait automatiquement après compilation. En cas de besoin : Tools → Sonic FX → HUD → Installer et verifier le HUD.
