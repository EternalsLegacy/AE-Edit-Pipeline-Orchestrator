/*
 * 99_ReverseRollback.jsx
 * Granular reverse rollback script supporting Stage2Only, Stage1Only, and Full modes.
 * Removes generated adjustment layers, unpacks ZoomGroup_ / Pre-comp layers back into
 * the main composition with corrected timestamps, and deletes unused CompItems.
 */

function unpackPrecompToMain(comp, targetLayer) {
    if (!targetLayer || !(targetLayer instanceof AVLayer) || !(targetLayer.source instanceof CompItem)) {
        return false;
    }

    var innerComp = targetLayer.source;
    var groupStart = targetLayer.startTime;
    var groupIn = targetLayer.inPoint;
    var groupOut = targetLayer.outPoint;

    if (targetLayer.locked) {
        targetLayer.locked = false;
    }

    for (var m = 1; m <= innerComp.numLayers; m++) {
        var inner = innerComp.layer(m);
        var origStart = inner.startTime;
        var origIn = inner.inPoint;
        var origOut = inner.outPoint;
        var innerWasLocked = inner.locked;
        if (innerWasLocked) {
            inner.locked = false;
        }

        inner.copyToComp(comp);
        var copied = comp.layer(1);
        if (copied.locked) {
            copied.locked = false;
        }
        copied.moveBefore(targetLayer);

        var newStart = origStart + groupStart;
        var newIn = Math.max(groupIn, origIn + groupStart);
        var newOut = Math.min(groupOut, origOut + groupStart);

        copied.startTime = newStart;
        if (newOut > newIn) {
            if (newIn < copied.outPoint) {
                copied.inPoint = newIn;
                copied.outPoint = newOut;
            } else {
                copied.outPoint = newOut;
                copied.inPoint = newIn;
            }
        }

        if (innerWasLocked) {
            copied.locked = true;
            inner.locked = true;
        }
    }

    targetLayer.remove();

    if (innerComp && innerComp.usedIn.length === 0) {
        try {
            innerComp.remove();
        } catch (eRemComp) {}
    }
    return true;
}

function reverseStage2(comp, excludedNames, jobId) {
    var removedAdj = 0;
    var unpackedGroups = 0;

    for (var i = comp.numLayers; i >= 1; i--) {
        var lyr = comp.layer(i);
        var totalNow = comp.numLayers;
        if (!lyr.adjustmentLayer) {
            continue;
        }
        if (isProtectedGlobalLayer(lyr, totalNow, excludedNames)) {
            continue;
        }
        if (startsWith(lyr.name, "BlurZoom_") || hasCommentTag(lyr, "[DONE_ZOOM_ADJ]")) {
            if (lyr.locked) {
                lyr.locked = false;
            }
            lyr.remove();
            removedAdj++;
        }
    }

    var zoomGroupLayers = [];
    var totalAfterAdj = comp.numLayers;
    for (var j = 1; j <= totalAfterAdj; j++) {
        var cand = comp.layer(j);
        if (cand.adjustmentLayer || isProtectedGlobalLayer(cand, totalAfterAdj, excludedNames)) {
            continue;
        }
        if ((cand instanceof AVLayer) && (cand.source instanceof CompItem) &&
            (startsWith(cand.name, "ZoomGroup_") || hasCommentTag(cand, "[DONE_ZOOM_GROUP]"))) {
            zoomGroupLayers.push(cand);
        }
    }

    for (var g = 0; g < zoomGroupLayers.length; g++) {
        writeHeartbeat(jobId, g + 1, zoomGroupLayers.length, "Reverse Stufe 2: Entpacke " + zoomGroupLayers[g].name);
        if (unpackPrecompToMain(comp, zoomGroupLayers[g])) {
            unpackedGroups++;
        }
    }

    return {
        removedAdj: removedAdj,
        unpackedGroups: unpackedGroups
    };
}

function reverseStage1(comp, excludedNames, jobId) {
    var removedAdj = 0;
    var unpackedPrecomps = 0;

    for (var i = comp.numLayers; i >= 1; i--) {
        var lyr = comp.layer(i);
        var totalNow = comp.numLayers;
        if (!lyr.adjustmentLayer) {
            continue;
        }
        if (isProtectedGlobalLayer(lyr, totalNow, excludedNames)) {
            continue;
        }
        if (startsWith(lyr.name, "StartBlur_") ||
            startsWith(lyr.name, "EndBlur200_") ||
            startsWith(lyr.name, "EndBlur80_")) {
            if (lyr.locked) {
                lyr.locked = false;
            }
            lyr.remove();
            removedAdj++;
        }
    }

    var precompLayers = [];
    var totalAfterAdj = comp.numLayers;
    for (var j = 1; j <= totalAfterAdj; j++) {
        var cand = comp.layer(j);
        if (cand.adjustmentLayer || isProtectedGlobalLayer(cand, totalAfterAdj, excludedNames)) {
            continue;
        }
        if (startsWith(cand.name, "ZoomGroup_") || hasCommentTag(cand, "[DONE_ZOOM_GROUP]")) {
            continue;
        }
        if ((cand instanceof AVLayer) && (cand.source instanceof CompItem) &&
            (startsWith(cand.name, "Pre-comp ") || hasCommentTag(cand, "[EDIT_CLIP:") || hasCommentTag(cand, "[DONE_PIPELINE_1]"))) {
            precompLayers.push(cand);
        }
    }

    for (var p = 0; p < precompLayers.length; p++) {
        writeHeartbeat(jobId, p + 1, precompLayers.length, "Reverse Stufe 1: Entpacke " + precompLayers[p].name);
        if (unpackPrecompToMain(comp, precompLayers[p])) {
            unpackedPrecomps++;
        }
    }

    return {
        removedAdj: removedAdj,
        unpackedPrecomps: unpackedPrecomps
    };
}

function runStep(job) {
    var jobId = (job && job.jobId) ? String(job.jobId) : "";
    var settings = (job && job.settings) ? job.settings : {};

    var comp = getSafeActiveComp();
    if (!comp) {
        return buildErrorResult(jobId, null, "Bitte öffne zuerst eine Komposition für das Reverse-Rollback.");
    }

    var reverseMode = settings.reverseMode ? trimStr(settings.reverseMode) : "Stage2Only";
    var modeLower = reverseMode.toLowerCase();
    var doStage2 = (modeLower === "stage2only" || modeLower === "full");
    var doStage1 = (modeLower === "stage1only" || modeLower === "full");

    if (!doStage2 && !doStage1) {
        return buildErrorResult(jobId, null, "Unbekannter reverseMode: " + reverseMode);
    }

    comp.openInViewer();
    writeHeartbeat(jobId, 0, 1, "Starte Reverse-Rollback (" + reverseMode + ")...");

    var stage2Stats = { removedAdj: 0, unpackedGroups: 0 };
    var stage1Stats = { removedAdj: 0, unpackedPrecomps: 0 };

    safeBeginUndoGroup("AE Orchestrator: Reverse Rollback (" + reverseMode + ")");
    try {
        if (doStage2) {
            stage2Stats = reverseStage2(comp, settings.excludedNames, jobId);
        }
        if (doStage1) {
            stage1Stats = reverseStage1(comp, settings.excludedNames, jobId);
        }
        deselectAllLayers(comp);
    } finally {
        safeEndUndoGroup();
    }

    var totalUnpacked = stage2Stats.unpackedGroups + stage1Stats.unpackedPrecomps;
    var totalRemovedAdj = stage2Stats.removedAdj + stage1Stats.removedAdj;
    var state = detectProjectState(comp, settings, null);
    var msg = "Reverse (" + reverseMode + ") abgeschlossen: " +
        totalUnpacked + " Precomps entpackt, " +
        totalRemovedAdj + " Adjustment-Layer entfernt.";

    writeHeartbeat(jobId, 1, 1, msg);

    return buildOkResult(jobId, comp, {
        totalClips: totalUnpacked,
        processedInBatch: totalUnpacked,
        skippedInBatch: 0,
        hasMoreBatches: false,
        nextBatchIndex: totalUnpacked,
        detectedState: state,
        message: msg
    });
}
