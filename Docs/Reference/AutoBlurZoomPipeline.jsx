/*
 * AutoBlurZoomPipeline.jsx
 * Gruppiert jeden Clip-Block (Precomp + zugehörige Shake/Blur Adjustment Layers)
 * in eine neue Precomp, legt ein Adjustment Layer über die gesamte Clip-Länge darüber
 * und wendet abwechselnd BlurZoomIn / BlurZoomOut mit dynamischer Keyframe-Streckung an.
 * Inkl. Batch-Undo, Auto-Save, RAM-Purge & Resume-Schutz.
 */

(function () {
    // ================= KONFIGURATION =================
    var EXCLUDED_NAMES = [
        "adjustment layer 1",
        "black solid 9",
        "black solid 10",
        "eternals"
    ];

    var PRESET_NAMES = {
        blurZoomIn:  "BlurZoomIn",
        blurZoomOut: "BlurZoomOut"
    };

    var MOTION_TILE_WIDTH  = 370;
    var MOTION_TILE_HEIGHT = 370;
    // =================================================

    var comp = app.project.activeItem;
    if (!comp || !(comp instanceof CompItem)) {
        alert("Bitte öffne zuerst deine Edit-Komposition.");
        return;
    }

    // ================= EINSTELLUNGS-DIALOG =================
    var dlg = new Window("dialog", "Auto BlurZoom & Group Pipeline");
    dlg.orientation = "column";
    dlg.alignChildren = ["fill", "top"];
    dlg.spacing = 10;
    dlg.margins = 16;

    var tipPanel = dlg.add("panel", undefined, "Performance-Tipp");
    tipPanel.alignChildren = ["left", "top"];
    tipPanel.margins = 10;
    var tipText = tipPanel.add("statictext", undefined, "Aktiviere vor dem Start CAPS LOCK (Feststelltaste) für maximale Geschwindigkeit!", { multiline: true });
    tipText.preferredSize.width = 360;

    var scopePanel = dlg.add("panel", undefined, "Ziel-Clips");
    scopePanel.alignChildren = ["left", "top"];
    scopePanel.margins = 12;
    var rbAll = scopePanel.add("radiobutton", undefined, "Alle Clip-Blöcke automatisch gruppieren (mit Resume-Schutz)");
    var rbSel = scopePanel.add("radiobutton", undefined, "Nur aktuell markierte Basis-Clips (inkl. ihrer Blurs) gruppieren");
    rbAll.value = true;

    var zoomPanel = dlg.add("panel", undefined, "BlurZoom Einstellungen");
    zoomPanel.alignChildren = ["fill", "top"];
    zoomPanel.margins = 12;

    var startRow = zoomPanel.add("group");
    startRow.add("statictext", undefined, "Start-Preset für Clip 1:");
    var ddStartZoom = startRow.add("dropdownlist", undefined, ["BlurZoomIn (In -> Out -> In ...)", "BlurZoomOut (Out -> In -> Out ...)"]);
    ddStartZoom.selection = 0;

    var cbAddTile = zoomPanel.add("checkbox", undefined, "Zusätzlich Motion Tile (370/370) auf BlurZoom-Adjustment-Layer legen");
    cbAddTile.value = false;

    var optPanel = dlg.add("panel", undefined, "Stabilität");
    optPanel.alignChildren = ["fill", "top"];
    optPanel.margins = 12;

    var batchRow = optPanel.add("group");
    batchRow.add("statictext", undefined, "Auto-Save & Cache leeren alle X Clips:");
    var batchInput = batchRow.add("edittext", undefined, "10");
    batchInput.characters = 4;

    var cbAutoSave = optPanel.add("checkbox", undefined, "Projekt nach jedem Batch automatisch speichern");
    cbAutoSave.value = true;

    var btnGroup = dlg.add("group");
    btnGroup.alignment = ["right", "top"];
    btnGroup.add("button", undefined, "Abbrechen", { name: "cancel" });
    btnGroup.add("button", undefined, "Starten", { name: "ok" });

    if (dlg.show() !== 1) {
        return;
    }

    var useSelectedOnly = rbSel.value;
    var startWithIn     = (ddStartZoom.selection.index === 0);
    var addTileToZoom   = cbAddTile.value;
    var autoSave        = cbAutoSave.value;
    var batchSize       = parseInt(batchInput.text, 10);
    if (isNaN(batchSize) || batchSize < 1) {
        batchSize = 10;
    }

    if (autoSave && app.project.file === null) {
        var proceed = confirm("Das Projekt wurde noch nie gespeichert – Auto-Save kann deshalb nicht automatisch sichern.\nTrotzdem fortfahren?");
        if (!proceed) return;
        autoSave = false;
    }

    // ================= HILFSFUNKTIONEN =================

    function trimStr(str) {
        return str.replace(/^\s+|\s+$/g, "");
    }

    function isProtectedGlobalLayer(layer, totalLayers) {
        if (layer.index === 1 || layer.index === totalLayers) return true;
        if (layer instanceof TextLayer) return true;
        if (layer instanceof AVLayer && !layer.hasVideo) return true;

        var lowerName = trimStr(layer.name).toLowerCase();
        for (var i = 0; i < EXCLUDED_NAMES.length; i++) {
            if (lowerName === EXCLUDED_NAMES[i] || lowerName.indexOf("eternals") !== -1) {
                return true;
            }
        }
        return false;
    }

    function isAlreadyZoomGrouped(layer) {
        if (layer.comment && layer.comment.indexOf("[DONE_ZOOM_GROUP]") !== -1) {
            return true;
        }
        if (layer.name.indexOf("ZoomGroup_") === 0) {
            return true;
        }
        return false;
    }

    function extractClipTag(commentStr) {
        if (!commentStr) return null;
        var match = commentStr.match(/\[EDIT_CLIP:\d+\]/);
        return match ? match[0] : null;
    }

    function findUserPresetsFolder() {
        var adobeDocs = new Folder(Folder.myDocuments.fsName + "/Adobe");
        if (adobeDocs.exists) {
            var preferred = new Folder(adobeDocs.fsName + "/After Effects 2026/User Presets");
            if (preferred.exists) return preferred;

            var items = adobeDocs.getFiles();
            for (var i = 0; i < items.length; i++) {
                if (items[i] instanceof Folder && items[i].name.indexOf("After Effects") === 0) {
                    var up = new Folder(items[i].fsName + "/User Presets");
                    if (up.exists) return up;
                }
            }
        }
        return Folder.selectDialog("Bitte wähle deinen After Effects 'User Presets'-Ordner aus:");
    }

    function findPresetRecursive(folder, baseName) {
        var target = (baseName + ".ffx").toLowerCase();
        var files = folder.getFiles();
        for (var i = 0; i < files.length; i++) {
            var item = files[i];
            if (item instanceof File) {
                if (decodeURI(item.name).toLowerCase() === target) return item;
            } else if (item instanceof Folder) {
                var found = findPresetRecursive(item, baseName);
                if (found !== null) return found;
            }
        }
        return null;
    }

    function deselectAllLayers(targetComp) {
        var sel = targetComp.selectedLayers;
        for (var i = 0; i < sel.length; i++) {
            sel[i].selected = false;
        }
    }

    function applyPresetAtTime(targetComp, layer, presetFile, atTime) {
        deselectAllLayers(targetComp);
        layer.selected = true;
        targetComp.time = atTime;
        layer.applyPreset(presetFile);
    }

    function addMotionTile(layer) {
        var fx = layer.property("ADBE Effect Parade");
        var tile = fx.addProperty("ADBE Tile");
        tile.property("ADBE Tile-0004").setValue(MOTION_TILE_WIDTH);
        tile.property("ADBE Tile-0005").setValue(MOTION_TILE_HEIGHT);
        tile.property("ADBE Tile-0006").setValue(1);
        return tile;
    }

    function collectAnimatedProperties(propGroup, list) {
        for (var i = 1; i <= propGroup.numProperties; i++) {
            var p = propGroup.property(i);
            if (p.propertyType === PropertyType.PROPERTY) {
                if (p.canVaryOverTime && p.numKeys >= 2) {
                    list.push(p);
                }
            } else if (p.propertyType === PropertyType.INDEXED_GROUP || p.propertyType === PropertyType.NAMED_GROUP) {
                collectAnimatedProperties(p, list);
            }
        }
    }

    function stretchKeyframesToSpan(props, targetIn, targetOut) {
        if (props.length === 0 || targetOut <= targetIn) return;

        var minT = 9999999;
        var maxT = -9999999;

        for (var pIdx = 0; pIdx < props.length; pIdx++) {
            var pr = props[pIdx];
            var tFirst = pr.keyTime(1);
            var tLast  = pr.keyTime(pr.numKeys);
            if (tFirst < minT) minT = tFirst;
            if (tLast > maxT) maxT = tLast;
        }

        var oldSpan = maxT - minT;
        var newSpan = targetOut - targetIn;
        if (oldSpan <= 0.0001) return;

        for (var i = 0; i < props.length; i++) {
            var prop = props[i];
            var isSpatial = (
                prop.propertyValueType === PropertyValueType.TwoD_SPATIAL ||
                prop.propertyValueType === PropertyValueType.ThreeD_SPATIAL
            );

            var keysData = [];
            for (var k = 1; k <= prop.numKeys; k++) {
                var oldT = prop.keyTime(k);
                var norm = (oldT - minT) / oldSpan;
                var newT = targetIn + (norm * newSpan);

                var kd = {
                    time:      newT,
                    value:     prop.keyValue(k),
                    inType:    prop.keyInInterpolationType(k),
                    outType:   prop.keyOutInterpolationType(k),
                    inEase:    prop.keyInTemporalEase(k),
                    outEase:   prop.keyOutTemporalEase(k),
                    tAuto:     prop.keyTemporalAutoBezier(k),
                    tCont:     prop.keyTemporalContinuous(k),
                    isSpatial: isSpatial
                };

                if (isSpatial) {
                    kd.inTan  = prop.keyInSpatialTangent(k);
                    kd.outTan = prop.keyOutSpatialTangent(k);
                    kd.sAuto  = prop.keySpatialAutoBezier(k);
                    kd.sCont  = prop.keySpatialContinuous(k);
                    kd.roving = prop.keyRoving(k);
                }
                keysData.push(kd);
            }

            for (var r = prop.numKeys; r >= 1; r--) {
                prop.removeKey(r);
            }

            for (var n = 0; n < keysData.length; n++) {
                var d = keysData[n];
                var newIdx = prop.addKey(d.time);
                prop.setValueAtKey(newIdx, d.value);
                prop.setInterpolationTypeAtKey(newIdx, d.inType, d.outType);
                try {
                    prop.setTemporalEaseAtKey(newIdx, d.inEase, d.outEase);
                    prop.setTemporalContinuousAtKey(newIdx, d.tCont);
                    prop.setTemporalAutoBezierAtKey(newIdx, d.tAuto);
                } catch (e) {}

                if (d.isSpatial) {
                    try {
                        prop.setSpatialTangentsAtKey(newIdx, d.inTan, d.outTan);
                        prop.setSpatialContinuousAtKey(newIdx, d.sCont);
                        prop.setSpatialAutoBezierAtKey(newIdx, d.sAuto);
                        if (n > 0 && n < keysData.length - 1) {
                            prop.setRovingAtKey(newIdx, d.roving);
                        }
                    } catch (e) {}
                }
            }
        }
    }

    // Findet alle Adjustment Layers (StartBlur, EndBlur200, EndBlur80 etc.), die zu diesem Basis-Clip gehören
    function findAssociatedAdjustmentLayers(targetComp, baseClip) {
        var found = [];
        var total = targetComp.numLayers;
        var clipTag = extractClipTag(baseClip.comment);
        var eps = 0.002; // kleine Toleranz für Fließkomma-Vergleiche

        for (var i = 1; i <= total; i++) {
            var cand = targetComp.layer(i);
            if (!cand.adjustmentLayer) continue;
            if (isProtectedGlobalLayer(cand, total)) continue;
            if (cand.comment && cand.comment.indexOf("[DONE_ZOOM_ADJ]") !== -1) continue;
            if (cand.name.indexOf("BlurZoom_") === 0) continue;

            if (clipTag !== null) {
                // Exakter Match über Tag aus v2
                if (cand.comment && cand.comment.indexOf(clipTag) !== -1) {
                    found.push(cand);
                }
            } else {
                // Fallback (v1 / manuell): Adjustment Layer liegt zeitlich komplett innerhalb des Basis-Clips
                if (cand.inPoint >= (baseClip.inPoint - eps) && cand.outPoint <= (baseClip.outPoint + eps)) {
                    found.push(cand);
                }
            }
        }
        return found;
    }

    // ================= PRESETS SUCHEN =================
    var presetsRoot = findUserPresetsFolder();
    if (!presetsRoot || !presetsRoot.exists) {
        alert("Abbruch: Kein gültiger Presets-Ordner gefunden.");
        return;
    }

    var presetIn  = findPresetRecursive(presetsRoot, PRESET_NAMES.blurZoomIn);
    var presetOut = findPresetRecursive(presetsRoot, PRESET_NAMES.blurZoomOut);

    var missing = [];
    if (!presetIn)  missing.push(PRESET_NAMES.blurZoomIn + ".ffx");
    if (!presetOut) missing.push(PRESET_NAMES.blurZoomOut + ".ffx");

    if (missing.length > 0) {
        alert("Folgende Presets wurden im User-Presets-Ordner nicht gefunden:\n- " + missing.join("\n- "));
        return;
    }

    // ================= BASIS-CLIPS SAMMELN =================
    var baseClips = [];
    var totalLayers = comp.numLayers;

    if (useSelectedOnly) {
        for (var s = 0; s < comp.selectedLayers.length; s++) {
            var sl = comp.selectedLayers[s];
            if (!sl.adjustmentLayer && !isProtectedGlobalLayer(sl, totalLayers)) {
                baseClips.push(sl);
            }
        }
    } else {
        for (var l = 1; l <= totalLayers; l++) {
            var lyr = comp.layer(l);
            if (!lyr.adjustmentLayer && !isProtectedGlobalLayer(lyr, totalLayers)) {
                baseClips.push(lyr);
            }
        }
    }

    if (baseClips.length === 0) {
        alert("Keine Basis-Clips zum Gruppieren gefunden.");
        return;
    }

    // Chronologisch sortieren (hält In/Out-Reihenfolge auch bei Resume konsistent)
    baseClips.sort(function (a, b) {
        if (a.inPoint !== b.inPoint) return a.inPoint - b.inPoint;
        return b.index - a.index;
    });

    // ================= AUSFÜHRUNG IN BATCHES =================
    comp.openInViewer();
    var frameDur = comp.frameDuration;

    var progressWin = new Window("palette", "Auto BlurZoom Pipeline läuft...", undefined, { closeButton: false });
    progressWin.alignChildren = ["fill", "top"];
    var statusTxt = progressWin.add("statictext", undefined, "Starte...");
    statusTxt.preferredSize.width = 360;
    var pBar = progressWin.add("progressbar", undefined, 0, baseClips.length);
    pBar.preferredSize.width = 360;
    progressWin.show();

    var processedCount = 0;
    var skippedCount   = 0;
    var inBatchCount   = 0;
    var undoOpen       = false;

    try {
        for (var idx = 0; idx < baseClips.length; idx++) {
            var baseClip = baseClips[idx];

            if (isAlreadyZoomGrouped(baseClip)) {
                skippedCount++;
                pBar.value = idx + 1;
                statusTxt.text = "Überspringe bereits gruppierten Clip " + (idx + 1) + " / " + baseClips.length;
                progressWin.update();
                continue;
            }

            if (!undoOpen) {
                app.beginUndoGroup("Auto BlurZoom Pipeline Batch");
                undoOpen = true;
            }

            var clipNum  = idx + 1;
            var clipIn   = baseClip.inPoint;
            var clipOut  = baseClip.outPoint;
            var clipSpan = Math.max(frameDur, clipOut - clipIn);
            var isZoomIn = startWithIn ? (idx % 2 === 0) : (idx % 2 === 1);
            var zoomTag  = isZoomIn ? "In" : "Out";

            statusTxt.text = "Gruppiere Clip " + clipNum + " / " + baseClips.length + " (BlurZoom" + zoomTag + ")";
            pBar.value = clipNum;
            progressWin.update();

            // 1. ZUGEHÖRIGE BLUR-ADJUSTMENT-LAYERS FINDEN & INDIZES SAMMELN
            var adjLayers = findAssociatedAdjustmentLayers(comp, baseClip);
            var groupIndices = [baseClip.index];
            for (var a = 0; a < adjLayers.length; a++) {
                groupIndices.push(adjLayers[a].index);
            }
            groupIndices.sort(function (x, y) { return x - y; });

            var topIndex = groupIndices[0];
            var groupName = "ZoomGroup_" + clipNum + "_" + zoomTag;

            // 2. PRE-COMPOSE (Move all attributes + exakt auf Clip-Länge zuschneiden)
            var groupCompItem = comp.layers.precompose(groupIndices, groupName, true);

            for (var m = 1; m <= groupCompItem.numLayers; m++) {
                var inner = groupCompItem.layer(m);
                inner.startTime = inner.startTime - clipIn;
            }
            groupCompItem.duration = clipSpan;

            var groupLayer = comp.layer(topIndex);
            groupLayer.startTime = clipIn;
            groupLayer.inPoint   = clipIn;
            groupLayer.outPoint  = clipOut;
            groupLayer.comment   = "[DONE_ZOOM_GROUP]";

            // 3. ADJUSTMENT LAYER ÜBER DIE GESAMTE CLIP-LÄNGE ERSTELLEN
            var zoomAdj = comp.layers.addSolid(
                [1, 1, 1],
                "BlurZoom_" + zoomTag + "_" + clipNum,
                comp.width,
                comp.height,
                comp.pixelAspect,
                comp.duration
            );
            zoomAdj.adjustmentLayer = true;
            zoomAdj.inPoint  = clipIn;
            zoomAdj.outPoint = clipOut;
            zoomAdj.comment  = "[DONE_ZOOM_ADJ]";
            zoomAdj.moveBefore(groupLayer);

            if (addTileToZoom) {
                addMotionTile(zoomAdj);
            }

            // 4. BLURZOOM-PRESET ANWENDEN & KEYFRAMES AUF GESAMTE LÄNGE STRECKEN
            var activePreset = isZoomIn ? presetIn : presetOut;
            applyPresetAtTime(comp, zoomAdj, activePreset, clipIn);

            var animatedProps = [];
            collectAnimatedProperties(zoomAdj, animatedProps);
            stretchKeyframesToSpan(animatedProps, clipIn, clipOut);

            processedCount++;
            inBatchCount++;

            // Batch-Abschluss: Undo schließen, Cache leeren, Projekt speichern
            if (inBatchCount >= batchSize) {
                app.endUndoGroup();
                undoOpen = false;
                inBatchCount = 0;

                statusTxt.text = "Leere Cache & speichere Projekt (Clip " + clipNum + " / " + baseClips.length + ")...";
                progressWin.update();

                try { app.purge(PurgeTarget.ALL_CACHES); } catch (e) {}
                if (autoSave && app.project.file !== null) {
                    app.project.save();
                }
            }
        }

        if (undoOpen) {
            app.endUndoGroup();
            undoOpen = false;
        }

        deselectAllLayers(comp);
        try { app.purge(PurgeTarget.ALL_CACHES); } catch (e) {}
        if (autoSave && app.project.file !== null && processedCount > 0) {
            app.project.save();
        }

        progressWin.close();
        alert("Fertig!\nGruppiert & BlurZoom angewendet: " + processedCount + " Clips\nÜbersprungen (bereits fertig): " + skippedCount + " Clips");

    } catch (err) {
        if (undoOpen) {
            try { app.endUndoGroup(); } catch (e) {}
        }
        alert("Fehler bei Clip " + (idx + 1) + ":\n" + err.toString() + " (Zeile " + err.line + ")\n\nAlle abgeschlossenen Batches davor wurden gespeichert.");
    } finally {
        if (progressWin) {
            try { progressWin.close(); } catch (e) {}
        }
    }
})();