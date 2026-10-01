# Décors Green Hill

Les prefabs violet, sun_flower et totem conservent leurs noms et leur géométrie. Leurs UV sont adaptés à un atlas commun : pétales violets ou jaunes, feuillage vert, bois orangé et ailes argentées.

Après installation, revenir dans Unity hors du mode Play et attendre la compilation. Application automatique une seule fois. Commande manuelle : Tools > Sonic FX > Decor > Appliquer les textures des decors.

Continuer à utiliser les trois prefabs existants dans Assets/Structure/décore. Les instances qui héritent de leurs matériaux se mettront à jour. Une instance avec un remplacement de matériau peut garder ce remplacement.

Fichiers : Textures/Decor_GreenHill_Atlas.png ; GreenHill/Decor_GreenHill.mat ; trois maillages avec UV adaptés dans GreenHill. Ne pas supprimer ces fichiers. Aucun composant de jeu ajouté. Les scripts du dossier Editor ne sont exécutés que dans l'éditeur.

Une sauvegarde des prefabs et de leurs .meta avant modification est conservée dans le dossier de travail outputs/GreenHill_Decor/Backups.

## Génération de l’atlas

Mode : outil intégré image_gen, nouvelle génération à partir des couleurs et matières des références fournies. Aucun service API externe. Prompt exact :

Create one production-ready square 2048x2048 RGB diffuse texture atlas for three stylized low-poly Sonic Green Hill decorative props: a purple flower, a yellow sunflower, and an orange wooden totem with green carved faces and silver wings. This is a MATERIAL SWATCH ATLAS, not an illustration of the objects. Strict layout: exactly 4 equal columns and 2 equal rows, eight rectangular swatches touching edge to edge. No margins, gutters, borders, text, labels, objects, lighting, perspective, shadows or transparency. Every swatch completely fills its cell, including corners. Top row left to right: (1) saturated lavender purple petal surface, very soft organic silky variation; (2) vivid yellow-green lime plant surface, subtle organic variation; (3) bright sunny golden yellow petal surface, soft variation; (4) dark olive-green sunflower seed center surface, tiny subtle evenly distributed seed stipples. Bottom row left to right: (1) warm orange ochre carved wood with subtle fine vertical grain, bright Sonic Green Hill style; (2) pale cool silver metal, very subtle brushed grain, no specular hotspots; (3) vivid fresh emerald green leaf/stem surface with subtle fine lengthwise veins; (4) deep forest green painted recessed carving surface, gently mottled. Bright simple game palette, clean hand-painted game textures, subtle low contrast micro-detail only. The color of each cell must remain consistent throughout, no dark edges or vignettes. Textures will be UV-mapped onto existing geometry, so DO NOT draw a flower or totem or any silhouettes. Cell divisions exactly at x=25%,50%,75% and y=50%.
