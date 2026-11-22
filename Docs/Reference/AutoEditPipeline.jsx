/*
 * AutoEditPipeline_v2.jsx
 * Optimierte Version mit Batch-Undo, Auto-Save, RAM-Purge, Resume-Schutz
 * und automatischem Clip-Tagging für das spätere Gruppieren.
 * Kompatibel mit After Effects 2026.
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
        fadeInOut:       "FadeInOut",
        verticalShake:   "VerticalShake",
        verticalBlur:    "VerticalBlur",
        horizontalShake: "HorizontalShake",
        horizontalBlur:  "HorizontalBlur"
    };

    var MOTION_TILE_WIDTH   = 370;
    var MOTION_TILE_HEIGHT  = 370;
    var START_BLUR_FRAMES   = 10;
    var END_BLUR_1_LENGTH   = 200; // Vorletzter Frame
    var END_BLUR_2_LENGTH   = 80;  // Letzter Frame
    var VERTICAL_BLUR_DIR   = 90;  // 0x+90°
    var HORIZONTAL_BLUR_DIR = 180; // 0x+180°
    // =================================================

    var comp = app.project.activeItem;
    if (!comp || !(comp instanceof CompItem)) {
        alert("Bitte öffne zuerst deine Edit-Komposition.");
        return;
    }

    // ================= EINSTELLUNGS-DIALOG =================
    var dlg = new Window("dialog", "Auto Edit Pipeline v2 (Optimiert)");
    dlg.orientation = "column";
    dlg.alignChildren = ["fill", "top"];
    dlg.spacing = 10;
    dlg.margins = 16;

    var tipPanel = dlg.add("panel", undefined, "Performance-Tipp");
    tipPanel.alignChildren = ["left", "top"];
    tipPanel.margins = 10;
    var tipText = tipPanel.add("statictext", undefined, "Aktiviere vor dem Start CAPS LOCK (Feststelltaste), damit AE die Vorschau pausiert!", { multiline: true });
    tipText.preferredSize.width = 360;

    var scopePanel = dlg.add("panel", undefined, "Ziel-Layer");
    scopePanel.alignChildren = ["left", "top"];
    scopePanel.margins = 12;
    var rbAll = scopePanel.add("radiobutton", undefined, "Alle gültigen Clips automatisch (mit Resume-Schutz)");
    var rbSel = scopePanel.add("radiobutton", undefined, "Nur aktuell markierte Clips verarbeiten");
    rbAll.value = true;

    var stepsPanel = dlg.add("panel", undefined, "Schritte");
    stepsPanel.alignChildren = ["left", "top"];
    stepsPanel.margins = 12;
    var cbPrecomp    = stepsPanel.add("checkbox", undefined, "1. Einzeln Pre-composen (Move all attributes + Adjust duration)");
    var cbFadeInOut  = stepsPanel.add("checkbox", undefined, "2. FadeInOut anwenden & Keyframes auf Clip-Länge strecken");
    var cbShakeBlur  = stepsPanel.add("checkbox", undefined, "3. Motion Tile (370/370) + abwechselnd Vertical/Horizontal Shake & 10f Blur");
    var cbEndBlurs   = stepsPanel.add("checkbox", undefined, "4. Zwei 1-Frame End-Blurs (Directional Blur 200 -> 80)");
    cbPrecomp.value   = true;
    cbFadeInOut.value = true;
    cbShakeBlur.value = true;
    cbEndBlurs.value  = true;

    var optPanel = dlg.add("panel", undefined, "Stabilität & Richtung");
    optPanel.alignChildren = ["fill", "top"];
    optPanel.margins = 12;

    var dirRow = optPanel.add("group");
    dirRow.add("statictext", undefined, "Start-Richtung für Clip 1:");
    var ddStartDir = dirRow.add("dropdownlist", undefined, ["Vertical", "Horizontal"]);
    ddStartDir.selection = 0;

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
    var doPrecomp       = cbPrecomp.value;
    var doFadeInOut     = cbFadeInOut.value;
    var doShakeBlur     = cbShakeBlur.value;
    var doEndBlurs      = cbEndBlurs.value;
    var startVertical   = (ddStartDir.selection.index === 0);
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

    function isExcludedLayer(layer, totalLayers) {
        if (layer.index === 1 || layer.index === totalLayers) return true;
        if (layer instanceof TextLayer) return true;
        if (layer.adjustmentLayer) return true;
        if (layer instanceof AVLayer && !layer.hasVideo) return true;

        var lowerName = trimStr(layer.name).toLowerCase();
        for (var i = 0; i < EXCLUDED_NAMES.length; i++) {
            if (lowerName === EXCLUDED_NAMES[i] || lowerName.indexOf("eternals") !== -1) {
                return true;
            }
        }
        return false;
    }

    function isAlreadyProcessed(layer) {
        if (layer.comment && layer.comment.indexOf("[DONE_PIPELINE_1]") !== -1) {
            return true;
        }
        // Fallback für Clips aus v1
        if (layer.name.indexOf("Pre-comp ") === 0 || layer.name.indexOf("ZoomGroup_") === 0) {
            return true;
        }
        return false;
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

    function addDirectionalBlur(layer, directionDeg, blurLength) {
        var fx = layer.property("ADBE Effect Parade");
        var dBlur = fx.addProperty("ADBE Motion Blur");
        dBlur.property("ADBE Motion Blur-0001").setValue(directionDeg);
        dBlur.property("ADBE Motion Blur-0002").setValue(blurLength);
        return dBlur;
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

    function createTrimmedAdjustmentLayer(targetComp, name, inT, outT, aboveLayer, commentTag) {
        var adj = targetComp.layers.addSolid(
            [1, 1, 1],
            name,
            targetComp.width,
            targetComp.height,
            targetComp.pixelAspect,
            targetComp.duration
        );
        adj.adjustmentLayer = true;
        adj.inPoint  = inT;
        adj.outPoint = outT;
        adj.comment  = commentTag;
        adj.moveBefore(aboveLayer);
        return adj;
    }

    // ================= PRESETS SUCHEN =================
    var presets = {};
    if (doFadeInOut || doShakeBlur) {
        var presetsRoot = findUserPresetsFolder();
        if (!presetsRoot || !presetsRoot.exists) {
            alert("Abbruch: Kein gültiger Presets-Ordner gefunden.");
            return;
        }

        var missing = [];
        if (doFadeInOut) {
            presets.fadeInOut = findPresetRecursive(presetsRoot, PRESET_NAMES.fadeInOut);
            if (!presets.fadeInOut) missing.push(PRESET_NAMES.fadeInOut + ".ffx");
        }
        if (doShakeBlur) {
            presets.verticalShake   = findPresetRecursive(presetsRoot, PRESET_NAMES.verticalShake);
            presets.verticalBlur    = findPresetRecursive(presetsRoot, PRESET_NAMES.verticalBlur);
            presets.horizontalShake = findPresetRecursive(presetsRoot, PRESET_NAMES.horizontalShake);
            presets.horizontalBlur  = findPresetRecursive(presetsRoot, PRESET_NAMES.horizontalBlur);

            if (!presets.verticalShake)   missing.push(PRESET_NAMES.verticalShake + ".ffx");
            if (!presets.verticalBlur)    missing.push(PRESET_NAMES.verticalBlur + ".ffx");
            if (!presets.horizontalShake) missing.push(PRESET_NAMES.horizontalShake + ".ffx");
            if (!presets.horizontalBlur)  missing.push(PRESET_NAMES.horizontalBlur + ".ffx");
        }

        if (missing.length > 0) {
            alert("Folgende Presets wurden nicht gefunden:\n- " + missing.join("\n- "));
            return;
        }
    }

    // ================= ZIEL-LAYER SAMMELN =================
    var targetLayers = [];
    var totalLayers = comp.numLayers;

    if (useSelectedOnly) {
        for (var s = 0; s < comp.selectedLayers.length; s++) {
            var sl = comp.selectedLayers[s];
            if (!isExcludedLayer(sl, totalLayers)) {
                targetLayers.push(sl);
            }
        }
    } else {
        for (var l = 1; l <= totalLayers; l++) {
            var lyr = comp.layer(l);
            if (!isExcludedLayer(lyr, totalLayers)) {
                targetLayers.push(lyr);
            }
        }
    }

    if (targetLayers.length === 0) {
        alert("Keine gültigen Clip-Layer gefunden.");
        return;
    }

    // Chronologisch sortieren (hält V/H-Reihenfolge auch bei Resume konsistent)
    targetLayers.sort(function (a, b) {
        if (a.inPoint !== b.inPoint) return a.inPoint - b.inPoint;
        return b.index - a.index;
    });

    // ================= AUSFÜHRUNG IN BATCHES =================
    comp.openInViewer();
    var frameDur = comp.frameDuration;

    var progressWin = new Window("palette", "Auto Edit Pipeline v2 läuft...", undefined, { closeButton: false });
    progressWin.alignChildren = ["fill", "top"];
    var statusTxt = progressWin.add("statictext", undefined, "Starte...");
    statusTxt.preferredSize.width = 360;
    var pBar = progressWin.add("progressbar", undefined, 0, targetLayers.length);
    pBar.preferredSize.width = 360;
    progressWin.show();

    var processedCount = 0;
    var skippedCount   = 0;
    var inBatchCount   = 0;
    var undoOpen       = false;

    try {
        for (var idx = 0; idx < targetLayers.length; idx++) {
            var clipLayer = targetLayers[idx];

            // Resume-Schutz: bereits fertige Clips überspringen
            if (isAlreadyProcessed(clipLayer)) {
                skippedCount++;
                pBar.value = idx + 1;
                statusTxt.text = "Überspringe bereits fertigen Clip " + (idx + 1) + " / " + targetLayers.length;
                progressWin.update();
                continue;
            }

            if (!undoOpen) {
                app.beginUndoGroup("Auto Edit Pipeline Batch");
                undoOpen = true;
            }

            var clipNum  = idx + 1;
            var clipTag  = "[EDIT_CLIP:" + clipNum + "]";
            var clipIn   = clipLayer.inPoint;
            var clipOut  = clipLayer.outPoint;
            var clipSpan = Math.max(frameDur, clipOut - clipIn);
            var isVert   = startVertical ? (idx % 2 === 0) : (idx % 2 === 1);
            var dirTag   = isVert ? "V" : "H";

            statusTxt.text = "Clip " + clipNum + " / " + targetLayers.length + " (" + dirTag + "): " + clipLayer.name;
            pBar.value = clipNum;
            progressWin.update();

            // 1. PRE-COMPOSE
            if (doPrecomp) {
                var origIdx = clipLayer.index;
                var precompName = "Pre-comp " + clipNum + " - " + clipLayer.name;
                var precompItem = comp.layers.precompose([origIdx], precompName, true);

                var innerLayer = precompItem.layer(1);
                innerLayer.startTime = innerLayer.startTime - clipIn;
                precompItem.duration = clipSpan;

                clipLayer = comp.layer(origIdx);
                clipLayer.startTime = clipIn;
                clipLayer.inPoint   = clipIn;
                clipLayer.outPoint  = clipOut;
            }

            clipLayer.comment = "[DONE_PIPELINE_1] " + clipTag;

            // 2. MOTION TILE AUF PRECOMP
            if (doShakeBlur) {
                addMotionTile(clipLayer);
            }

            // 3. FADE IN / OUT + KEYFRAMES STRECKEN
            if (doFadeInOut) {
                applyPresetAtTime(comp, clipLayer, presets.fadeInOut, clipIn);
                var animatedProps = [];
                collectAnimatedProperties(clipLayer, animatedProps);
                stretchKeyframesToSpan(animatedProps, clipIn, clipOut);
            }

            // 4. SHAKE + 10-FRAME START-BLUR
            var topMostForClip = clipLayer;

            if (doShakeBlur) {
                var shakePreset = isVert ? presets.verticalShake : presets.horizontalShake;
                var blurPreset  = isVert ? presets.verticalBlur  : presets.horizontalBlur;

                applyPresetAtTime(comp, clipLayer, shakePreset, clipIn);

                var blurOut = Math.min(clipOut, clipIn + (START_BLUR_FRAMES * frameDur));
                var startBlurAdj = createTrimmedAdjustmentLayer(
                    comp,
                    "StartBlur_" + dirTag + "_" + clipNum,
                    clipIn,
                    blurOut,
                    topMostForClip,
                    clipTag
                );
                addMotionTile(startBlurAdj);
                applyPresetAtTime(comp, startBlurAdj, blurPreset, clipIn);
                topMostForClip = startBlurAdj;
            }

            // 5. ZWEI 1-FRAME END-BLURS (200 -> 80)
            if (doEndBlurs && clipSpan >= (2 * frameDur - 0.0001)) {
                var blurDir = isVert ? VERTICAL_BLUR_DIR : HORIZONTAL_BLUR_DIR;

                var endBlur1 = createTrimmedAdjustmentLayer(
                    comp,
                    "EndBlur200_" + dirTag + "_" + clipNum,
                    clipOut - (2 * frameDur),
                    clipOut - (1 * frameDur),
                    topMostForClip,
                    clipTag
                );
                addMotionTile(endBlur1);
                addDirectionalBlur(endBlur1, blurDir, END_BLUR_1_LENGTH);
                topMostForClip = endBlur1;

                var endBlur2 = createTrimmedAdjustmentLayer(
                    comp,
                    "EndBlur80_" + dirTag + "_" + clipNum,
                    clipOut - (1 * frameDur),
                    clipOut,
                    topMostForClip,
                    clipTag
                );
                addMotionTile(endBlur2);
                addDirectionalBlur(endBlur2, blurDir, END_BLUR_2_LENGTH);
            }

            processedCount++;
            inBatchCount++;

            // Batch-Abschluss: Undo schließen, Cache leeren, Projekt speichern
            if (inBatchCount >= batchSize) {
                app.endUndoGroup();
                undoOpen = false;
                inBatchCount = 0;

                statusTxt.text = "Leere Cache & speichere Projekt (Clip " + clipNum + " / " + targetLayers.length + ")...";
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
        alert("Fertig!\nVerarbeitet: " + processedCount + " Clips\nÜbersprungen (bereits fertig): " + skippedCount + " Clips");

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