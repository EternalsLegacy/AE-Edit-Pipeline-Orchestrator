/*
 * 02_PrecomposeClips.jsx
 * Pre-composes each eligible clip individually (Move all attributes), trims the
 * resulting precomp duration to clipSpan, re-positions it, and sets [EDIT_CLIP:<n>].
 */

function runStep(job) {
    var jobId = (job && job.jobId) ? String(job.jobId) : "";
    var settings = (job && job.settings) ? job.settings : {};
    var useSelectedOnly = Boolean(job && job.useSelectedOnly);

    var comp = getSafeActiveComp();
    if (!comp) {
        return buildErrorResult(jobId, null, "Bitte öffne zuerst deine Edit-Komposition.");
    }

    comp.openInViewer();
    var savedSelection = useSelectedOnly ? captureSelectedLayers(comp) : null;
    var targetLayers = collectSortedEligibleLayers(comp, useSelectedOnly, settings.excludedNames, false);
    var totalClips = targetLayers.length;

    if (totalClips === 0) {
        return buildErrorResult(jobId, null, "Keine gültigen Clip-Layer gefunden.");
    }

    var slice = getBatchSlice(totalClips, job.batchStartIndex, job.batchSize);
    var frameDur = comp.frameDuration;
    var processedCount = 0;
    var skippedCount = 0;
    var aborted = false;

    safeBeginUndoGroup("AE Orchestrator: Precompose Clips");
    try {
        for (var idx = slice.startIndex; idx < slice.endIndex; idx++) {
            if (isAbortRequested()) {
                aborted = true;
                break;
            }

            var clipLayer = targetLayers[idx];
            var clipNum = idx + 1;
            var clipTag = "[EDIT_CLIP:" + clipNum + "]";

            writeHeartbeat(jobId, idx + 1, totalClips, "Pre-compose Clip " + clipNum + " / " + totalClips + ": " + clipLayer.name);

            if (clipLayer.locked ||
                hasCommentTag(clipLayer, "[EDIT_CLIP:") ||
                hasCommentTag(clipLayer, "[DONE_PIPELINE_1]") ||
                hasCommentTag(clipLayer, "[DONE_ZOOM_GROUP]") ||
                startsWith(clipLayer.name, "Pre-comp ") ||
                startsWith(clipLayer.name, "ZoomGroup_")) {
                skippedCount++;
                continue;
            }

            var clipIn = clipLayer.inPoint;
            var clipOut = clipLayer.outPoint;
            var clipSpan = Math.max(frameDur, clipOut - clipIn);
            var origIdx = clipLayer.index;
            var precompName = "Pre-comp " + clipNum + " - " + clipLayer.name;

            var precompItem = comp.layers.precompose([origIdx], precompName, true);
            var innerLayer = precompItem.layer(1);
            innerLayer.startTime = innerLayer.startTime - clipIn;
            precompItem.duration = clipSpan;

            var newClipLayer = comp.layer(origIdx);
            newClipLayer.startTime = clipIn;
            newClipLayer.inPoint = clipIn;
            newClipLayer.outPoint = clipOut;
            appendCommentTag(newClipLayer, clipTag);

            if (savedSelection) {
                replaceCapturedLayer(savedSelection, clipLayer, newClipLayer);
            }

            processedCount++;
        }
    } finally {
        safeEndUndoGroup();
        if (useSelectedOnly && savedSelection) {
            restoreSelectedLayers(comp, savedSelection);
        } else {
            deselectAllLayers(comp);
        }
    }

    if (aborted) {
        return buildAbortedResult(jobId, comp, "Pre-compose sauber abgebrochen.");
    }

    return buildOkResult(jobId, comp, {
        totalClips: totalClips,
        processedInBatch: processedCount,
        skippedInBatch: skippedCount,
        hasMoreBatches: slice.hasMoreBatches,
        nextBatchIndex: slice.nextBatchIndex,
        message: "Pre-compose Batch abgeschlossen: " + processedCount + " verarbeitet, " + skippedCount + " übersprungen."
    });
}
