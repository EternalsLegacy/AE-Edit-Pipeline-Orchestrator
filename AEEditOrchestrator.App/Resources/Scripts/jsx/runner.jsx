/*
 * runner.jsx
 * IPC entrypoint executed via AfterFX.exe -r "<path>\runner.jsx".
 * Supports single-job mode and persistent session mode via session_keepalive.flag
 * so sequential pipeline steps and batches execute within one ExtendScript host.
 */

#include "_lib_core.jsxinc"

(function () {
    var SESSION_POLL_MS = 50;
    var SESSION_IDLE_TIMEOUT_MS = 120000;
    var suppressDialogsActive = false;

    function resetSuppressDialogs() {
        if (suppressDialogsActive) {
            try {
                app.endSuppressDialogs(false);
            } catch (eSupEnd) {}
            suppressDialogsActive = false;
        }
    }

    function executeSingleJob(job, runnerDir) {
        var jobId = (job && job.jobId) ? String(job.jobId) : "";
        var cleanupDone = false;

        function performJobCleanup() {
            if (cleanupDone) {
                return;
            }
            cleanupDone = true;
            resetSuppressDialogs();
            closeAllUndoGroups();
        }

        try {
            var scriptFile = (job && job.scriptFile) ? trimStr(job.scriptFile) : "";
            if (scriptFile === "") {
                throw new Error("job.scriptFile ist leer.");
            }

            // Silent startup acknowledgement so C# knows AE accepted and started the job.
            writeHeartbeat(jobId, 0, 0, "");

            if (scriptFile === "__save_project__") {
                if (!app.project) {
                    throw new Error("Kein After Effects-Projekt geöffnet.");
                }
                if (app.project.file === null) {
                    throw new Error("Projekt wurde noch nie gespeichert und hat keinen Dateipfad.");
                }
                app.project.save();
                var saveRes = buildOkResult(jobId, getSafeActiveComp(), {
                    message: "Projekt gespeichert."
                });
                performJobCleanup();
                writeResultJson(saveRes);
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
                resetSuppressDialogs();
                var openRes = buildOkResult(jobId, getSafeActiveComp(), {
                    message: "Projekt geöffnet: " + openFile.fsName
                });
                performJobCleanup();
                writeResultJson(openRes);
                return;
            }

            if (scriptFile === "__close_no_save__") {
                if (app.project) {
                    app.project.close(CloseOptions.DO_NOT_SAVE_CHANGES);
                }
                var closeRes = buildOkResult(jobId, null, {
                    projectPath: "",
                    compositionName: "",
                    message: "Projekt ohne Speichern geschlossen."
                });
                performJobCleanup();
                writeResultJson(closeRes);
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
                if (isAbortRequested()) {
                    result = buildAbortedResult(jobId, getSafeActiveComp(), "Abbruch angefordert nach Batch-Verarbeitung.");
                } else {
                    var completedCount = (result.processedInBatch || 0) + (result.skippedInBatch || 0);
                    var totalClipCount = result.totalClips || 0;
                    if (job.purgeAfterBatch) {
                        writeHeartbeat(jobId, completedCount, totalClipCount, "Leere AE-Cache nach Batch...");
                        try {
                            app.purge(PurgeTarget.ALL_CACHES);
                        } catch (ePurge) {}
                    }
                    if (job.saveAfterBatch && app.project && app.project.file !== null) {
                        writeHeartbeat(jobId, completedCount, totalClipCount, "Speichere AE-Projekt nach Batch...");
                        app.project.save();
                    }
                }
            }

            try {
                $.gc();
            } catch (eGc) {}

            performJobCleanup();
            writeResultJson(result);
        } catch (err) {
            performJobCleanup();
            var errResult = buildErrorResult(jobId, err, "Fehler im Runner");
            try {
                writeResultJson(errResult);
            } catch (eWrite) {}
        } finally {
            if (!cleanupDone) {
                performJobCleanup();
            }
        }
    }

    var lastJobId = "";
    try {
        var runnerFile = new File($.fileName);
        var runnerDir = runnerFile.parent;

        var initialJob = readJobJson();
        if (initialJob && initialJob.jobId) {
            lastJobId = String(initialJob.jobId);
        }
        executeSingleJob(initialJob, runnerDir);

        var idleStartedAt = (new Date()).getTime();
        while (isSessionKeepAliveActive()) {
            var nowMs = (new Date()).getTime();
            if ((nowMs - idleStartedAt) > SESSION_IDLE_TIMEOUT_MS) {
                break;
            }

            var nextJob = tryReadJobJsonSafe();
            if (nextJob && nextJob.jobId) {
                var nextJobId = String(nextJob.jobId);
                if (nextJobId !== "" && nextJobId !== lastJobId) {
                    lastJobId = nextJobId;
                    executeSingleJob(nextJob, runnerDir);
                    idleStartedAt = (new Date()).getTime();
                    continue;
                }
            }

            $.sleep(SESSION_POLL_MS);
        }
    } catch (fatalErr) {
        try {
            resetSuppressDialogs();
            closeAllUndoGroups();
            writeResultJson(buildErrorResult(lastJobId, fatalErr, "Fehler in Runner-Session"));
        } catch (eFatalWrite) {}
    } finally {
        resetSuppressDialogs();
        closeAllUndoGroups();
        writeSessionEndedFlag();
    }
})();
