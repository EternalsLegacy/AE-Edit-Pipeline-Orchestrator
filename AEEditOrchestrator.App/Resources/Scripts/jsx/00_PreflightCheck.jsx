/*
 * 00_PreflightCheck.jsx
 * Validates AE project readiness, active composition, saved state,
 * required presets, clip count, and existing pipeline tags.
 */

function runStep(job) {
    var jobId = (job && job.jobId) ? String(job.jobId) : "";
    var settings = (job && job.settings) ? job.settings : {};

    writeHeartbeat(jobId, 0, 1, "Preflight-Prüfung gestartet...");

    var comp = getSafeActiveComp();
    var state = detectProjectState(comp, settings, job ? job.requiredPresets : null);
    var totalClips = 0;

    if (state.hasActiveComp) {
        var eligible = collectSortedEligibleLayers(
            comp,
            Boolean(job && job.useSelectedOnly),
            settings.excludedNames,
            false
        );
        totalClips = eligible.length;
    }

    var msg = "";
    if (!state.hasActiveComp) {
        msg = "Keine aktive Komposition geöffnet.";
    } else if (!state.isProjectSaved) {
        msg = "Projekt wurde noch nie gespeichert (.aep-Pfad fehlt).";
    } else if (state.missingPresets.length > 0) {
        msg = "Fehlende Presets: " + state.missingPresets.join(", ");
    } else {
        msg = "Preflight OK: " + totalClips + " gültige Clips in '" + comp.name + "'.";
    }

    writeHeartbeat(jobId, 1, 1, msg);

    return buildOkResult(jobId, comp, {
        totalClips: totalClips,
        processedInBatch: totalClips,
        skippedInBatch: 0,
        hasMoreBatches: false,
        nextBatchIndex: totalClips,
        detectedState: state,
        message: msg
    });
}
