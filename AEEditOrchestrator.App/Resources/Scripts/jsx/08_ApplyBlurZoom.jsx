/*
 * 08_ApplyBlurZoom.jsx
 * Finds ZoomGroup layers, creates BlurZoom_<In/Out>_<clipNum> adjustment layers
 * with optional Motion Tile, applies alternating BlurZoomIn / BlurZoomOut presets,
 * and stretches keyframes proportionally to [clipIn .. clipOut].
 */

function hasExistingBlurZoomAdjustment(comp, groupLayer, clipTag, clipIn, clipOut, excludedNames) {
    var total = comp.numLayers;
    var eps = 0.002;
    for (var i = 1; i <= total; i++) {
        var cand = comp.layer(i);
        if (!cand.adjustmentLayer) {
            continue;
        }
        if (isProtectedGlobalLayer(cand, total, excludedNames)) {
            continue;
        }
        if (!startsWith(cand.name, "BlurZoom_") && !hasCommentTag(cand, "[DONE_ZOOM_ADJ]")) {
            continue;
        }

        var candClipTag = extractClipTag(cand.comment);
        if (clipTag !== null && candClipTag !== null) {
            if (candClipTag === clipTag) {
                return true;
            }
            continue;
        }
        if (Math.abs(cand.inPoint - clipIn) <= eps && Math.abs(cand.outPoint - clipOut) <= eps) {
            return true;
        }
    }
    return false;
}

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
    var zoomInName = presetNames.blurZoomIn ? presetNames.blurZoomIn : "BlurZoomIn";
    var zoomOutName = presetNames.blurZoomOut ? presetNames.blurZoomOut : "BlurZoomOut";
    var presetIn = requirePresetFile(presetsRoot, zoomInName);
    var presetOut = requirePresetFile(presetsRoot, zoomOutName);

    var startWithIn = (String(settings.blurZoomStart || "BlurZoomIn").toLowerCase() !== "blurzoomout");
    var addTileToZoom = Boolean(settings.addMotionTileToBlurZoom);
    var tileWidth = (typeof settings.motionTileWidth === "number" && settings.motionTileWidth > 0) ? settings.motionTileWidth : 370;
    var tileHeight = (typeof settings.motionTileHeight === "number" && settings.motionTileHeight > 0) ? settings.motionTileHeight : 370;

    comp.openInViewer();
    var savedSelection = useSelectedOnly ? captureSelectedLayers(comp) : null;
    var eligibleLayers = collectSortedEligibleLayers(comp, useSelectedOnly, settings.excludedNames, false);

    var zoomGroups = [];
    for (var i = 0; i < eligibleLayers.length; i++) {
        var lyr = eligibleLayers[i];
        if (hasCommentTag(lyr, "[DONE_ZOOM_GROUP]") || startsWith(lyr.name, "ZoomGroup_")) {
            zoomGroups.push(lyr);
        }
    }

    var totalClips = zoomGroups.length;
    if (totalClips === 0) {
        return buildErrorResult(jobId, null, "Keine ZoomGroup-Layer ([DONE_ZOOM_GROUP] / ZoomGroup_) gefunden.");
    }

    var slice = getBatchSlice(totalClips, job.batchStartIndex, job.batchSize);
    var processedCount = 0;
    var skippedCount = 0;

    safeBeginUndoGroup("AE Orchestrator: Apply BlurZoom");
    try {
        for (var idx = slice.startIndex; idx < slice.endIndex; idx++) {
            var groupLayer = zoomGroups[idx];
            var clipTag = extractClipTag(groupLayer.comment);
            var existingNum = extractClipNumber(groupLayer.comment);
            var clipNum = (existingNum !== null) ? existingNum : (idx + 1);
            var isZoomIn = startWithIn ? (idx % 2 === 0) : (idx % 2 === 1);
            var zoomTag = isZoomIn ? "In" : "Out";

            writeHeartbeat(jobId, idx + 1, totalClips, "BlurZoom" + zoomTag + " Clip " + clipNum + " / " + totalClips);

            var clipIn = groupLayer.inPoint;
            var clipOut = groupLayer.outPoint;

            if (hasCommentTag(groupLayer, "[DONE_BLURZOOM]") ||
                hasExistingBlurZoomAdjustment(comp, groupLayer, clipTag, clipIn, clipOut, settings.excludedNames)) {
                skippedCount++;
                continue;
            }

            var adjComment = clipTag ? ("[DONE_ZOOM_ADJ] " + clipTag) : "[DONE_ZOOM_ADJ]";
            var zoomAdj = createTrimmedAdjustmentLayer(
                comp,
                "BlurZoom_" + zoomTag + "_" + clipNum,
                clipIn,
                clipOut,
                groupLayer,
                adjComment
            );

            if (addTileToZoom) {
                addMotionTile(zoomAdj, tileWidth, tileHeight);
            }

            var activePreset = isZoomIn ? presetIn : presetOut;
            applyPresetAtTime(comp, zoomAdj, activePreset, clipIn);

            var animatedProps = [];
            collectAnimatedProperties(zoomAdj, animatedProps);
            stretchKeyframesToSpan(animatedProps, clipIn, clipOut);

            appendCommentTag(groupLayer, "[DONE_BLURZOOM]");
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
        message: "BlurZoom Batch abgeschlossen: " + processedCount + " angewendet, " + skippedCount + " übersprungen."
    });
}
