/*
 * runner.jsx
 * IPC entrypoint executed via AfterFX.exe -r "<path>\runner.jsx".
 * Dispatches special project actions or loads and runs an atomic step script.
 */

#include "_lib_core.jsxinc"

(function () {
    var jobId = "";
    var job = null;
    var suppressDialogsActive = false;

    try {
        job = readJobJson();
        if (job && job.jobId) {
            jobId = String(job.jobId);
        }

        var scriptFile = (job && job.scriptFile) ? trimStr(job.scriptFile) : "";
        if (scriptFile === "") {
            throw new Error("job.scriptFile ist leer.");
        }

        var runnerFile = new File($.fileName);
        var runnerDir = runnerFile.parent;

        if (scriptFile === "__save_project__") {
            if (!app.project) {
                throw new Error("Kein After Effects-Projekt geöffnet.");
            }
            if (app.project.file === null) {
                throw new Error("Projekt wurde noch nie gespeichert und hat keinen Dateipfad.");
            }
            app.project.save();
            writeResultJson(buildOkResult(jobId, getSafeActiveComp(), {
                message: "Projekt gespeichert."
            }));
            return;
        }

        if (scriptFile === "__open_project__") {
            var projectToOpen = (job.settings && job.settings.projectToOpen) ? trimStr(job.settings.projectToOpen) : "";
            if (projectToOpen === "") {
                throw new Error("Kein Projektpfad in settings.projectToOpen angegeben.");
            }
            var openFile = new File(projectToOpen);
            if (!openFile.exists) {
                throw new Error("Projektdatei nicht gefunden: " + projectToOpen);
            }
            try {
                app.beginSuppressDialogs();
                suppressDialogsActive = true;
            } catch (eSup1) {}
            try {
                if (app.project) {
                    app.project.close(CloseOptions.DO_NOT_SAVE_CHANGES);
                }
            } catch (eClosePreOpen) {}
            app.open(openFile);
            if (suppressDialogsActive) {
                try {
                    app.endSuppressDialogs(false);
                } catch (eSup2) {}
                suppressDialogsActive = false;
            }
            writeResultJson(buildOkResult(jobId, getSafeActiveComp(), {
                message: "Projekt geöffnet: " + openFile.fsName
            }));
            return;
        }

        if (scriptFile === "__close_no_save__") {
            if (app.project) {
                app.project.close(CloseOptions.DO_NOT_SAVE_CHANGES);
            }
            writeResultJson(buildOkResult(jobId, null, {
                projectPath: "",
                compositionName: "",
                message: "Projekt ohne Speichern geschlossen."
            }));
            return;
        }

        var targetFile = new File(runnerDir.fsName + "/" + scriptFile);
        if (!targetFile.exists) {
            throw new Error("Step-Script nicht gefunden: " + targetFile.fsName);
        }

        if (typeof $ !== "undefined" && $.global) {
            $.global.runStep = null;
        }

        $.evalFile(targetFile);

        var stepFn = null;
        if (typeof $ !== "undefined" && $.global && typeof $.global.runStep === "function") {
            stepFn = $.global.runStep;
        } else if (typeof runStep === "function") {
            stepFn = runStep;
        }

        if (!stepFn) {
            throw new Error("Ziel-Script definiert keine Funktion runStep(job): " + scriptFile);
        }

        var result = null;
        if (isAbortRequested()) {
            result = buildAbortedResult(jobId, getSafeActiveComp(), "Abbruch angefordert vor Ausführung von " + scriptFile + ".");
        } else {
            result = stepFn(job);
        }
        closeAllUndoGroups();

        if (!result) {
            result = buildOkResult(jobId, getSafeActiveComp(), {
                message: scriptFile + " abgeschlossen."
            });
        }
        if (!result.jobId) {
            result.jobId = jobId;
        }

        if (result.status === "ok") {
            if (job.purgeAfterBatch) {
                try {
                    app.purge(PurgeTarget.ALL_CACHES);
                } catch (ePurge) {}
            }
            if (job.saveAfterBatch && app.project && app.project.file !== null) {
                app.project.save();
            }
        }

        writeResultJson(result);
    } catch (err) {
        closeAllUndoGroups();
        var errResult = buildErrorResult(jobId, err, "Fehler im Runner");
        try {
            writeResultJson(errResult);
        } catch (eWrite) {}
    } finally {
        if (suppressDialogsActive) {
            try {
                app.endSuppressDialogs(false);
            } catch (eSupEnd) {}
        }
        closeAllUndoGroups();
    }
})();
