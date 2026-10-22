# Fred Go Game

Une application de bureau WPF en C# pour jouer au Go contre l’ordinateur.

## Prérequis

- Windows
- .NET 9 SDK

## Lancer le jeu

Télécharger le fichier [FredGoGame.exe-V1.0.0-net9.0-windows.zip](https://github.com/fredatgithub/FredGoGame/releases/download/V1.0.0/FredGoGame.exe-V1.0.0-net9.0-windows.zip) ou la dernière version dans la [section Release](https://github.com/fredatgithub/FredGoGame/releases).

Décompresser le fichier zip dans un répertoire en local.

Depuis ce dossier, exécutez :

FredGoGame.exe

## Jouer

- Vous jouez les Noirs et commencez la partie ; l’ordinateur joue les Blancs.
- Choisissez un plateau (appelé Goban) de 9 × 9, 13 × 13 ou 19 × 19.
- Cliquez sur une intersection pour poser une pierre.
- Les pierres ou groupes sans liberté sont capturés. Les coups suicidaires et les répétitions de position (superko positionnel) sont interdits.
- Vous pouvez passer votre tour. Deux passes consécutives terminent la partie.
- Le score provisoire et le résultat final utilisent un comptage de zone simplifié, avec 6,5 points de komi pour les Blancs.
- Chaque partie est enregistrée au format SGF dans le dossier `Documents\Go-Games` lorsqu’elle se termine, lorsqu’une nouvelle partie est lancée ou lorsque l’application est fermée. Le fichier contient les coups, les passes, les noms `joueurHumain` et `joueurOrdinateur`, et le résultat lorsqu’il est disponible.

## À propos de l’ordinateur

L’ordinateur choisit ses coups avec une heuristique simple qui tient compte des captures, des libertés et de la proximité des pierres déjà posées. Il ne s’agit pas d’un moteur de Go avancé.

Le comptage des points est indicatif : le jeu ne propose pas d’étape de confirmation des pierres mortes ni de négociation des territoires.
