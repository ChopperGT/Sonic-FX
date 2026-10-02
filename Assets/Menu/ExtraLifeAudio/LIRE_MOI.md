# Son du gain de vie

Installation automatique, sans composant à ajouter à Sonic ou aux scènes.
Le fichier fourni `Extra life.wav` joue pour les vies gagnées via les rings,
le monitor Vie et le bonus de fin de niveau. Les futurs gains doivent appeler
`SonicXProgress.GainLives(nombre)` pour bénéficier du même comportement.

Le réglage `Resources/SonicExtraLifeSound.asset` permet de remplacer le clip
et de changer le volume. Le groupe SFX respecte le volume des effets sonores.
Un gain de plusieurs vies en une seule récompense joue un jingle ; les gains
successifs sont joués à la suite. Le son continue pendant le changement de scène.
Une perte de vie, un chargement de sauvegarde ou une nouvelle partie ne joue pas ce son.

La musique du niveau est mise en pause pendant le jingle puis reprend à la même position.
La case Pause Level Music du réglage SonicExtraLifeSound permet de désactiver cela.
