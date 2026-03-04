# DofusOrganizer — Contexte projet

## Projet actif
`M:\Projets-Windows\dofus\dofus-multi-organizer\DofusMultiOrganizer\DofusMultiOrganizer\`
Ancien code de référence archivé dans `M:\Projets-Windows\dofus\tests\organizer\`

## Statut
App lancée et fonctionnelle — build 0 erreur 0 warning, déploiement MSIX OK via F5 dans VS.

## Build
```
cd DofusMultiOrganizer
dotnet build DofusMultiOrganizer.slnx -c Debug -p:Platform=x64 --verbosity minimal
```
Le fichier solution est `.slnx` (nouveau format VS 2022), pas `.sln`.

## Architecture
- **Stack** : C# / WinUI 3 (Windows App SDK 1.8.260209005) / MSIX packaged / net8.0-windows10.0.19041.0 / x64+x86+ARM64
- **Patterns** : MVVM (CommunityToolkit.Mvvm 8.3.2), DI (Microsoft.Extensions.DependencyInjection 9.0.2)
- **Tray** : H.NotifyIcon.WinUI 2.2.0
- **Win32 P/Invoke** : CsWin32 0.3.106 + P/Invoke manuels pour SetWindowLongPtrW/CallWindowProcW
- **Identity MSIX** : Name="dbfbf87b-1d83-42df-8525-84bccbb8176b", Publisher="CN=Magicked"

## Structure fichiers clés
```
DofusMultiOrganizer.csproj              — RootNamespace=DofusOrganizer, net8.0, AllowUnsafeBlocks
Package.appxmanifest                    — uap5:StartupTask + runFullTrust + 4 langues
NativeMethods.txt                       — entrées CsWin32 (RegisterHotKey, EnumWindows, GetKeyState…)
App.xaml/.cs                            — DI, Mutex single instance, point d'entrée
Views/MainWindow.xaml/.cs              — fenêtre settings cachée au démarrage + tray icon
Views/Controls/DofusWindowRow.xaml/.cs — row drag&drop de la liste
Views/Controls/HotkeyBox.xaml/.cs      — UserControl capture de touche (clic → appuyer une touche)
ViewModels/MainViewModel.cs            — logique principale
ViewModels/DofusWindowViewModel.cs
Services/HotkeyService.cs              — WndProc subclassing manuel
Services/WindowDetectionService.cs     — EnumWindows + filtre UnityWndClass uniquement
Services/WindowFocusService.cs         — AttachThreadInput + SetForegroundWindow
Services/HotkeyParser.cs               — parse "Ctrl+F3" → (modifiers, vk) + Format inverse
Services/SettingsService.cs            — JSON dans %LocalAppData%/DofusOrganizer/
Strings/{fr-FR,en-US,pt-BR,es-ES}/Resources.resw
Assets/TrayIcon.ico                     — icône tray (placeholder à remplacer)
Assets/                                 — PNG placeholder (à remplacer par vraies icônes)
```

## Points techniques importants
- **SetWindowLongPtr** : CsWin32 ne génère pas correctement → P/Invoke manuel `SetWindowLongPtrW`
- **ResourceLoader** : Windows App SDK → `new ResourceLoader()` (pas `GetForViewIndependentUse`)
- **MSIX déploiement** : nécessite `ProjectCapability Include="Msix"` dans .csproj ET `.slnx` avec `<Deploy />`
- **DISABLE_XAML_GENERATED_MAIN** : NE PAS utiliser, laisser WinUI générer le Main
- **Single instance** : Mutex (pas AppInstance.FindOrRegisterForKey)
- **Strings** : dossiers avec codes locale COMPLETS (fr-FR, en-US, es-ES, pt-BR) — sinon warning PRI257
- **Assets** : wildcard `<None Remove="Assets\**" /><Content Include="Assets\**">` dans .csproj
- **GetKeyState** : retourne `short`, check `< 0` pour savoir si la touche est enfoncée

## Dofus windows
- Classe Win32 : `UnityWndClass` (filtre suffisant, pas besoin de vérifier le nom du processus)
- Format titre : `"CharacterName - DofusClass - Version - Release"` (ex: "Madgique-F - Feca - 3.5.3.1 - Release")
- Parse : `IndexOf(" - ")` (premier séparateur) pour CharacterName, deuxième pour DofusClass
- Ne PAS utiliser `LastIndexOf` — retournerait "Release" comme classe

## HotkeyBox (Views/Controls/HotkeyBox.xaml.cs)
- `Hotkey` DependencyProperty (string, TwoWay bindable)
- Clic → mode capture → "Appuyez sur une touche…"
- Touche normale → capture avec modificateurs (Ctrl/Shift/Alt/Win via GetKeyState)
- Échap → annuler, Suppr/Backspace → effacer, perte focus → annuler
- Touches modificatrices seules ignorées pendant la capture

## Hotkeys (IDs dans HotkeyService)
- `ID_NEXT_WINDOW = 1` — fenêtre suivante (cyclique)
- `ID_PREV_WINDOW = 2` — fenêtre précédente (cyclique)
- `ID_WINDOW_BASE = 100` — hotkeys directs par fenêtre (100 + index)

## Todo restant
- Tester à l'exécution avec Dofus lancé (hotkeys, focus, drag&drop, HotkeyBox)
- Remplacer les assets PNG/ICO placeholder par de vraies icônes
- Vérifier que H.NotifyIcon ContextFlyout fonctionne au clic droit
- Vérifier le comportement hide-to-tray (Closed → args.Handled = true)
- Signer le package MSIX pour distribution
