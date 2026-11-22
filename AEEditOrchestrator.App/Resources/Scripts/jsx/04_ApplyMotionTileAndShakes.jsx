/*
 * 04_ApplyMotionTileAndShakes.jsx
 * Adds Motion Tile (Mirror Edges) if absent and applies alternating Vertical/Horizontal
 * Shake presets synchronized with settings.startDirection.
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
    var vShakeName = presetNames.verticalShake ? presetNames.verticalShake : "VerticalShake";
    var hShakeName = presetNames.horizontalShake ? presetNames.horizontalShake : "HorizontalShake";
    var vShakePreset = requirePresetFile(presetsRoot, vShakeName);
    var hShakePreset = requirePresetFile(presetsRoot, hShakeName);

    var startVertical = (String(settings.startDirection || "Vertical").toLowerCase() !== "horizontal");
    var tileWidth = (typeof settings.motionTileWidth === "number" && settings.motionTileWidth > 0) ? settings.motionTileWidth : 370;
    var tileHeight = (typeof settings.motionTileHeight === "number" && settings.motionTileHeight > 0) ? settings.motionTileHeight : 370;

    comp.openInViewer();
    var savedSelection = useSelectedOnly ? captureSelectedLayers(comp) : null;
    var targetLayers = collectSortedEligibleLayers(comp, useSelectedOnly, settings.excludedNames, false);
    var totalClips = targetLayers.length;

    if (totalClips === 0) {
        return buildErrorResult(jobId, null, "Keine gültigen Clip-Layer für Motion Tile & Shakes gefunden.");
    }

    var slice = getBatchSlice(totalClips, job.batchStartIndex, job.batchSize);
    var processedCount = 0;
    var skippedCount = 0;

    safeBeginUndoGroup("AE Orchestrator: Motion Tile & Shakes");
    try {
        for (var idx = slice.startIndex; idx < slice.endIndex; idx++) {
            var clipLayer = targetLayers[idx];
            var existingNum = extractClipNumber(clipLayer.comment);
            var clipNum = (existingNum !== null) ? existingNum : (idx + 1);
            var isVert = startVertical ? (idx % 2 === 0) : (idx % 2 === 1);
            var dirTag = isVert ? "V" : "H";

            writeHeartbeat(jobId, idx + 1, totalClips, "Shake (" + dirTag + ") Clip " + clipNum + " / " + totalClips + ": " + clipLayer.name);

            if (hasCommentTag(clipLayer, "[DONE_PIPELINE_1]") ||
                hasCommentTag(clipLayer, "[DONE_ZOOM_GROUP]") ||
                hasCommentTag(clipLayer, "[DONE_SHAKES]")) {
                skippedCount++;
                continue;
            }

            if (!hasEffectByMatchName(clipLayer, "ADBE Tile")) {
                addMotionTile(clipLayer, tileWidth, tileHeight);
            }

            var shakePreset = isVert ? vShakePreset : hShakePreset;
            applyPresetAtTime(comp, clipLayer, shakePreset, clipLayer.inPoint);
            appendCommentTag(clipLayer, "[DONE_SHAKES]");
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
        message: "Motion Tile & Shakes Batch abgeschlossen: " + processedCount + " verarbeitet, " + skippedCount + " übersprungen."
    });
}
