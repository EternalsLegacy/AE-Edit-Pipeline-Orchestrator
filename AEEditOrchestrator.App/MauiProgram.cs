using AEEditOrchestrator.App.Services;
using AEEditOrchestrator.App.ViewModels;
using AEEditOrchestrator.App.Views;
using AEEditOrchestrator.Core.Engine;
using AEEditOrchestrator.Core.Interfaces;
using AEEditOrchestrator.Core.Steps;
using AEEditOrchestrator.Infrastructure.AfterEffects;
using AEEditOrchestrator.Infrastructure.Persistence;
using AEEditOrchestrator.Infrastructure.Transactions;
using Microsoft.Extensions.Logging;

namespace AEEditOrchestrator.App;

public static class MauiProgram
{
    #region Public Methods

    public static MauiApp CreateMauiApp()
    {
        EmbeddedScriptExtractor ScriptExtractor = new EmbeddedScriptExtractor();
        string ExtractedScriptsDirectory = ScriptExtractor.ExtractAllScripts();

        MauiAppBuilder Builder = MauiApp.CreateBuilder();
        Builder
            .UseMauiApp<App>()
            .ConfigureFonts(Fonts =>
            {
                Fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                Fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        Builder.Logging.AddDebug();
#endif

        // Infrastructure
        Builder.Services.AddSingleton<IpcPaths>(ServiceProvider => new IpcPaths(null, ExtractedScriptsDirectory));
        Builder.Services.AddSingleton<IAfterEffectsBridge, AfterEffectsCliBridge>();
        Builder.Services.AddSingleton<ITransactionManager, FileSnapshotTransactionManager>();
        Builder.Services.AddSingleton<ISettingsStore, JsonSettingsStore>();

        // Core Pipeline Steps
        Builder.Services.AddSingleton<IPipelineStep, PreflightCheckStep>();
        Builder.Services.AddSingleton<IPipelineStep, BeatDetectionStep>();
        Builder.Services.AddSingleton<IPipelineStep, PrecomposeClipsStep>();
        Builder.Services.AddSingleton<IPipelineStep, ApplyFadeInOutStep>();
        Builder.Services.AddSingleton<IPipelineStep, ApplyMotionTileAndShakesStep>();
        Builder.Services.AddSingleton<IPipelineStep, CreateStartBlursStep>();
        Builder.Services.AddSingleton<IPipelineStep, CreateEndBlursStep>();
        Builder.Services.AddSingleton<IPipelineStep, GroupClipsForZoomStep>();
        Builder.Services.AddSingleton<IPipelineStep, ApplyBlurZoomStep>();
        Builder.Services.AddSingleton<IPipelineStep, ReverseRollbackStep>();

        // Core Engine
        Builder.Services.AddSingleton<StepRegistry>();
        Builder.Services.AddSingleton<StepDependencyValidator>();
        Builder.Services.AddSingleton<PipelineOrchestrator>();

        // Shared App State & ViewModels
        Builder.Services.AddSingleton<OrchestratorAppState>();
        Builder.Services.AddSingleton<PipelineViewModel>();
        Builder.Services.AddSingleton<SettingsViewModel>();
        Builder.Services.AddSingleton<RecoveryViewModel>();

        // Views
        Builder.Services.AddTransient<PipelinePage>();
        Builder.Services.AddTransient<SettingsPage>();
        Builder.Services.AddTransient<RecoveryPage>();

        return Builder.Build();
    }

    #endregion
}
