# AE Edit Pipeline Orchestrator

Kompakte, transaktionssichere Windows-Desktop-Anwendung (**.NET 10 / .NET MAUI**) zur deterministischen Orchestrierung modularer **Adobe After Effects 2026 ExtendScript-Pipelines** für Edit-Workflows.

Statt monolithischer, blockierender `.jsx`-Dialog-Scripts steuert der Orchestrator kleine, zustandsunabhängige ES3-Module in kontrollierten Batches aus C# heraus, sichert Projekte vorab über SHA-256-verifizierte `.aep`-Snapshots und überwacht jeden Arbeitsschritt per Watchdog.

---

## Inhaltsverzeichnis

- [Features & Sicherheitskonzept](#features--sicherheitskonzept)
- [Architektur-Überblick](#architektur-überblick)
- [Voraussetzungen](#voraussetzungen)
- [Schnellstart](#schnellstart)
- [Pipeline-Module im Überblick](#pipeline-module-im-überblick)
- [Konfiguration (Settings)](#konfiguration-settings)
- [Weiterführende Dokumentation](#weiterführende-dokumentation)

---

## Features & Sicherheitskonzept

- **Modulare Pipeline (`00`–`08` + `99`)**: Jeder Bearbeitungsschritt läuft isoliert in konfigurierbaren Clip-Batches (Standard: 10 Clips), gefolgt von gezieltem RAM-Purge (`app.purge(PurgeTarget.ALL_CACHES)`) und optionalem Projekt-Save.
- **Headless ExtendScript (ES3)**: Keine `alert()`, `confirm()`, `prompt()` oder ScriptUI-Fenster – Status, Fortschritt pro Clip und Fehler werden ausschließlich über JSON-IPC gemeldet.
- **Snapshot-Transaktionen & Atomarer Rollback**: Vor jedem Lauf erstellt der `FileSnapshotTransactionManager` eine exakte Kopie (`00_Initial_Snapshot.aep`) inklusive SHA-256-Hash und `manifest.json` im Projekt-Unterordner `_AEP_Backups/`. Optional wird vor Stufe 2 ein Zwischen-Snapshot (`01_After_Stage1.aep`) angelegt. Bei Fehlern, Watchdog-Timeout oder Nutzer-Abbruch schließt `close_no_save.jsx` das Projekt ohne Speichern, stellt den Snapshot atomar per temporärer Datei und `File.Move` wieder her, prüft den SHA-256-Hash und öffnet die `.aep` erneut in After Effects.
- **Granulares Reverse-Recovery**: Über `99_ReverseRollback.jsx` lassen sich Stufe 2 (`ZoomGroup` + `BlurZoom`), Stufe 1 (`Pre-comp` + `StartBlur`/`EndBlur`) oder beide Stufen direkt in der offenen Komposition entpacken.
- **Abhängigkeits-Engine (`StepDependencyValidator`)**: Harmonisiert Schritt-Auswahl und erzwingt synchrone Richtungen (`Vertical` / `Horizontal`) zwischen Shakes, 10f-StartBlurs und 90°/180°-EndBlurs; erkennt bestehende `[EDIT_CLIP:X]`-, `[DONE_PIPELINE_1]`- und `[DONE_ZOOM_GROUP]`-Tags im Preflight.
- **Dark-Mode-First Desktop-UI**: Kompaktes `980 × 680 px` Fenster mit drei Bereichen: **Pipeline**, **Settings** und **Backups & Recovery**.

---

## Architektur-Überblick

```text
+-------------------------------------------------------------------+
| AEEditOrchestrator.App (.NET 10 MAUI Windows Desktop, MVVM)       |
|  - PipelinePage / SettingsPage / RecoveryPage                     |
|  - EmbeddedScriptExtractor -> %LocalAppData%\...\scripts\         |
+---------------------------------+---------------------------------+
                                  |
                                  v
+-------------------------------------------------------------------+
| AEEditOrchestrator.Core (.NET 10, UI-unabhängig)                  |
|  - PipelineOrchestrator, StepRegistry, StepDependencyValidator    |
|  - IPipelineStep (00_preflight .. 08_blurzoom, 99_reverse)        |
+---------------------------------+---------------------------------+
                                  |
                                  v
+-------------------------------------------------------------------+
| AEEditOrchestrator.Infrastructure (IPC, Watchdog & Snapshots)     |
|  - AfterEffectsCliBridge (AfterFX.exe -r runner.jsx)              |
|  - IpcFileWatcher + HeartbeatWatchdog (%LocalAppData%\...\ipc\)   |
|  - FileSnapshotTransactionManager (_AEP_Backups + SHA-256)        |
|  - JsonSettingsStore (%LocalAppData%\AEEditOrchestrator\settings) |
+---------------------------------+---------------------------------+
                                  | job.json / heartbeat.json / result.json
                                  v
+-------------------------------------------------------------------+
| Adobe After Effects 2026 (ES3 ExtendScript)                       |
|  - runner.jsx + _lib_core.jsxinc + 00..08 / 99 / close_no_save    |
|  - Sprachunabhängige AE-Match-Names (ADBE Tile, ADBE Motion Blur) |
+-------------------------------------------------------------------+
```

---

## Voraussetzungen

- **Betriebssystem**: Windows 11 (x64)
- **Host-Anwendung**: Adobe After Effects 2026 (`AfterFX.exe`)
- **After Effects Berechtigung**:
  Unter *Edit → Preferences → Scripting & Expressions* (DE: *Bearbeiten → Voreinstellungen → Skripterstellung und Expressions*) muss **„Allow Scripts to Write Files and Access Network“** aktiviert sein, damit `runner.jsx` die IPC-Dateien lesen/schreiben kann.
- **Benötigte Effekt-Presets (`.ffx`)** im After Effects `User Presets`-Ordner (auch Unterordner werden rekursiv durchsucht):
  - `FadeInOut.ffx`
  - `VerticalShake.ffx`
  - `VerticalBlur.ffx`
  - `HorizontalShake.ffx`
  - `HorizontalBlur.ffx`
  - `BlurZoomIn.ffx`
  - `BlurZoomOut.ffx`
- **Für Entwickler-Builds**: .NET 10 SDK (`10.0.x`) mit Windows-Desktop-Targeting (`net10.0-windows10.0.19041.0`).

---

## Schnellstart

### Variante A: Fertige Release-`.exe` ausführen

1. Die gepublishte Anwendung liegt als eigenständige **Self-Contained Single-File-Executable** vor (`AEEditOrchestrator.App.exe`, ohne separate `.NET`-Runtime- oder WinAppSDK-Installation lauffähig).
2. Beim Start extrahiert `EmbeddedScriptExtractor` automatisch alle 13 eingebetteten `.jsx`/`.jsxinc`-Dateien mit SHA-256-Prüfung nach:
   ```text
   %LocalAppData%\AEEditOrchestrator\scripts\
   ```
   Dadurch kann `AfterFX.exe -r` direkt auf die aktuellen Scripts auf der Festplatte zugreifen, während die `.exe` portabel bleibt.
3. Öffne in After Effects 2026 dein gespeichertes `.aep`-Projekt und die Ziel-Komposition, aktiviere **Caps Lock** (pausiert AE-Frame-Vorschau) und klicke im Orchestrator auf **Preflight prüfen**.

> **Hinweis zum Single-File-Publish:** Alle WinUI-3-/Windows-App-SDK-Laufzeitkomponenten und `.pri`-Ressourcen sind vollständig in der Single-File-`.exe` gebündelt (`WindowsAppSDKSelfContained=true`, `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`). Beim allerersten Start nach dem Entpacken kann der Erststart wenige Sekunden dauern, da .NET das Bundle einmalig in `%LocalAppData%\Temp\.net\` entpackt.

### Variante B: Selbst bauen, testen & publishen

```powershell
# Debug-Build der gesamten Solution
dotnet build AEEditOrchestrator.sln -c Debug

# Alle 83 xUnit-Tests (Core + Infrastructure) ausführen
dotnet test AEEditOrchestrator.sln

# Self-Contained Single-File Release Build nach ./Temp erstellen
dotnet publish AEEditOrchestrator.App\AEEditOrchestrator.App.csproj -c Release -r win-x64 -o Temp -p:DebugType=None -p:DebugSymbols=false
```

---

## Pipeline-Module im Überblick

| Step-ID | Script | Aufgabe (Single Responsibility) | Batching | Voraussetzung |
| :--- | :--- | :--- | :--- | :--- |
| `00_preflight` | `00_PreflightCheck.jsx` | Prüft aktive Comp, gespeicherte `.aep`, `.ffx`-Presets, gültige Clips und vorhandene Tags | Nein | Automatisch vor Start |
| `01_beats` | `01_BeatDetection.jsx` | Erkennt Beats/Overlay-Wechsel per `sampleImage()` (`RedOverlay` / `UniversalFourZone`) und setzt Marker | Nein | Standalone (1 Video-Layer markiert) |
| `02_precomp` | `02_PrecomposeClips.jsx` | Precompost gültige Roh-Clips einzeln (`Move all attributes`), trimmt Precomp-Dauer und setzt `[EDIT_CLIP:X]` | Ja | Keine |
| `03_fade` | `03_ApplyFadeInOut.jsx` | Wendet `FadeInOut.ffx` an und streckt alle Keyframes proportional auf `inPoint` bis `outPoint` | Ja | `02_precomp` oder `[EDIT_CLIP:X]` |
| `04_shakes` | `04_ApplyMotionTileAndShakes.jsx` | Fügt `ADBE Tile` (370/370, Mirror Edges) hinzu und wendet abwechselnd `VerticalShake`/`HorizontalShake` an | Ja | `02_precomp` oder `[EDIT_CLIP:X]` |
| `05_startblur` | `05_CreateStartBlurs.jsx` | Erstellt 10-Frame Adjustment Layer `StartBlur_V/H_X`, fügt `Motion Tile` + `VerticalBlur`/`HorizontalBlur` hinzu | Ja | `02_precomp` + synchron zu `04_shakes` |
| `06_endblurs` | `06_CreateEndBlurs.jsx` | Erstellt zwei 1-Frame Adjustment Layers (`EndBlur200` → `EndBlur80`) mit `ADBE Motion Blur` (90°/180°); setzt `[DONE_PIPELINE_1]` | Ja | `02_precomp` + synchron zu `04_shakes` |
| `07_zoomgroup` | `07_GroupClipsForZoom.jsx` | Gruppiert Clip + Blur-Adjustments zu `ZoomGroup_X_In/Out`, trimmt Dauer und setzt `[DONE_ZOOM_GROUP]` | Ja | `02_precomp` oder Stage-1-Tags |
| `08_blurzoom` | `08_ApplyBlurZoom.jsx` | Erstellt `BlurZoom_In/Out_X` Adjustment Layer, optional mit `Motion Tile`, wendet `BlurZoomIn`/`BlurZoomOut` an und streckt Keyframes | Ja | `07_zoomgroup` oder `[DONE_ZOOM_GROUP]` |
| `99_reverse` | `99_ReverseRollback.jsx` | Entpackt gezielt Stufe 2, Stufe 1 oder komplett zurück in die Hauptkomposition und löscht unbenutzte Precomps | Nein | Manuelles Recovery-Tool |

---

## Konfiguration (Settings)

Alle Einstellungen werden unter `%LocalAppData%\AEEditOrchestrator\settings.json` gespeichert und im Tab **Settings** in drei Gruppen verwaltet:

1. **Pfade & Stabilität**:
   - `AfterFX.exe`-Pfad (Auto-Detection für AE 2026 & Registry-Fallback)
   - `User Presets`-Ordner (Auto-Detection unter `Dokumente\Adobe\After Effects 2026\User Presets`)
   - `Batch-Größe` (Standard: `10`), `Watchdog-Timeout` (Standard: `45 s`), `Max. Backups` (Standard: `5`)
   - Toggles: `SaveAfterBatch` und `PurgeAfterBatch`
2. **Geschützte Layer & Auswahl**:
   - Editierbare Ausschlussliste (Standard: `Adjustment Layer 1`, `Black Solid 9`, `Black Solid 10`, `ETERNALS`)
   - Toggle `Nur markierte Clips verarbeiten` (`UseSelectedOnly`)
3. **Presets & Effekt-Parameter**:
   - Preset-Namen-Mapping für alle 7 `.ffx`-Dateien + Button **Presets jetzt validieren**
   - `Motion Tile` Breite/Höhe (`370`/`370`), `StartBlur Frames` (`10`), `EndBlur` Längen (`200`/`80`), Blur-Winkel (`90°`/`180°`), `StartDirection Stage 1` (`Vertical`/`Horizontal`), `BlurZoom Start Stage 2` (`BlurZoomIn`/`BlurZoomOut`), Toggle `Motion Tile auf BlurZoom-Layer hinzufügen`
   - `Beat-Detection`: Modus (`RedOverlay` / `UniversalFourZone`), `Empfindlichkeit` (`5.0`), `MinFrameGap` (`2`), `Marker-Ziel` (`Composition` / `Layer`), `OnlyWorkArea`

---

## Weiterführende Dokumentation

- Schritt-für-Schritt-Testanleitung für After Effects 2026: [Docs/TESTING_GUIDE.md](Docs/TESTING_GUIDE.md)
- Technische Spezifikation & Architekturkonzept: [Docs/Reference/AE_Edit_Pipeline_Orchestrator_Konzept.pdf](Docs/Reference/AE_Edit_Pipeline_Orchestrator_Konzept.pdf)
