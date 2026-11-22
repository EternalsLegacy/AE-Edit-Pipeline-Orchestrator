/*
 * 03_ApplyFadeInOut.jsx
 * Applies FadeInOut preset to eligible base clips and stretches animated keyframes
 * proportionally to [inPoint .. outPoint].
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
    var fadeName = presetNames.fadeInOut ? presetNames.fadeInOut : "FadeInOut";
    var fadePreset = requirePresetFile(presetsRoot, fadeName);

    comp.openInViewer();
    var savedSelection = useSelectedOnly ? captureSelectedLayers(comp) : null;
    var targetLayers = collectSortedEligibleLayers(comp, useSelectedOnly, settings.excludedNames, false);
    var totalClips = targetLayers.length;

    if (totalClips === 0) {
        return buildErrorResult(jobId, null, "Keine gültigen Clip-Layer für FadeInOut gefunden.");
    }

    var slice = getBatchSlice(totalClips, job.batchStartIndex, job.batchSize);
    var processedCount = 0;
    var skippedCount = 0;
    var aborted = false;

    safeBeginUndoGroup("AE Orchestrator: Apply FadeInOut");
    try {
        for (var idx = slice.startIndex; idx < slice.endIndex; idx++) {
            if (isAbortRequested()) {
                aborted = true;
                break;
            }

            var clipLayer = targetLayers[idx];
            var existingNum = extractClipNumber(clipLayer.comment);
            var clipNum = (existingNum !== null) ? existingNum : (idx + 1);

            writeHeartbeat(jobId, idx + 1, totalClips, "FadeInOut Clip " + clipNum + " / " + totalClips + ": " + clipLayer.name);

            if (hasCommentTag(clipLayer, "[DONE_PIPELINE_1]") ||
                hasCommentTag(clipLayer, "[DONE_ZOOM_GROUP]") ||
                hasCommentTag(clipLayer, "[DONE_FADE]")) {
                skippedCount++;
                continue;
            }

            var opProp = null;
            try {
                var trGroup = clipLayer.property("ADBE Transform Group");
                if (trGroup) {
                    opProp = trGroup.property("ADBE Opacity");
                }
            } catch (eOp) {
                opProp = null;
            }
            if (opProp && opProp.numKeys >= 2) {
                appendCommentTag(clipLayer, "[DONE_FADE]");
                skippedCount++;
                continue;
            }

            var clipIn = clipLayer.inPoint;
            var clipOut = clipLayer.outPoint;

            applyPresetAtTime(comp, clipLayer, fadePreset, clipIn);
            var animatedProps = [];
            collectAnimatedProperties(clipLayer, animatedProps);
            stretchKeyframesToSpan(animatedProps, clipIn, clipOut);

            appendCommentTag(clipLayer, "[DONE_FADE]");
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
        return buildAbortedResult(jobId, comp, "FadeInOut sauber abgebrochen.");
    }

    return buildOkResult(jobId, comp, {
        totalClips: totalClips,
        processedInBatch: processedCount,
        skippedInBatch: skippedCount,
        hasMoreBatches: slice.hasMoreBatches,
        nextBatchIndex: slice.nextBatchIndex,
        message: "FadeInOut Batch abgeschlossen: " + processedCount + " verarbeitet, " + skippedCount + " übersprungen."
    });
}
