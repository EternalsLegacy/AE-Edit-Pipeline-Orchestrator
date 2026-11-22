/*
 * 01_BeatDetection.jsx
 * Analyzes a selected video layer frame-by-frame via sampleImage() expression
 * in either RedOverlay or UniversalFourZone mode and sets AE markers.
 */

function runStep(job) {
    var jobId = (job && job.jobId) ? String(job.jobId) : "";
    var settings = (job && job.settings) ? job.settings : {};
    var bd = settings.beatDetection ? settings.beatDetection : {};

    var comp = getSafeActiveComp();
    if (!comp) {
        return buildErrorResult(jobId, null, "Bitte öffne zuerst eine Komposition.");
    }

    if (comp.selectedLayers.length !== 1) {
        return buildErrorResult(jobId, null, "Bitte markiere genau EINEN Video-Layer für die Beat-Erkennung.");
    }

    var layer = comp.selectedLayers[0];
    if (!(layer instanceof AVLayer) || !layer.hasVideo) {
        return buildErrorResult(jobId, null, "Der ausgewählte Layer ist kein Video-Layer.");
    }

    var mode = bd.mode ? trimStr(bd.mode) : "RedOverlay";
    var isFourZone = (mode.toLowerCase() === "universalfourzone");
    var sensitivity = (typeof bd.sensitivity === "number" && bd.sensitivity > 0) ? bd.sensitivity : 5.0;
    var minFrameGap = (typeof bd.minFrameGap === "number" && bd.minFrameGap >= 1) ? Math.floor(bd.minFrameGap) : 2;
    var markerTargetMode = bd.markerTarget ? trimStr(bd.markerTarget) : "Composition";
    var useCompMarkers = (markerTargetMode.toLowerCase() !== "layer");
    var onlyWorkArea = Boolean(bd.onlyWorkArea);
    var sampleXRatio = (typeof bd.sampleXRatio === "number") ? bd.sampleXRatio : 0.85;
    var sampleYRatio = (typeof bd.sampleYRatio === "number") ? bd.sampleYRatio : 0.15;
    var sampleRadius = (typeof bd.sampleRadius === "number" && bd.sampleRadius >= 1) ? bd.sampleRadius : 15;

    var startTime = Math.max(0, layer.inPoint);
    var endTime = Math.min(comp.duration, layer.outPoint);

    if (onlyWorkArea) {
        startTime = Math.max(startTime, comp.workAreaStart);
        endTime = Math.min(endTime, comp.workAreaStart + comp.workAreaDuration);
    }

    var frameDur = comp.frameDuration;
    var totalFrames = Math.floor((endTime - startTime) / frameDur);

    if (totalFrames <= 1) {
        return buildErrorResult(jobId, null, "Der ausgewählte Zeitbereich für die Beat-Erkennung ist zu kurz.");
    }

    writeHeartbeat(jobId, 0, totalFrames, "Starte Beat-Erkennung (" + mode + ")...");

    var sliderEffect = null;
    var beatCount = 0;

    safeBeginUndoGroup("AE Orchestrator: Beat Detection");
    try {
        var effects = layer.property("ADBE Effect Parade");
        sliderEffect = effects.addProperty("ADBE Slider Control");
        sliderEffect.name = "__TempBeatSampler";

        var sliderProp = sliderEffect.property("ADBE Slider Control-0001");
        if (isFourZone) {
            sliderProp.expression =
                "var dt = thisComp.frameDuration;\n" +
                "var r = [" + sampleRadius + ", " + sampleRadius + "];\n" +
                "var p1 = [width * 0.25, height * 0.25];\n" +
                "var p2 = [width * 0.75, height * 0.25];\n" +
                "var p3 = [width * 0.25, height * 0.75];\n" +
                "var p4 = [width * 0.75, height * 0.75];\n" +
                "var d1 = length(sampleImage(p1, r, false, time) - sampleImage(p1, r, false, time - dt));\n" +
                "var d2 = length(sampleImage(p2, r, false, time) - sampleImage(p2, r, false, time - dt));\n" +
                "var d3 = length(sampleImage(p3, r, false, time) - sampleImage(p3, r, false, time - dt));\n" +
                "var d4 = length(sampleImage(p4, r, false, time) - sampleImage(p4, r, false, time - dt));\n" +
                "(d1 + d2 + d3 + d4) * 25.0;";
        } else {
            sliderProp.expression =
                "var c = sampleImage([width * " + sampleXRatio + ", height * " + sampleYRatio + "], [" + sampleRadius + ", " + sampleRadius + "], false, time);\n" +
                "(c[0] - (c[1] + c[2]) * 0.5) * 100;";
        }

        var values = [];
        var times = [];
        var minVal = 999999;
        var maxVal = -999999;

        for (var i = 0; i <= totalFrames; i++) {
            var t = startTime + (i * frameDur);
            var val = sliderProp.valueAtTime(t, false);

            values.push(val);
            times.push(t);

            if (val < minVal) {
                minVal = val;
            }
            if (val > maxVal) {
                maxVal = val;
            }

            if (i % 25 === 0 || i === totalFrames) {
                writeHeartbeat(jobId, i, totalFrames, "Analysiere Frames (" + mode + "): " + i + " / " + totalFrames);
            }
        }

        var markerTarget = useCompMarkers ? comp.markerProperty : layer.property("ADBE Marker");
        var lastMarkerFrame = -minFrameGap - 1;

        if (isFourZone) {
            for (var j = 1; j < values.length; j++) {
                var curr = values[j];
                var prev = values[j - 1];
                var isBeatSpike = (curr >= sensitivity) && (prev < sensitivity || curr > (prev * 1.5));

                if (isBeatSpike && (j - lastMarkerFrame) >= minFrameGap) {
                    beatCount++;
                    var mv4 = new MarkerValue("Beat " + beatCount);
                    mv4.label = (beatCount % 2 === 1) ? 1 : 9;
                    markerTarget.setValueAtTime(times[j], mv4);
                    lastMarkerFrame = j;
                }
            }
        } else {
            var spread = maxVal - minVal;
            if (spread < sensitivity) {
                return buildErrorResult(
                    jobId,
                    null,
                    "Es konnte kein eindeutiges rotes Overlay erkannt werden (Kontrast zu gering: " +
                    spread.toFixed(2) + " < " + sensitivity + ")."
                );
            }

            var threshold = minVal + (spread * 0.5);
            var prevIsRed = values[0] > threshold;

            for (var k = 1; k < values.length; k++) {
                var currentIsRed = values[k] > threshold;
                if (currentIsRed !== prevIsRed) {
                    if ((k - lastMarkerFrame) >= minFrameGap) {
                        beatCount++;
                        var mvRed = new MarkerValue("Beat " + beatCount);
                        mvRed.label = currentIsRed ? 1 : 9;
                        markerTarget.setValueAtTime(times[k], mvRed);
                        lastMarkerFrame = k;
                    }
                    prevIsRed = currentIsRed;
                }
            }
        }
    } finally {
        if (sliderEffect !== null) {
            try {
                sliderEffect.remove();
            } catch (eRem) {}
        }
        safeEndUndoGroup();
    }

    var msg = "Beat-Erkennung (" + mode + ") abgeschlossen: " + beatCount + " Marker gesetzt.";
    writeHeartbeat(jobId, totalFrames, totalFrames, msg);

    return buildOkResult(jobId, comp, {
        totalClips: totalFrames,
        processedInBatch: beatCount,
        skippedInBatch: 0,
        hasMoreBatches: false,
        nextBatchIndex: totalFrames,
        message: msg
    });
}
