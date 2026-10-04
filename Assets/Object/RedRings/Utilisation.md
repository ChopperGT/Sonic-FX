# Défi des rings rouges

Glisser **Defi_Rings_Rouges.prefab** dans le niveau. Il contient cinq rings.
Déplacer chacun des enfants `Ring Rouge 1` à `Ring Rouge 5` pour dessiner le parcours.
Le parent affiche aussi les poignées de déplacement de tous les rings dans la vue Scene.

Sur le parent **Red Ring Challenge** :

- **Time Limit / Temps imparti** : durée totale, lancée au ramassage du premier ring (60 secondes par défaut).
- **Success Sound / Son de réussite** : déposer ici le fichier audio quand il sera prêt.
- **Star Spacing / Star Size** : écart et taille des étoiles qui mènent au prochain ring.
- **Nombre de rings rouges** : augmenter cette valeur cree immediatement les nouveaux rings a cote du dernier et met a jour l'objectif. La diminuer retire les derniers rings du parcours. Ctrl + Z permet d'annuler.
- **Decalage du nouveau ring** : distance et direction, dans les axes du parent, pour placer chaque nouveau ring.
- **Ordre de ramassage** : deplacer les lignes pour changer l'ordre. Les boutons + et - creent ou retirent des rings, eux aussi.

Pour modifier ces objets, selectionner le parent dans la Scene. Si le prefab est selectionne dans Project, l'ouvrir avec **Open** pour editer ses objets.

Pour dessiner le chemin des etoiles comme le tube :

1. Selectionner le parent dans la Scene, puis choisir **Chemin d'etoiles a editer**, par exemple Ring 1 vers Ring 2.
2. Cliquer **Modifier le chemin comme le tube**. Le trace existant sert de base ; les courbes et anciens points verrouilles sont conserves comme points de controle.
3. Cliquer un **point bleu** dans la Scene puis utiliser ses fleches X/Y/Z. La courbe se deforme autour du point : tous les autres points restent a leur place et les etoiles se repartissent automatiquement sur le chemin. Il n'y a plus de translation en groupe.
4. **Ajouter un point** ajoute un point entre le point selectionne et le suivant. **Supprimer le point** retire le point selectionne.
5. **Verrouiller ce point** le rend orange et bloque son deplacement et sa suppression tant qu'il n'est pas deverrouille. Les autres points restent editables.
6. **Star Spacing / Ecart entre les etoiles**, sur le parent, regle leur espacement. **Star Size / Taille des etoiles** regle leur taille.

Le chemin utilise le meme moteur de splines Unity en courbe automatique lisse que Tube_Test. Les deux extremites restent attachees aux rings : deplacer un ring met donc a jour l'extremite correspondante. La courbe suit le parent sans tourner avec le ring. Les etoiles jaunes sont un apercu du rendu en jeu ; les points bleus servent uniquement a l'edition. Ctrl + Z permet d'annuler. **Revenir au trace automatique** remet un guidage calcule avec les Guide Points. Sans conversion, les anciens traces restent utilisables tels quels.
Sur chaque **Red Star Ring** : rotation, rayon de ramassage, edition de la courbe des etoiles qui menent a ce ring et **Guide Points** pour le mode automatique.
Pour éviter un obstacle, créer des objets vides hors du ring qui tourne, les placer aux étapes du chemin et les ajouter à `Guide Points` du ring de destination, dans l'ordre.

Le prefab original **Assets/Object/Rings Rouge.prefab** fonctionne aussi seul : plusieurs exemplaires dans une scène forment automatiquement un parcours dans l'ordre de la Hierarchy. `Order` permet de choisir explicitement cet ordre. Dans ce mode, le temps et le son se règlent sur le premier ring avec `Auto Time Limit` et `Auto Success Sound`.

Les rings sont visibles et ramassables **uniquement en mode Histoire**. Pour tester, lancer l'aventure depuis le menu ; lancer directement une scène dans l'éditeur ne constitue pas une session Histoire.

Seul le premier ring est visible au depart. Chaque ramassage revele uniquement le suivant (les rings caches ne sont pas ramassables). Dans l'editeur, tous restent visibles pour placer le parcours. Les étoiles indiquent le prochain. Le menu Pause suspend le chrono. Après un dépassement du temps ou une mort pendant le défi, seul le premier ring reapparait et une nouvelle tentative est possible. Une réussite déjà obtenue reste en attente jusqu'à la fin du niveau, même si Sonic meurt ensuite.

Ramasser tous les rings affiche la réussite et joue le son configuré. **Terminer le niveau** valide alors une seule récompense par niveau et par aventure. Quitter sans terminer n'enregistre pas la réussite. Les rings d'un niveau déjà récompensé disparaissent pour cette sauvegarde.

Les quatre premiers niveaux récompensés donnent chacun **Top Speed +5 / Max Speed +1**. Ensuite chaque nouveau niveau récompensé donne **Max Speed +1**, limité à **300**. Les bonus s'ajoutent aux vitesses de base du personnage à son apparition dans le niveau suivant ; ils sont conservés avec **Continuer**. Une nouvelle partie repart sans ces bonus. Les anciennes sauvegardes restent compatibles.
