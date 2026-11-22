# After Effects 2026 – Test- & Verifikationsanleitung

Diese Schritt-für-Schritt-Anleitung dient dazu, alle Funktionen des **AE Edit Pipeline Orchestrator** systematisch und risikofrei in einer echten **Adobe After Effects 2026**-Session zu testen.

---

## 1. Vorbereitung in After Effects 2026

Bevor du den ersten Test startest, prüfe diese vier Punkte:

1. **Scripting-Berechtigung aktivieren (einmalig):**
   - Öffne in After Effects *Edit → Preferences → Scripting & Expressions* (DE: *Bearbeiten → Voreinstellungen → Skripterstellung und Expressions*).
   - Aktiviere **„Allow Scripts to Write Files and Access Network“** (DE: *Skripten das Schreiben von Dateien und den Netzwerkzugriff erlauben*).
   - Ohne diese Option können die JSX-Module weder `job.json` lesen noch `heartbeat.json` / `result.json` schreiben.
2. **Projekt & Komposition vorbereiten:**
   - Öffne ein Test-Projekt (am besten eine **Kopie** eines echten Edit-Projekts) und speichere es mindestens einmal als `.aep` auf der Festplatte.
   - Öffne die Ziel-Edit-Komposition als **aktive Komposition** im Viewer/Timeline-Panel.
3. **Geschützte Layer-Struktur überprüfen:**
   - **Layer `1` (ganz oben)** (z. B. Audio-/Master-Layer) und **letzter Layer (ganz unten)** (z. B. `Black Solid 10`) werden automatisch per Layer-Index geschützt.
   - Folgende Layer werden zusätzlich automatisch geschützt (case-insensitiv, konfigurierbar unter *Settings → Geschützte Layer*):
     - `Adjustment Layer 1`
     - `Black Solid 9`
     - `Black Solid 10`
     - Alle Layer mit `ETERNALS` im Namen sowie alle `TextLayer`, sonstige `AdjustmentLayer` und Video-lose `AVLayer`.
4. **Performance-Schutz aktivieren:**
   - **Aktiviere `CAPS LOCK` (Feststelltaste)** auf deiner Tastatur! Dadurch pausiert After Effects das Frame-Rendering in der Vorschau während der Batch-Ausführung.

---

## 2. Systematischer Testplan (Step-by-Step)

### Schritt 1: Verbindung, Settings & Preflight-Check testen

1. Starte `AEEditOrchestrator.App.exe` (z. B. aus `./Temp`).
2. Wechsle links auf **Settings**:
   - Prüfe den Pfad zu `AfterFX.exe` (ggf. **Auto-Erkennen** klicken).
   - Prüfe den **User Presets Ordner** (ggf. **Auto-Erkennen** klicken).
   - Klicke auf **Presets jetzt validieren** und prüfe, dass alle 7 Presets (`FadeInOut`, `VerticalShake`, `VerticalBlur`, `HorizontalShake`, `HorizontalBlur`, `BlurZoomIn`, `BlurZoomOut`) den Status **Gefunden** zeigen.
   - Klicke bei Bedarf auf **Einstellungen speichern**.
3. Wechsle links zurück auf **Pipeline** und klicke rechts unten auf **Preflight prüfen**:
   - **Erwartetes Ergebnis im Status-Banner:**
     - Verbindungs-Badge wechselt auf grün (`AE 2026 verbunden`).
     - Projektname (`.aep`), aktive Komposition und gültige Clip-Anzahl werden korrekt angezeigt.
     - Falls das Projekt bereits Tags (`[EDIT_CLIP]`, `[DONE_PIPELINE_1]`, `[DONE_ZOOM_GROUP]`) enthält, leuchten die entsprechenden Tag-Pills auf.

---

### Schritt 2: Beat-Detection (`01_BeatDetection`) testen

1. Markiere in deiner offenen AE-Komposition **genau einen** Video-Layer (z. B. dein Template-/Referenz-Video).
2. Im Tab **Pipeline**:
   - Setze den Haken **nur** bei `01_BeatDetection`.
   - Wähle im Quick-Dropdown **Beat-Modus** entweder `Rot-Overlay` (für Template-Clips mit hartem Rot-Overlay) oder `Universal 4-Zonen` (für allgemeine Schnitte/Beats).
   - Klicke auf **Pipeline starten**.
3. **Prüf-Checkliste in After Effects:**
   - [ ] Fortschrittsbalken und Live-Log aktualisieren sich flüssig alle 25 Frames per `heartbeat.json`.
   - [ ] Auf der Komposition (bzw. dem Layer, je nach Setting *Marker-Ziel*) wurden nummerierte `Beat 1`, `Beat 2`, … Marker mit abwechselnden Farben (Rot/Cyan) gesetzt.
   - [ ] Der temporäre Effekt `__TempBeatSampler` wurde nach Abschluss sauber vom Layer entfernt.
   - [ ] Falls kein Layer oder ein falscher Layer markiert war, erscheint eine saubere Fehlermeldung im Live-Log (und **kein** blockierendes AE-Popup).

---

### Schritt 3: Stufe 1 (`02` bis `06`) an 2–3 markierten Clips testen

Teste die Edit-Pipeline zuerst im kleinen Maßstab, bevor du eine ganze Timeline verarbeitest:

1. Markiere in After Effects **2 oder 3 Roh-Clips** in deiner Timeline.
2. Im Orchestrator auf der Seite **Pipeline**:
   - Aktiviere die Checkbox **Nur markierte Clips verarbeiten**.
   - Aktiviere ausschließlich die Schritte:
     - `02_PrecomposeClips`
     - `03_ApplyFadeInOut`
     - `04_ApplyMotionTileAndShakes`
     - `05_CreateStartBlurs`
     - `06_CreateEndBlurs`
   - Deaktiviere `07_GroupClipsForZoom` und `08_ApplyBlurZoom` zunächst.
   - Stelle **Start-Richtung Stage 1** auf `Vertical` (oder `Horizontal`).
3. Klicke auf **Pipeline starten**.
4. **Prüf-Checkliste in After Effects:**
   - [ ] Unter `<ProjektOrdner>\_AEP_Backups\<Timestamp>_<TxId>\` wurde vor Beginn `00_Initial_Snapshot.aep` + `manifest.json` erstellt.
   - [ ] Jeder markierte Clip wurde zu `Pre-comp <N> - <Name>` precompost (*Move all attributes*); die Precomp-Dauer entspricht exakt `outPoint - inPoint`, der innere Clip beginnt bei `0` der Precomp und liegt wieder an derselben Timeline-Position.
   - [ ] Auf den Precomps steht im Kommentar `[DONE_PIPELINE_1] [EDIT_CLIP:<N>]`.
   - [ ] Auf jeder Precomp ist `Motion Tile` (`370` / `370`, *Mirror Edges* aktiviert) angewendet.
   - [ ] `FadeInOut.ffx` wurde angewendet und alle animierten Keyframes sind proportional auf die volle Länge (`inPoint` bis `outPoint`) gestreckt (inkl. erhaltenem Temporal-/Spatial-Ease).
   - [ ] Shake- und Blur-Richtungen wechseln sauber ab (bei Start `Vertical`: Clip 1 = `V`, Clip 2 = `H`, Clip 3 = `V`).
   - [ ] Über jedem Clip liegt ein `StartBlur_V/H_<N>` Adjustment Layer mit **10 Frames** Länge, `Motion Tile` und dem passenden `VerticalBlur`/`HorizontalBlur` Preset.
   - [ ] Am Clip-Ende liegen zwei **1-Frame** Adjustment Layers in korrekter Reihenfolge: `EndBlur200_V/H_<N>` (vorletzter Frame, Length `200`) und `EndBlur80_V/H_<N>` (letzter Frame, Length `80`) mit Richtungsunschärfe (`90°` bei Vertical, `180°` bei Horizontal).
   - [ ] Alle geschützten Layer (`Layer 1`, letzter Layer, `Adjustment Layer 1`, `Black Solid 9/10`, `ETERNALS`) blieben völlig unverändert.

---

### Schritt 4: Stufe 2 (`07` + `08`) testen

Nutze die in Schritt 3 erzeugten (oder markierten) Stage-1-Clips:

1. Markiere in After Effects die verarbeiteten Basis-Clips (die Precomps aus Schritt 3) oder lasse die Auswahl auf **alle** gültigen Clips, wenn in der Test-Comp nur diese Clips existieren.
2. Im Orchestrator auf **Pipeline**:
   - Deaktiviere `02`–`06` und aktiviere `07_GroupClipsForZoom` sowie `08_ApplyBlurZoom`.
   - Prüfe das Dependency-Badge: Nach einem **Preflight prüfen** erkennt Schritt 7 die vorhandenen `[EDIT_CLIP]`/`[DONE_PIPELINE_1]`-Tags; falls du `08_ApplyBlurZoom` anhakst, wird `07_GroupClipsForZoom` automatisch mitaktiviert, solange noch keine `[DONE_ZOOM_GROUP]`-Tags existieren.
   - Wähle **Zoom-Start Stage 2** (`BlurZoomIn` oder `BlurZoomOut`).
3. Klicke auf **Pipeline starten**.
4. **Prüf-Checkliste in After Effects:**
   - [ ] Jede Clip-Precomp wurde zusammen mit ihren zugehörigen `StartBlur`- und `EndBlur`-Adjustment-Layern in `ZoomGroup_<N>_In/Out` precompost, exakt auf Clip-Länge getrimmt und mit `[DONE_ZOOM_GROUP]` getaggt.
   - [ ] Direkt über jeder `ZoomGroup` liegt ein `BlurZoom_In/Out_<N>` Adjustment Layer (`[DONE_ZOOM_ADJ]`) über die gesamte Clip-Länge.
   - [ ] `BlurZoomIn.ffx` und `BlurZoomOut.ffx` wechseln pro Clip sauber ab, Keyframes sind auf Clip-Länge gestreckt (optional inkl. `Motion Tile`, falls in Settings aktiviert).

---

### Schritt 5: Rollback & Recovery testen

#### Teil A: Snapshot-Restore aus `_AEP_Backups`
1. Wechsle links auf **Backups & Recovery**.
2. Prüfe, ob das aktuelle `.aep`-Projekt automatisch übernommen wurde (sonst über **Snapshots laden** aktualisieren).
3. In der Liste unter **VERFÜGBARE SNAPSHOTS (_AEP_BACKUPS)** sollten die Transaktionen aus Schritt 3 und 4 mit Zeitstempel, Status `Committed` und grünem SHA-256-Integritäts-Badge (`Integrität OK`) erscheinen.
4. Wähle den älteren Snapshot (vor Schritt 3) und klicke auf **Diesen Stand wiederherstellen**.
5. **Erwartetes Ergebnis:**
   - After Effects schließt das veränderte Projekt ohne Speichern (`close_no_save.jsx`), die Original-`.aep` wird atomar über eine temporäre Datei und `File.Move` wiederhergestellt, per SHA-256 verifiziert und automatisch wieder in After Effects geöffnet.
   - In After Effects liegt die Timeline wieder exakt im Ausgangszustand vor Schritt 3 vor.

#### Teil B: Granulares Reverse-Script (`99_ReverseRollback`)
1. Führe Stufe 1 + Stufe 2 erneut auf den 2–3 Test-Clips aus (damit `ZoomGroup_` und `Pre-comp ` vorhanden sind).
2. Wechsle in **Backups & Recovery** zum Bereich **GRANULARES REVERSE-SCRIPT AUSFÜHREN**:
   - Teste zuerst **`Stufe 2 (Nur BlurZoom & ZoomGroup)`** → Klick auf **99_ReverseRollback ausführen**:
     - `BlurZoom_`-Adjustment-Layers werden gelöscht, `ZoomGroup_`-Precomps wieder in `Pre-comp` + `StartBlur`/`EndBlur`-Layers entpackt und unbenutzte `ZoomGroup_`-Comps aus dem Projektfenster entfernt.
   - Teste anschließend **`Stufe 1 (Pre-comp & Start-/End-Blurs)`** (oder direkt **`Komplett (Stufe 2 + Stufe 1)`**):
     - `StartBlur_`/`EndBlur_`-Layers werden entfernt, die `Pre-comp`-Layers zurück in die ursprünglichen Roh-Clips entpackt und unbenutzte `Pre-comp`-Comps gelöscht.

---

### Schritt 6: Vollständiger Durchlauf über alle Clips (Batching, Watchdog & Resume)

1. Öffne ein vollständiges Edit-Projekt (z. B. 40–100+ Clips).
2. Stelle unter **Settings** ein: `Batch-Größe = 10`, `Watchdog-Timeout = 45`, `Max. Backups = 5`, `SaveAfterBatch = true`, `PurgeAfterBatch = true`.
3. Deaktiviere auf **Pipeline** die Option **Nur markierte Clips verarbeiten**, wähle alle Schritte `02` bis `08`, prüfe **Caps Lock = AN** und starte mit **Pipeline starten**.
4. **Prüf-Checkliste:**
   - [ ] Zwischen Batch 1, 2, 3 … aktualisiert sich die UI flüssig ohne AE-Freeze; After Effects speichert nach jedem Batch und leert den RAM-Cache.
   - [ ] Zwischen Stufe 1 (`06_endblurs`) und Stufe 2 (`07_zoomgroup`) wird im Transaktionsordner der Meilenstein-Snapshot `01_After_Stage1.aep` angelegt.
   - [ ] **Abbruch-Test (optional):** Klicke während eines laufenden Batches auf **Abbrechen & Rollback** → laufender Schritt stoppt sauber nach dem aktuellen Batch bzw. per Watchdog-Cancellation und stellt `00_Initial_Snapshot.aep` atomar wieder her.
   - [ ] **Backup-Rotation:** Nach > 5 Läufen bleiben maximal 5 Snapshot-Ordner in `_AEP_Backups/` erhalten; ältere werden automatisch bereinigt.

---

## 3. Troubleshooting

| Symptom / Fehlermeldung | Ursache | Lösung |
| :--- | :--- | :--- |
| Timeout oder keine `result.json` beim ersten Ausführen | After Effects darf keine Dateien schreiben/lesen | In AE *Voreinstellungen → Skripterstellung & Expressions* → **„Skripten das Schreiben von Dateien und den Netzwerkzugriff erlauben“** aktivieren und AE neu starten. |
| `Fehlende Presets` im Preflight oder Preset-Check | `.ffx`-Dateien heißen anders oder liegen in anderem Verzeichnis | Unter **Settings** den korrekten `User Presets`-Pfad per **Auto-Erkennen** setzen bzw. die 7 Preset-Namen exakt an deine `.ffx`-Dateien anpassen. |
| `Das aktive Projekt muss zuerst gespeichert werden` | Neue, ungespeicherte AE-Projektdatei (`app.project.file === null`) | Projekt einmalig in After Effects speichern (`Strg+S`), danach **Preflight prüfen** erneut ausführen. |
| `Keine aktive Komposition` | Timeline/Project-Panel aktiv statt Comp-Viewer | Doppelklick auf deine Edit-Komposition, sodass die Timeline/Komposition aktiv fokussiert ist. |
| Pipeline läuft sehr langsam / hoher RAM-Verbrauch | **Caps Lock** ist ausgeschaltet | `CAPS LOCK` aktivieren, damit AE während der Bearbeitung keine Vorschau-Frames rendert; ggf. `Batch-Größe` auf `5`–`8` reduzieren. |
| `Watchdog timeout after 45 seconds` | Sehr lange Clips oder Beat-Detection auf vielen Frames ohne Heartbeat-Update | In **Settings** `Watchdog-Timeout (s)` z. B. auf `90`–`120` erhöhen oder bei der Beat-Detection `OnlyWorkArea` aktivieren. |
| Nach einem harten AE-Absturz lässt sich die `.aep` nicht überschreiben | Hängender `AfterFX.exe`-Prozess hält File-Lock | Der Orchestrator versucht automatisch `TryRestartAfterEffectsAsync` / Prozessbeendigung; im Zweifel im Task-Manager `AfterFX.exe` schließen und über **Backups & Recovery → Diesen Stand wiederherstellen** recovern. |
