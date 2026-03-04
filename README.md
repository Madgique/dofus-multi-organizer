# DofusOrganizer

Outil de multi-boxing pour Dofus — gestion des fenêtres et raccourcis clavier globaux.

## Fonctionnalités

- **Détection automatique** des fenêtres Dofus ouvertes (classe Win32 `UnityWndClass`)
- **Raccourcis globaux** fenêtre suivante / précédente (cyclique)
- **Raccourci direct** par personnage (configurable individuellement)
- **Réordonnancement** par drag & drop
- **Capture de touche** : cliquer sur le champ de raccourci puis appuyer sur la touche souhaitée
- **System tray** : l'app tourne en arrière-plan, accessible via l'icône dans la barre des tâches
- **Démarrage avec Windows** configurable

## Limitations connues

- Les fenêtres Dofus **minimisées** dans la barre des tâches sont ignorées lors du Refresh. Il faut que les fenêtres soient ouvertes (non minimisées) pour être détectées.

## Utilisation

1. Lancer Dofus et ouvrir les comptes souhaités (fenêtres non minimisées)
2. Cliquer sur **Refresh** pour détecter les fenêtres
3. Configurer les raccourcis via les champs de capture (clic → touche)
4. Fermer la fenêtre settings → l'app continue en tray

## Build

```
cd DofusMultiOrganizer
dotnet build DofusMultiOrganizer.slnx -c Debug -p:Platform=x64
```

Ou via **F5** dans Visual Studio 2022 (déploiement MSIX).

## Stack

- C# / WinUI 3 / Windows App SDK 1.8
- .NET 8 / MSIX packaged / x64
- MVVM (CommunityToolkit.Mvvm) + DI (Microsoft.Extensions)
