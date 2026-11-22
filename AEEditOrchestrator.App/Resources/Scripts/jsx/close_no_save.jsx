/*
 * close_no_save.jsx
 * Standalone mini-script used during rollback to close the current AE project
 * without saving changes so the .aep file lock is released.
 */

#include "_lib_core.jsxinc"

(function () {
    var jobId = "";
    try {
        var job = readJobJson();
        if (job && job.jobId) {
            jobId = String(job.jobId);
        }
    } catch (eJob) {
        jobId = "";
    }

    try {
        closeAllUndoGroups();
        if (app.project) {
            app.project.close(CloseOptions.DO_NOT_SAVE_CHANGES);
        }
        writeResultJson(buildOkResult(jobId, null, {
            projectPath: "",
            compositionName: "",
            message: "Projekt ohne Speichern geschlossen."
        }));
    } catch (err) {
        var errRes = buildErrorResult(jobId, err, "Fehler beim Schließen des Projekts ohne Speichern");
        try {
            writeResultJson(errRes);
        } catch (eWrite) {}
    }
})();
