/*
 * AutoBeatMarkers.jsx
 * Erkennt harte Rot-Overlay-Wechsel (Template-Beats) framegenau und setzt AE-Marker.
 * Kompatibel mit After Effects 2026 (sprachunabhängige Match-Names).
 */

(function () {
    // ================= KONFIGURATION =================
    var USE_COMP_MARKERS = true;   // true = Marker oben auf der Komposition | false = Marker direkt auf dem Layer
    var ONLY_WORK_AREA   = false;  // true = nur Arbeitsbereich scannen | false = gesamte Layer-Dauer scannen
    var SAMPLE_X_RATIO   = 0.85;   // Messpunkt X (0.85 = rechts, frei von Text & Logo)
    var SAMPLE_Y_RATIO   = 0.15;   // Messpunkt Y (0.15 = oben)
    var SAMPLE_RADIUS    = 15;     // Messradius in Pixeln (klein = schnell)
    var MIN_FRAME_GAP    = 2;      // Mindestabstand in Frames zwischen zwei Markern (Anti-Flacker)
    // =================================================

    var comp = app.project.activeItem;
    if (!comp || !(comp instanceof CompItem)) {
        alert("Bitte öffne zuerst eine Komposition.");
        return;
    }

    if (comp.selectedLayers.length !== 1) {
        alert("Bitte markiere genau EINEN Video-Layer (das Template-Video).");
        return;
    }

    var layer = comp.selectedLayers[0];
    if (!(layer instanceof AVLayer)) {
        alert("Der ausgewählte Layer ist kein Video-Layer.");
        return;
    }

    // Zeitbereich bestimmen
    var startTime = Math.max(0, layer.inPoint);
    var endTime   = Math.min(comp.duration, layer.outPoint);

    if (ONLY_WORK_AREA) {
        startTime = Math.max(startTime, comp.workAreaStart);
        endTime   = Math.min(endTime, comp.workAreaStart + comp.workAreaDuration);
    }

    var frameDur    = comp.frameDuration;
    var totalFrames = Math.floor((endTime - startTime) / frameDur);

    if (totalFrames <= 1) {
        alert("Der ausgewählte Zeitbereich ist zu kurz.");
        return;
    }

    // Fortschrittsfenster (ScriptUI)
    var win = new Window("palette", "Beat-Erkennung läuft...", undefined, { closeButton: false });
    win.alignChildren = ["fill", "top"];
    var statusText = win.add("statictext", undefined, "Analysiere Frames: 0 / " + totalFrames);
    statusText.preferredSize.width = 300;
    var progressBar = win.add("progressbar", undefined, 0, totalFrames);
    progressBar.preferredSize.width = 300;
    win.show();

    app.beginUndoGroup("Auto Beat Markers");

    var sliderEffect = null;

    try {
        // Temporären Slider-Effekt für sampleImage() anlegen (sprachunabhängig via Match-Name)
        var effects = layer.property("ADBE Effect Parade");
        sliderEffect = effects.addProperty("ADBE Slider Control");
        sliderEffect.name = "__TempRednessSampler";

        var sliderProp = sliderEffect.property("ADBE Slider Control-0001");
        sliderProp.expression =
            "var c = sampleImage([width * " + SAMPLE_X_RATIO + ", height * " + SAMPLE_Y_RATIO + "], [" + SAMPLE_RADIUS + ", " + SAMPLE_RADIUS + "], false, time);\n" +
            "(c[0] - (c[1] + c[2]) * 0.5) * 100;";

        var values = [];
        var times  = [];
        var minVal = 999999;
        var maxVal = -999999;

        // Pass 1: Rot-Anteil für jeden Frame auslesen
        for (var i = 0; i <= totalFrames; i++) {
            var t = startTime + (i * frameDur);
            var val = sliderProp.valueAtTime(t, false);

            values.push(val);
            times.push(t);

            if (val < minVal) minVal = val;
            if (val > maxVal) maxVal = val;

            // UI alle 25 Frames updaten, damit AE nicht einfriert
            if (i % 25 === 0 || i === totalFrames) {
                progressBar.value = i;
                statusText.text = "Analysiere Frames: " + i + " / " + totalFrames;
                win.update();
            }
        }

        // Prüfen, ob überhaupt ein klarer Farbwechsel vorhanden ist
        var spread = maxVal - minVal;
        if (spread < 5) {
            alert("Es konnte kein eindeutiges rotes Overlay erkannt werden (Kontrast zu gering: " + spread.toFixed(2) + ").\nPrüfe ggf. SAMPLE_X_RATIO / SAMPLE_Y_RATIO.");
            return;
        }

        // Dynamischer Schwellenwert exakt zwischen Normal und Rot-Overlay
        var threshold = minVal + (spread * 0.5);

        // Pass 2: Zustandswechsel erkennen & Marker setzen
        var markerTarget = USE_COMP_MARKERS ? comp.markerProperty : layer.property("ADBE Marker");
        var prevIsRed = values[0] > threshold;
        var lastMarkerFrame = -MIN_FRAME_GAP - 1;
        var beatCount = 0;

        for (var j = 1; j < values.length; j++) {
            var currentIsRed = values[j] > threshold;

            if (currentIsRed !== prevIsRed) {
                if ((j - lastMarkerFrame) >= MIN_FRAME_GAP) {
                    beatCount++;
                    var markerVal = new MarkerValue("Beat " + beatCount);
                    // Abwechselnde Marker-Farben (1 = Rot, 9 = Grün/Cyan) zur besseren Übersicht
                    markerVal.label = currentIsRed ? 1 : 9;
                    markerTarget.setValueAtTime(times[j], markerVal);

                    lastMarkerFrame = j;
                }
                prevIsRed = currentIsRed;
            }
        }

        win.close();
        alert("Fertig! Es wurden " + beatCount + " Beat-Marker gesetzt.");

    } catch (err) {
        alert("Fehler beim Ausführen des Scripts:\n" + err.toString() + " (Zeile " + err.line + ")");
    } finally {
        // Temporären Effekt garantiert wieder entfernen
        if (sliderEffect !== null) {
            try {
                sliderEffect.remove();
            } catch (e) {}
        }
        if (win) {
            try {
                win.close();
            } catch (e) {}
        }
        app.endUndoGroup();
    }
})();