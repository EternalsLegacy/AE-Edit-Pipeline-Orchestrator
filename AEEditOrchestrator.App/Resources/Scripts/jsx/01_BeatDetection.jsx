/*
 * 01_BeatDetection.jsx
 * Analyzes a selected video layer frame-by-frame via sampleImage() expression.
 * Default mode is UniversalFourZone (detects cuts and overlay flashes of all colors
 * via 4-zone RGB + luminance frame-difference and local spike detection).
 * Legacy RedOverlay mode remains optionally selectable.
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

    var rawMode = bd.mode ? trimStr(bd.mode) : "UniversalFourZone";
    var isRedOverlay = (rawMode.toLowerCase() === "redoverlay");
    var modeLabel = isRedOverlay ? "RedOverlay" : "UniversalFourZone";

    var sensitivity = (typeof bd.sensitivity === "number" && !isNaN(bd.sensitivity)) ? bd.sensitivity : 65.0;
    if (sensitivity < 10.0) {
        sensitivity = 10.0;
    }
    if (sensitivity > 100.0) {
        sensitivity = 100.0;
    }

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

    writeHeartbeat(jobId, 0, totalFrames, "Starte Beat-Erkennung (" + modeLabel + ")...");

    var sliderEffect = null;
    var beatCount = 0;
    var aborted = false;

    safeBeginUndoGroup("AE Orchestrator: Beat Detection");
    try {
        var effects = layer.property("ADBE Effect Parade");
        sliderEffect = effects.addProperty("ADBE Slider Control");
        sliderEffect.name = "__TempBeatSampler";

        var sliderProp = sliderEffect.property("ADBE Slider Control-0001");
        if (isRedOverlay) {
            sliderProp.expression =
                "var c = sampleImage([width * " + sampleXRatio + ", height * " + sampleYRatio + "], [" + sampleRadius + ", " + sampleRadius + "], false, time);\n" +
                "(c[0] - (c[1] + c[2]) * 0.5) * 100;";
        } else {
            sliderProp.expression =
                "var dt = thisComp.frameDuration;\n" +
                "var r = [" + sampleRadius + ", " + sampleRadius + "];\n" +
                "var p1 = [width * 0.25, height * 0.25];\n" +
                "var p2 = [width * 0.75, height * 0.25];\n" +
                "var p3 = [width * 0.25, height * 0.75];\n" +
                "var p4 = [width * 0.75, height * 0.75];\n" +
                "var c1a = sampleImage(p1, r, false, time); var c1b = sampleImage(p1, r, false, time - dt);\n" +
                "var c2a = sampleImage(p2, r, false, time); var c2b = sampleImage(p2, r, false, time - dt);\n" +
                "var c3a = sampleImage(p3, r, false, time); var c3b = sampleImage(p3, r, false, time - dt);\n" +
                "var c4a = sampleImage(p4, r, false, time); var c4b = sampleImage(p4, r, false, time - dt);\n" +
                "var l1 = Math.abs((0.2126 * c1a[0] + 0.7152 * c1a[1] + 0.0722 * c1a[2]) - (0.2126 * c1b[0] + 0.7152 * c1b[1] + 0.0722 * c1b[2]));\n" +
                "var l2 = Math.abs((0.2126 * c2a[0] + 0.7152 * c2a[1] + 0.0722 * c2a[2]) - (0.2126 * c2b[0] + 0.7152 * c2b[1] + 0.0722 * c2b[2]));\n" +
                "var l3 = Math.abs((0.2126 * c3a[0] + 0.7152 * c3a[1] + 0.0722 * c3a[2]) - (0.2126 * c3b[0] + 0.7152 * c3b[1] + 0.0722 * c3b[2]));\n" +
                "var l4 = Math.abs((0.2126 * c4a[0] + 0.7152 * c4a[1] + 0.0722 * c4a[2]) - (0.2126 * c4b[0] + 0.7152 * c4b[1] + 0.0722 * c4b[2]));\n" +
                "var d1 = length(c1a - c1b) + l1;\n" +
                "var d2 = length(c2a - c2b) + l2;\n" +
                "var d3 = length(c3a - c3b) + l3;\n" +
                "var d4 = length(c4a - c4b) + l4;\n" +
                "(d1 + d2 + d3 + d4) * 12.5;";
        }

        var values = [];
        var times = [];
        var minVal = 999999;
        var maxVal = -999999;

        for (var i = 0; i <= totalFrames; i++) {
            if (i % 5 === 0 && isAbortRequested()) {
                aborted = true;
                break;
            }

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
                writeHeartbeat(jobId, i, totalFrames, "Analysiere Frames (" + modeLabel + "): " + i + " / " + totalFrames);
            }
        }

        if (!aborted && isAbortRequested()) {
            aborted = true;
        }

        if (!aborted) {
            var markerTarget = useCompMarkers ? comp.markerProperty : layer.property("ADBE Marker");
            var lastMarkerFrame = -minFrameGap - 1;

            if (isRedOverlay) {
                var minSpread = 2.0 + ((1.0 - (sensitivity / 100.0)) * 10.0);
                var spread = maxVal - minVal;
                if (spread < minSpread) {
                    return buildErrorResult(
                        jobId,
                        null,
                        "Es konnte kein eindeutiges rotes Overlay erkannt werden (Kontrast zu gering: " +
                        spread.toFixed(2) + " < " + minSpread.toFixed(2) + ")."
                    );
                }

                var redThreshold = minVal + (spread * 0.5);
                var prevIsRed = values[0] > redThreshold;

                for (var k = 1; k < values.length; k++) {
                    var currentIsRed = values[k] > redThreshold;
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
            } else {
                var normSens = sensitivity / 100.0;
                var inv = 1.0 - normSens;
                var threshold = 1.5 + (inv * inv * 30.0);

                for (var j = 1; j < values.length; j++) {
                    var curr = values[j];
                    var prev = values[j - 1];
                    var isLocalPeak = (curr >= prev) && (j === values.length - 1 || curr >= values[j + 1]);

                    if (curr < threshold || !isLocalPeak) {
                        continue;
                    }

                    var winStart = Math.max(0, j - 3);
                    var winEnd = Math.min(values.length - 1, j + 2);
                    var neighborSum = 0.0;
                    var neighborCount = 0;

                    for (var w = winStart; w <= winEnd; w++) {
                        if (w !== j) {
                            neighborSum += values[w];
                            neighborCount++;
                        }
                    }

                    var localBaseline = (neighborCount > 0) ? (neighborSum / neighborCount) : 0.0;
                    var isProminentSpike = curr >= ((localBaseline * 1.8) + 0.75);

                    if (isProminentSpike && (j - lastMarkerFrame) >= minFrameGap) {
                        beatCount++;
                        var mv4 = new MarkerValue("Beat " + beatCount);
                        mv4.label = (beatCount % 2 === 1) ? 1 : 9;
                        markerTarget.setValueAtTime(times[j], mv4);
                        lastMarkerFrame = j;
                    }
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

    if (aborted) {
        return buildAbortedResult(jobId, comp, "Beat-Erkennung sauber abgebrochen.");
    }

    var msg = "Beat-Erkennung (" + modeLabel + ") abgeschlossen: " + beatCount + " Marker gesetzt.";
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
