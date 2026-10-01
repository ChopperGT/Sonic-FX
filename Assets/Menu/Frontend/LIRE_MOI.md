# Menu SonicX

Ouvrir Assets/BumperEngineV1/Scenes/LogoScreen.unity et lancer Play.

Start, Croix sur PS5, Entrée, Espace ou clic ouvrent le menu principal. Flèches / stick gauche / croix directionnelle naviguent. Croix / Entrée valident. Rond / Échap reviennent en arrière.

Mode Histoire → Nouvelle partie → Sonic (jeune) lance directement Act 1-1 de Sonic 1. Continuer apparaît après le premier chargement réussi d'une aventure et reprend au début du dernier niveau sauvegardé. Une confirmation protège la progression lorsqu'une nouvelle partie la remplace. Les déblocages sont conservés.

La sauvegarde locale utilise PlayerPrefs, clé SonicFX.Story.Save.v1. Elle contient le personnage et le chemin du niveau, pas la position, les anneaux ou les checkpoints. Les transitions vers les scènes de Assets/Level enregistrent le niveau suivant pendant une session Histoire. Pour une scène située ailleurs, appeler SonicFX.Menu.SonicXProgress.SaveLevel(cheminScene) après son chargement. Une ouverture directe du niveau dans l'éditeur ne crée pas de sauvegarde d'aventure.

Les déblocages sont masqués par défaut. Le futur système de progression peut appeler :

```csharp
SonicFX.Menu.SonicXProgress.Unlock("arcade");
SonicFX.Menu.SonicXProgress.Unlock("tails");
SonicFX.Menu.SonicXProgress.Unlock("amy");
SonicFX.Menu.SonicXProgress.Unlock("shadow");
```

Aucune condition de déblocage n'a été inventée. Sur l'objet « SonicX - Menu principal », les champs Tails Scene, Amy Scene et Shadow Scene permettent d'assigner leurs futures scènes, chacune équipée du personnage correspondant. Tant qu'aucune scène n'est configurée, leur sélection affiche un message sans lancer Sonic à leur place. Arcade Scene utilise actuellement la sélection de niveaux existante. Ajouter toute nouvelle scène aux scènes du build.

Paramètre reprend MUSIC_VOL, SFX_VOL, X_SENS, Y_SENS, X_INV, Y_INV et la préférence de caméra du tube du projet. Valider une ligne fait défiler sa valeur. Les volumes sont affichés en pourcentage et enregistrés dans le format décibels attendu par les anciens menus. Les réglages sont enregistrés immédiatement.

Le composant SonicX Front Menu gère le nouveau menu. L'ancien TitleScreenControl et le Canvas BGUI sont désactivés dans LogoScreen. Leurs autres fichiers ne sont pas modifiés. Le logo et [MenuMusic] sont réutilisés. La scène Act 1-1 est ajoutée à la fin des scènes du build pour préserver les indices existants.

Quitter ferme le jeu construit ; dans l'éditeur, il quitte le mode Play.

Installation automatique hors Play après compilation. Commande manuelle : Tools → Sonic FX → Menu → Installer les menus Histoire. Ne pas relancer l'ancien installateur du logo pour réactiver l'ancien écran.

Des sauvegardes des fichiers modifiés sont conservées dans le dossier de travail outputs/SonicX_Frontend/Backups.
