# Capacités de Sonic

Les personnages qui utilisent ActionManager conservent leur déplacement normal, leur saut et leur capacité à rouler en boule (R1). Les paramètres de vitesse ne sont pas modifiés.

Les capacités spéciales sont verrouillées par défaut : Spin Dash, Drop Dash, attaque téléguidée, dash aérien, rebond offensif, Light Dash et glissade sur les rails. Le ciblage automatique disparaît tant que l'attaque téléguidée est verrouillée. Les ressorts, l'eau, les tubes, les plateformes et le bloc destructible restent utilisables.

L'installation s'applique automatiquement aux personnages déjà présents, après compilation. Aucun composant à retirer ni prefab à remplacer.

## Futur déblocage

Depuis le script d'un événement, appeler par exemple :

```csharp
sonic.GetComponent<ActionManager>().UnlockAbility(SonicAbility.SpinDash);
```

La méthode renvoie `true` pour une nouvelle capacité acquise. Les noms disponibles sont `HomingAttack`, `AirDash`, `SpinDash`, `Bounce`, `LightDash`, `DropDash` et `RailGrinding`. Aucun événement de déblocage n'est installé pour le moment.

En mode Histoire, les capacités acquises sont conservées dans la sauvegarde et lors des changements de niveau et des morts. « Continuer » les restaure ; « Nouvelle partie » repart sans capacités spéciales. Les anciennes sauvegardes restent compatibles et commencent sans capacités spéciales. Les tests lancés directement depuis l'éditeur n'écrivent pas dans une aventure existante.

Dans l'Inspector, la liste **Capacités spéciales de départ → Starting Abilities** d'ActionManager permet de donner des capacités de départ à une instance ou un prefab. Laisser **None** pour le Sonic limité demandé. Ces réglages de départ ne remplacent pas les capacités acquises dans la sauvegarde.
