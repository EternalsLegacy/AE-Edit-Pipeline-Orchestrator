/*
 * 06_CreateEndBlurs.jsx
 * Creates EndBlur200 and EndBlur80 1-frame adjustment layers at the end of each clip
 * with Motion Tile and Directional Blur synchronized with settings.startDirection,
 * then appends [DONE_PIPELINE_1] while keeping [EDIT_CLIP:<clipNum>].
 */

function runStep(job) {
    var jobId = (job && job.jobId) ? String(job.jobId) : "";
    var settings = (job && job.settings) ? job.settings : {};
    var useSelectedOnly = Boolean(job && job.useSelectedOnly);

    var comp = getSafeActiveComp();
    if (!comp) {
        return buildErrorResult(jobId, null, "Bitte öffne zuerst deine Edit-Komposition.");
    }

    var startVertical = (String(settings.startDirection || "Vertical").toLowerCase() !== "horizontal");
    var tileWidth = (typeof settings.motionTileWidth === "number" && settings.motionTileWidth > 0) ? settings.motionTileWidth : 370;
    var tileHeight = (typeof settings.motionTileHeight === "number" && settings.motionTileHeight > 0) ? settings.motionTileHeight : 370;
    var endBlur1Length = (typeof settings.endBlur1Length === "number") ? settings.endBlur1Length : 200;
    var endBlur2Length = (typeof settings.endBlur2Length === "number") ? settings.endBlur2Length : 80;
    var verticalBlurDir = (typeof settings.verticalBlurDir === "number") ? settings.verticalBlurDir : 90;
    var horizontalBlurDir = (typeof settings.horizontalBlurDir === "number") ? settings.horizontalBlurDir : 180;

    comp.openInViewer();
    var savedSelection = useSelectedOnly ? captureSelectedLayers(comp) : null;
    var targetLayers = collectSortedEligibleLayers(comp, useSelectedOnly, settings.excludedNames, false);
    var totalClips = targetLayers.length;

    if (totalClips === 0) {
        return buildErrorResult(jobId, null, "Keine gültigen Clip-Layer für End-Blurs gefunden.");
    }

    var slice = getBatchSlice(totalClips, job.batchStartIndex, job.batchSize);
    var frameDur = comp.frameDuration;
    var processedCount = 0;
    var skippedCount = 0;

    safeBeginUndoGroup("AE Orchestrator: Create End Blurs");
    try {
        for (var idx = slice.startIndex; idx < slice.endIndex; idx++) {
            var clipLayer = targetLayers[idx];
            var existingNum = extractClipNumber(clipLayer.comment);
            var clipNum = (existingNum !== null) ? existingNum : (idx + 1);
            var clipTag = "[EDIT_CLIP:" + clipNum + "]";
            var isVert = startVertical ? (idx % 2 === 0) : (idx % 2 === 1);
            var dirTag = isVert ? "V" : "H";
            var blurDir = isVert ? verticalBlurDir : horizontalBlurDir;

            writeHeartbeat(jobId, idx + 1, totalClips, "EndBlurs (" + dirTag + ") Clip " + clipNum + " / " + totalClips);

            if (hasCommentTag(clipLayer, "[DONE_PIPELINE_1]") || hasCommentTag(clipLayer, "[DONE_ZOOM_GROUP]")) {
                skippedCount++;
                continue;
            }

            if (!extractClipTag(clipLayer.comment)) {
                appendCommentTag(clipLayer, clipTag);
            }

            var assoc = findAssociatedAdjustmentLayers(comp, clipLayer, settings.excludedNames);
            var hasEnd200 = false;
            var hasEnd80 = false;
            for (var a = 0; a < assoc.length; a++) {
                if (startsWith(assoc[a].name, "EndBlur200_")) {
                    hasEnd200 = true;
                } else if (startsWith(assoc[a].name, "EndBlur80_")) {
                    hasEnd80 = true;
                }
            }

            if (hasEnd200 && hasEnd80) {
                appendCommentTag(clipLayer, "[DONE_PIPELINE_1]");
                skippedCount++;
                continue;
            }

            var clipIn = clipLayer.inPoint;
            var clipOut = clipLayer.outPoint;
            var clipSpan = Math.max(frameDur, clipOut - clipIn);

            if (clipSpan >= (2 * frameDur - 0.0001)) {
                var topMost = findTopMostLayerForClip(comp, clipLayer, settings.excludedNames);

                if (!hasEnd200) {
                    var endBlur1 = createTrimmedAdjustmentLayer(
                        comp,
                        "EndBlur200_" + dirTag + "_" + clipNum,
                        clipOut - (2 * frameDur),
                        clipOut - (1 * frameDur),
                        topMost,
                        clipTag
                    );
                    addMotionTile(endBlur1, tileWidth, tileHeight);
                    addDirectionalBlur(endBlur1, blurDir, endBlur1Length);
                    topMost = endBlur1;
                }

                if (!hasEnd80) {
                    var endBlur2 = createTrimmedAdjustmentLayer(
                        comp,
                        "EndBlur80_" + dirTag + "_" + clipNum,
                        clipOut - (1 * frameDur),
                        clipOut,
                        topMost,
                        clipTag
                    );
                    addMotionTile(endBlur2, tileWidth, tileHeight);
                    addDirectionalBlur(endBlur2, blurDir, endBlur2Length);
                }
            }

            appendCommentTag(clipLayer, "[DONE_PIPELINE_1]");
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

    return buildOkResult(jobId, comp, {
        totalClips: totalClips,
        processedInBatch: processedCount,
        skippedInBatch: skippedCount,
        hasMoreBatches: slice.hasMoreBatches,
        nextBatchIndex: slice.nextBatchIndex,
        message: "End-Blurs Batch abgeschlossen: " + processedCount + " verarbeitet, " + skippedCount + " übersprungen."
    });
}
