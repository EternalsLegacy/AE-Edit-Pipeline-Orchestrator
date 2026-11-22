/*
 * 99_ReverseRollback.jsx
 * Granular reverse rollback script supporting Stage2Only, Stage1Only, and Full modes.
 * Strictly protects locked/excluded layers, uses reference-diffing instead of hardcoded
 * index(1) after copyToComp(), and only touches orchestrator-tagged layers.
 */

function snapshotCompLayers(comp) {
    var list = [];
    if (!comp) {
        return list;
    }
    for (var i = 1; i <= comp.numLayers; i++) {
        list.push(comp.layer(i));
    }
    return list;
}

function findNewlyCopiedLayer(comp, existingLayers) {
    if (!comp) {
        return null;
    }
    for (var i = 1; i <= comp.numLayers; i++) {
        var cand = comp.layer(i);
        if (arrayIndexOf(existingLayers, cand) === -1) {
            return cand;
        }
    }
    return null;
}

function unpackPrecompToMain(comp, targetLayer, excludedNames) {
    if (!comp || !targetLayer || !(targetLayer instanceof AVLayer) || !(targetLayer.source instanceof CompItem)) {
        return false;
    }

    if (targetLayer.locked || isProtectedGlobalLayer(targetLayer, comp.numLayers, excludedNames)) {
        return false;
    }

    var innerComp = targetLayer.source;
    var groupStart = targetLayer.startTime;
    var groupIn = targetLayer.inPoint;
    var groupOut = targetLayer.outPoint;

    deselectAllLayers(comp);
    deselectAllLayers(innerComp);

    for (var m = 1; m <= innerComp.numLayers; m++) {
        var inner = innerComp.layer(m);
        var origStart = inner.startTime;
        var origIn = inner.inPoint;
        var origOut = inner.outPoint;
        var innerWasLocked = inner.locked;

        var existingLayers = snapshotCompLayers(comp);
        var expectedCount = existingLayers.length + 1;

        if (innerWasLocked) {
            inner.locked = false;
        }

        try {
            inner.copyToComp(comp);
        } finally {
            if (innerWasLocked) {
                try {
                    inner.locked = true;
                } catch (eRelockInner) {}
            }
        }

        if (comp.numLayers !== expectedCount) {
            throw new Error(
                "Unerwartete Layer-Anzahl nach copyToComp() bei '" + inner.name +
                "' (erwartet " + expectedCount + ", erhalten " + comp.numLayers + ")."
            );
        }

        var copied = findNewlyCopiedLayer(comp, existingLayers);
        if (!copied) {
            throw new Error("Neu kopierter Layer '" + inner.name + "' konnte nicht eindeutig identifiziert werden.");
        }

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

function isStage2GeneratedAdjustment(layer, totalLayers, excludedNames) {
    if (!layer || !layer.adjustmentLayer || layer.locked) {
        return false;
    }
    if (isProtectedGlobalLayer(layer, totalLayers, excludedNames)) {
        return false;
    }
    if (hasCommentTag(layer, "[DONE_ZOOM_ADJ]")) {
        return true;
    }
    if (startsWith(layer.name, "BlurZoom_") && hasCommentTag(layer, "[EDIT_CLIP:")) {
        return true;
    }
    return false;
}

function isStage2GeneratedGroupLayer(layer, totalLayers, excludedNames) {
    if (!layer || layer.adjustmentLayer || layer.locked) {
        return false;
    }
    if (isProtectedGlobalLayer(layer, totalLayers, excludedNames)) {
        return false;
    }
    if (!(layer instanceof AVLayer) || !(layer.source instanceof CompItem)) {
        return false;
    }
    if (hasCommentTag(layer, "[DONE_ZOOM_GROUP]")) {
        return true;
    }
    if (/^ZoomGroup_\d+_(In|Out)$/.test(layer.name)) {
        return true;
    }
    return false;
}

function isStage1GeneratedAdjustment(layer, totalLayers, excludedNames) {
    if (!layer || !layer.adjustmentLayer || layer.locked) {
        return false;
    }
    if (isProtectedGlobalLayer(layer, totalLayers, excludedNames)) {
        return false;
    }
    var hasPrefix = (
        startsWith(layer.name, "StartBlur_") ||
        startsWith(layer.name, "EndBlur200_") ||
        startsWith(layer.name, "EndBlur80_")
    );
    if (!hasPrefix) {
        return false;
    }
    if (hasCommentTag(layer, "[EDIT_CLIP:")) {
        return true;
    }
    if (/^((StartBlur|EndBlur200|EndBlur80)_[VH]_\d+)$/.test(layer.name)) {
        return true;
    }
    return false;
}

function isStage1GeneratedPrecompLayer(layer, totalLayers, excludedNames) {
    if (!layer || layer.adjustmentLayer || layer.locked) {
        return false;
    }
    if (isProtectedGlobalLayer(layer, totalLayers, excludedNames)) {
        return false;
    }
    if (startsWith(layer.name, "ZoomGroup_") || hasCommentTag(layer, "[DONE_ZOOM_GROUP]")) {
        return false;
    }
    if (!(layer instanceof AVLayer) || !(layer.source instanceof CompItem)) {
        return false;
    }
    if (hasCommentTag(layer, "[EDIT_CLIP:") || hasCommentTag(layer, "[DONE_PIPELINE_1]")) {
        return true;
    }
    return false;
}

function reverseStage2(comp, excludedNames, jobId) {
    var removedAdj = 0;
    var unpackedGroups = 0;

    for (var i = comp.numLayers; i >= 1; i--) {
        if (isAbortRequested()) {
            return {
                removedAdj: removedAdj,
                unpackedGroups: unpackedGroups,
                aborted: true
            };
        }

        var lyr = comp.layer(i);
        var totalNow = comp.numLayers;
        if (isStage2GeneratedAdjustment(lyr, totalNow, excludedNames)) {
            lyr.remove();
            removedAdj++;
        }
    }

    var zoomGroupLayers = [];
    var totalAfterAdj = comp.numLayers;
    for (var j = 1; j <= totalAfterAdj; j++) {
        var cand = comp.layer(j);
        if (isStage2GeneratedGroupLayer(cand, totalAfterAdj, excludedNames)) {
            zoomGroupLayers.push(cand);
        }
    }

    for (var g = 0; g < zoomGroupLayers.length; g++) {
        if (isAbortRequested()) {
            return {
                removedAdj: removedAdj,
                unpackedGroups: unpackedGroups,
                aborted: true
            };
        }

        writeHeartbeat(jobId, g + 1, zoomGroupLayers.length, "Reverse Stufe 2: Entpacke " + zoomGroupLayers[g].name);
        if (unpackPrecompToMain(comp, zoomGroupLayers[g], excludedNames)) {
            unpackedGroups++;
        }
    }

    return {
        removedAdj: removedAdj,
        unpackedGroups: unpackedGroups,
        aborted: false
    };
}

function reverseStage1(comp, excludedNames, jobId) {
    var removedAdj = 0;
    var unpackedPrecomps = 0;

    for (var i = comp.numLayers; i >= 1; i--) {
        if (isAbortRequested()) {
            return {
                removedAdj: removedAdj,
                unpackedPrecomps: unpackedPrecomps,
                aborted: true
            };
        }

        var lyr = comp.layer(i);
        var totalNow = comp.numLayers;
        if (isStage1GeneratedAdjustment(lyr, totalNow, excludedNames)) {
            lyr.remove();
            removedAdj++;
        }
    }

    var precompLayers = [];
    var totalAfterAdj = comp.numLayers;
    for (var j = 1; j <= totalAfterAdj; j++) {
        var cand = comp.layer(j);
        if (isStage1GeneratedPrecompLayer(cand, totalAfterAdj, excludedNames)) {
            precompLayers.push(cand);
        }
    }

    for (var p = 0; p < precompLayers.length; p++) {
        if (isAbortRequested()) {
            return {
                removedAdj: removedAdj,
                unpackedPrecomps: unpackedPrecomps,
                aborted: true
            };
        }

        writeHeartbeat(jobId, p + 1, precompLayers.length, "Reverse Stufe 1: Entpacke " + precompLayers[p].name);
        if (unpackPrecompToMain(comp, precompLayers[p], excludedNames)) {
            unpackedPrecomps++;
        }
    }

    return {
        removedAdj: removedAdj,
        unpackedPrecomps: unpackedPrecomps,
        aborted: false
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

    var stage2Stats = { removedAdj: 0, unpackedGroups: 0, aborted: false };
    var stage1Stats = { removedAdj: 0, unpackedPrecomps: 0, aborted: false };

    safeBeginUndoGroup("AE Orchestrator: Reverse Rollback (" + reverseMode + ")");
    try {
        if (doStage2) {
            stage2Stats = reverseStage2(comp, settings.excludedNames, jobId);
        }
        if (doStage1 && !stage2Stats.aborted) {
            stage1Stats = reverseStage1(comp, settings.excludedNames, jobId);
        }
        deselectAllLayers(comp);
    } finally {
        safeEndUndoGroup();
    }

    if (stage2Stats.aborted || stage1Stats.aborted) {
        return buildAbortedResult(jobId, comp, "Reverse-Rollback sauber abgebrochen.");
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
