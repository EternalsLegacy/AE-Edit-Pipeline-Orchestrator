/*
 * 07_GroupClipsForZoom.jsx
 * Collects each base clip together with its associated blur adjustment layers,
 * pre-composes them into ZoomGroup_<clipNum>_<In/Out>, trims duration, and sets
 * [DONE_ZOOM_GROUP] [EDIT_CLIP:<clipNum>].
 */

function runStep(job) {
    var jobId = (job && job.jobId) ? String(job.jobId) : "";
    var settings = (job && job.settings) ? job.settings : {};
    var useSelectedOnly = Boolean(job && job.useSelectedOnly);

    var comp = getSafeActiveComp();
    if (!comp) {
        return buildErrorResult(jobId, null, "Bitte öffne zuerst deine Edit-Komposition.");
    }

    var startWithIn = (String(settings.blurZoomStart || "BlurZoomIn").toLowerCase() !== "blurzoomout");

    comp.openInViewer();
    var savedSelection = useSelectedOnly ? captureSelectedLayers(comp) : null;
    var baseClips = collectSortedEligibleLayers(comp, useSelectedOnly, settings.excludedNames, false);
    var totalClips = baseClips.length;

    if (totalClips === 0) {
        return buildErrorResult(jobId, null, "Keine Basis-Clips zum Gruppieren gefunden.");
    }

    var slice = getBatchSlice(totalClips, job.batchStartIndex, job.batchSize);
    var frameDur = comp.frameDuration;
    var processedCount = 0;
    var skippedCount = 0;
    var aborted = false;

    safeBeginUndoGroup("AE Orchestrator: Group Clips For Zoom");
    try {
        for (var idx = slice.startIndex; idx < slice.endIndex; idx++) {
            if (isAbortRequested()) {
                aborted = true;
                break;
            }

            var baseClip = baseClips[idx];
            var existingNum = extractClipNumber(baseClip.comment);
            var clipNum = (existingNum !== null) ? existingNum : (idx + 1);
            var clipTag = "[EDIT_CLIP:" + clipNum + "]";
            var isZoomIn = startWithIn ? (idx % 2 === 0) : (idx % 2 === 1);
            var zoomTag = isZoomIn ? "In" : "Out";

            writeHeartbeat(jobId, idx + 1, totalClips, "ZoomGroup Clip " + clipNum + " / " + totalClips + " (" + zoomTag + ")");

            if (baseClip.locked || hasCommentTag(baseClip, "[DONE_ZOOM_GROUP]") || startsWith(baseClip.name, "ZoomGroup_")) {
                skippedCount++;
                continue;
            }

            var clipIn = baseClip.inPoint;
            var clipOut = baseClip.outPoint;
            var clipSpan = Math.max(frameDur, clipOut - clipIn);

            var adjLayers = findAssociatedAdjustmentLayers(comp, baseClip, settings.excludedNames);
            var capturedSlot = savedSelection ? findCapturedLayerSlot(savedSelection, baseClip) : -1;
            var groupIndices = [baseClip.index];
            for (var a = 0; a < adjLayers.length; a++) {
                groupIndices.push(adjLayers[a].index);
                if (savedSelection) {
                    var adjSlot = findCapturedLayerSlot(savedSelection, adjLayers[a]);
                    if (adjSlot >= 0) {
                        savedSelection[adjSlot] = null;
                    }
                }
            }
            groupIndices.sort(function (x, y) {
                return x - y;
            });

            var topIndex = groupIndices[0];
            var groupName = "ZoomGroup_" + clipNum + "_" + zoomTag;
            var groupCompItem = comp.layers.precompose(groupIndices, groupName, true);

            for (var m = 1; m <= groupCompItem.numLayers; m++) {
                var inner = groupCompItem.layer(m);
                inner.startTime = inner.startTime - clipIn;
            }
            groupCompItem.duration = clipSpan;

            var groupLayer = comp.layer(topIndex);
            groupLayer.startTime = clipIn;
            groupLayer.inPoint = clipIn;
            groupLayer.outPoint = clipOut;
            groupLayer.comment = "[DONE_ZOOM_GROUP] " + clipTag;

            if (savedSelection && capturedSlot >= 0) {
                replaceCapturedLayerAt(savedSelection, capturedSlot, groupLayer);
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
        return buildAbortedResult(jobId, comp, "ZoomGroup-Gruppierung sauber abgebrochen.");
    }

    return buildOkResult(jobId, comp, {
        totalClips: totalClips,
        processedInBatch: processedCount,
        skippedInBatch: skippedCount,
        hasMoreBatches: slice.hasMoreBatches,
        nextBatchIndex: slice.nextBatchIndex,
        message: "ZoomGroup Batch abgeschlossen: " + processedCount + " gruppiert, " + skippedCount + " übersprungen."
    });
}
