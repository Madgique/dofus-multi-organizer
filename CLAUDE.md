# DofusOrganizer — Contexte projet

## Projet actif
`M:\Projets-Windows\dofus\dofus-multi-organizer\DofusMultiOrganizer\DofusMultiOrganizer\`
Ancien code de référence archivé dans `M:\Projets-Windows\dofus\tests\organizer\`

## Statut
App lancée et fonctionnelle — build 0 erreur 0 warning, déploiement MSIX OK via F5 dans VS.

## Build
```
cd DofusMultiOrganizer\DofusMultiOrganizer
dotnet build DofusMultiOrganizer.slnx -c Debug -p:Platform=x64 --verbosity minimal
```
⚠️ Le `.slnx` est dans le **sous-dossier interne** : `…\DofusMultiOrganizer\DofusMultiOrganizer\` (deux niveaux de dossier avec le même nom).
Le fichier solution est `.slnx` (nouveau format VS 2022), pas `.sln`.

## Architecture
- **Stack** : C# / WinUI 3 (Windows App SDK 1.8.260209005) / MSIX packaged / net8.0-windows10.0.19041.0 / x64+x86+ARM64
- **Patterns** : MVVM (CommunityToolkit.Mvvm 8.3.2), DI (Microsoft.Extensions.DependencyInjection 9.0.2)
- **Tray** : H.NotifyIcon.WinUI 2.2.0
- **Win32 P/Invoke** : CsWin32 0.3.106 + P/Invoke manuels pour SetWindowLongPtrW/CallWindowProcW
- **Identity MSIX** : Name="dbfbf87b-1d83-42df-8525-84bccbb8176b", Publisher="CN=Madgique"

## Structure fichiers clés
```
DofusMultiOrganizer.csproj              — RootNamespace=DofusOrganizer, net8.0, AllowUnsafeBlocks
Package.appxmanifest                    — uap5:StartupTask + runFullTrust + userNotificationListener + 4 langues
NativeMethods.txt                       — entrées CsWin32 (RegisterHotKey, EnumWindows, GetKeyState…)
App.xaml/.cs                            — DI, Mutex single instance, point d'entrée
Views/MainWindow.xaml/.cs              — fenêtre settings cachée au démarrage + tray icon
Views/Controls/DofusWindowRow.xaml/.cs — row drag&drop de la liste
Views/Controls/HotkeyBox.xaml/.cs      — UserControl capture de touche (clic → appuyer une touche)
ViewModels/MainViewModel.cs            — logique principale
ViewModels/DofusWindowViewModel.cs
Models/DofusMode.cs                    — enum Unity = 0, Retro = 1
Models/AppSettings.cs                  — settings persistés (DofusMode, AutoFocusOnTurn, etc.)
Services/HotkeyService.cs              — WndProc subclassing manuel
Services/WindowDetectionService.cs     — EnumWindows + filtre UnityWndClass (Unity) ou Chrome_WidgetWin_1/IsDofusProcess (Rétro)
Services/WindowFocusService.cs         — AttachThreadInput + SetForegroundWindow
Services/NotificationListenerService.cs — UserNotificationListener WinRT pour auto-focus Rétro
Services/HotkeyParser.cs               — parse "Ctrl+F3" → (modifiers, vk) + Format inverse
Services/SettingsService.cs            — JSON dans %LocalAppData%/DofusOrganizer/
Strings/{fr-FR,en-US,pt-BR,es-ES}/Resources.resw
Assets/TrayIcon.ico                     — icône tray (placeholder à remplacer)
Assets/                                 — PNG placeholder (à remplacer par vraies icônes)
```

## Localisation — règle obligatoire
**TOUJOURS** passer par `x:Uid` + `Resources.resw` pour tout texte visible dans l'UI. Ne JAMAIS hardcoder une chaîne en français (ou toute autre langue) directement dans le XAML.

Workflow :
1. Ajouter la clé dans les 4 fichiers `Strings/{fr-FR,en-US,pt-BR,es-ES}/Resources.resw`
2. Référencer via `x:Uid="MaCle"` dans le XAML (la propriété cible est déduite automatiquement : `MaCle.Text`, `MaCle.Header`, `MaCle.ToolTipService.ToolTip`, etc.)
3. Pour les propriétés non-text (ex: `OffContent`/`OnContent` d'un ToggleSwitch), utiliser `x:Uid` avec le suffixe explicite dans la clé resw : `MaCle.OffContent`, `MaCle.OnContent`

Clés existantes (à compléter si nouvelles fonctionnalités) :
- `Tray_OpenSettings`, `Tray_StartWithWindows`, `Tray_Quit`
- `Settings_WindowsTitle.Text`, `Settings_Refresh.ToolTipService.ToolTip`
- `Settings_ColCharacter.Text`, `Settings_ColHotkey.Text`
- `Settings_GlobalHotkeys.Text`, `Settings_NextWindow.Text`, `Settings_PrevWindow.Text`
- `Settings_Language.Text`, `Settings_Startup.Header`
- `WindowRow_HotkeyPlaceholder`, `WindowRow_HotkeyTooltip`

## Points techniques importants
- **SetWindowLongPtr** : CsWin32 ne génère pas correctement → P/Invoke manuel `SetWindowLongPtrW`
- **ResourceLoader** : Windows App SDK → `new ResourceLoader()` (pas `GetForViewIndependentUse`)
- **MSIX déploiement** : nécessite `ProjectCapability Include="Msix"` dans .csproj ET `.slnx` avec `<Deploy />`
- **DISABLE_XAML_GENERATED_MAIN** : NE PAS utiliser, laisser WinUI générer le Main
- **Single instance** : Mutex (pas AppInstance.FindOrRegisterForKey)
- **Strings** : dossiers avec codes locale COMPLETS (fr-FR, en-US, es-ES, pt-BR) — sinon warning PRI257
- **Assets** : wildcard `<None Remove="Assets\**" /><Content Include="Assets\**">` dans .csproj
- **GetKeyState** : retourne `short`, check `< 0` pour savoir si la touche est enfoncée
- **RetroOptionsVisibility** : propriété calculée `Microsoft.UI.Xaml.Visibility` sur ViewModel (pas de converter XAML) — `Window` n'est pas `FrameworkElement` dans WinUI 3, donc `x:Bind` avec converter génère CS1503
- **`_isInitialized` guard** : `OnPropertyChanged(nameof(RetroOptionsVisibility))` doit être appelé AVANT le guard pour corriger l'affichage au démarrage en mode Rétro

## Dofus windows — Unity
- Classe Win32 : `UnityWndClass` (filtre suffisant, pas besoin de vérifier le processus)
- Format titre : `"CharacterName - DofusClass - Version - Release"` (ex: "Madgique-F - Feca - 3.5.3.1 - Release")
- Parse : `IndexOf(" - ")` (premier séparateur) pour CharacterName, deuxième pour DofusClass
- Ne PAS utiliser `LastIndexOf` — retournerait "Release" comme classe

## Dofus windows — Rétro
- Classes Win32 : `Chrome_WidgetWin_1` (client x64, Chromium embarqué) + `ShockwaveFlash` (client x86, obsolète depuis Flash EOL 2020)
- Filtre processus obligatoire : `IsDofusProcess()` → `Process.GetProcessById(pid).ProcessName.StartsWith("dofus")` — évite de catcher Chrome, Edge, Discord
- Format titre x64 : `"CharacterName - Dofus Retro v1.47.20"` → parse avec `IndexOf(" - Dofus", OrdinalIgnoreCase)`
- Format titre x86 : `"CharacterName"` uniquement → pas de séparateur, retourner tel quel

## Auto-focus Rétro (UserNotificationListener)
- Manifest : `<rescap:Capability Name="userNotificationListener" />`
- API : `Windows.UI.Notifications.Management.UserNotificationListener`
- Événement : `NotificationChanged` → `UserNotificationChangedKind.Added` uniquement
- Extraction : **uniquement le titre** (premier `GetTextElements()`) — format `"[CharacterName] - Dofus Retro v..."` — le destinataire est dans le titre, pas dans le corps
- Matching : `text.Contains(CharacterName, OrdinalIgnoreCase)`
- Thread safety : l'événement arrive sur un thread background → `_dispatcherQueue.TryEnqueue`
- Si accès refusé → désactiver silencieusement `AutoFocusOnTurn`
- Désactiver le son/bannière est OK. Désactiver entièrement les notifications casse le listener.

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
- Remplacer les assets PNG/ICO placeholder par de vraies icônes
- Signer le package MSIX pour distribution
