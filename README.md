# Fred Go Game

Une application de bureau WPF en C# pour jouer au Go contre l’ordinateur.

## Prérequis

- Windows
- .NET 9 SDK

## Lancer le jeu

Télécharger la dernière version.
Décompresser le fichier zip dans un répertoire.
Depuis ce dossier, exécutez :
FredGoGame.exe

## Jouer

- Vous jouez les Noirs et commencez la partie ; l’ordinateur joue les Blancs.
- Choisissez un plateau de 9 × 9, 13 × 13 ou 19 × 19.
- Cliquez sur une intersection pour poser une pierre.
- Les pierres ou groupes sans liberté sont capturés. Les coups suicidaires et les répétitions de position (superko positionnel) sont interdits.
- Vous pouvez passer votre tour. Deux passes consécutives terminent la partie.
- Le score provisoire et le résultat final utilisent un comptage de zone simplifié, avec 6,5 points de komi pour les Blancs.

## À propos de l’ordinateur

L’ordinateur choisit ses coups avec une heuristique simple qui tient compte des captures, des libertés et de la proximité des pierres déjà posées. Il ne s’agit pas d’un moteur de Go avancé.

Le comptage des points est indicatif : le jeu ne propose pas d’étape de confirmation des pierres mortes ni de négociation des territoires.
