/*
 * 05_CreateStartBlurs.jsx
 * Creates the StartBlur_<V/H>_<clipNum> adjustment layer directly above each clip's
 * topmost layer, adds Motion Tile, and applies VerticalBlur / HorizontalBlur
 * synchronized with settings.startDirection.
 */

function runStep(job) {
    var jobId = (job && job.jobId) ? String(job.jobId) : "";
    var settings = (job && job.settings) ? job.settings : {};
    var useSelectedOnly = Boolean(job && job.useSelectedOnly);

    var comp = getSafeActiveComp();
    if (!comp) {
        return buildErrorResult(jobId, null, "Bitte öffne zuerst deine Edit-Komposition.");
    }

    var presetsRoot = resolvePresetsFolder(settings.presetsRoot);
    var presetNames = settings.presets || {};
    var vBlurName = presetNames.verticalBlur ? presetNames.verticalBlur : "VerticalBlur";
    var hBlurName = presetNames.horizontalBlur ? presetNames.horizontalBlur : "HorizontalBlur";
    var vBlurPreset = requirePresetFile(presetsRoot, vBlurName);
    var hBlurPreset = requirePresetFile(presetsRoot, hBlurName);

    var startVertical = (String(settings.startDirection || "Vertical").toLowerCase() !== "horizontal");
    var tileWidth = (typeof settings.motionTileWidth === "number" && settings.motionTileWidth > 0) ? settings.motionTileWidth : 370;
    var tileHeight = (typeof settings.motionTileHeight === "number" && settings.motionTileHeight > 0) ? settings.motionTileHeight : 370;
    var startBlurFrames = (typeof settings.startBlurFrames === "number" && settings.startBlurFrames > 0) ? settings.startBlurFrames : 10;

    comp.openInViewer();
    var savedSelection = useSelectedOnly ? captureSelectedLayers(comp) : null;
    var targetLayers = collectSortedEligibleLayers(comp, useSelectedOnly, settings.excludedNames, false);
    var totalClips = targetLayers.length;

    if (totalClips === 0) {
        return buildErrorResult(jobId, null, "Keine gültigen Clip-Layer für Start-Blurs gefunden.");
    }

    var slice = getBatchSlice(totalClips, job.batchStartIndex, job.batchSize);
    var frameDur = comp.frameDuration;
    var processedCount = 0;
    var skippedCount = 0;
    var aborted = false;

    safeBeginUndoGroup("AE Orchestrator: Create Start Blurs");
    try {
        for (var idx = slice.startIndex; idx < slice.endIndex; idx++) {
            if (isAbortRequested()) {
                aborted = true;
                break;
            }

            var clipLayer = targetLayers[idx];
            var existingNum = extractClipNumber(clipLayer.comment);
            var clipNum = (existingNum !== null) ? existingNum : (idx + 1);
            var clipTag = "[EDIT_CLIP:" + clipNum + "]";
            var isVert = startVertical ? (idx % 2 === 0) : (idx % 2 === 1);
            var dirTag = isVert ? "V" : "H";

            writeHeartbeat(jobId, idx + 1, totalClips, "StartBlur (" + dirTag + ") Clip " + clipNum + " / " + totalClips);

            if (hasCommentTag(clipLayer, "[DONE_PIPELINE_1]") || hasCommentTag(clipLayer, "[DONE_ZOOM_GROUP]")) {
                skippedCount++;
                continue;
            }

            if (!extractClipTag(clipLayer.comment)) {
                appendCommentTag(clipLayer, clipTag);
            }

            var assoc = findAssociatedAdjustmentLayers(comp, clipLayer, settings.excludedNames);
            var alreadyHasStartBlur = false;
            for (var a = 0; a < assoc.length; a++) {
                if (startsWith(assoc[a].name, "StartBlur_")) {
                    alreadyHasStartBlur = true;
                    break;
                }
            }
            if (alreadyHasStartBlur) {
                skippedCount++;
                continue;
            }

            var clipIn = clipLayer.inPoint;
            var clipOut = clipLayer.outPoint;
            var blurOut = Math.min(clipOut, clipIn + (startBlurFrames * frameDur));
            var topMost = findTopMostLayerForClip(comp, clipLayer, settings.excludedNames);
            var blurPreset = isVert ? vBlurPreset : hBlurPreset;

            var startBlurAdj = createTrimmedAdjustmentLayer(
                comp,
                "StartBlur_" + dirTag + "_" + clipNum,
                clipIn,
                blurOut,
                topMost,
                clipTag
            );
            addMotionTile(startBlurAdj, tileWidth, tileHeight);
            applyPresetAtTime(comp, startBlurAdj, blurPreset, clipIn);

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
        return buildAbortedResult(jobId, comp, "Start-Blurs sauber abgebrochen.");
    }

    return buildOkResult(jobId, comp, {
        totalClips: totalClips,
        processedInBatch: processedCount,
        skippedInBatch: skippedCount,
        hasMoreBatches: slice.hasMoreBatches,
        nextBatchIndex: slice.nextBatchIndex,
        message: "Start-Blur Batch abgeschlossen: " + processedCount + " erstellt, " + skippedCount + " übersprungen."
    });
}
