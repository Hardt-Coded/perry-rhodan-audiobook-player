namespace PerryRhodan.AudiobookPlayer


open PerryRhodan.AudiobookPlayer.Views
open PerryRhodan.AudiobookPlayer.ViewModels
open ReactiveElmish.Avalonia




type AppCompositionRoot() =
    inherit CompositionRoot()


    override this.RegisterServices services =
        base.RegisterServices(services)                        // Auto-registers view models
            //.AddSingleton<>()  // Add any additional services

    override this.RegisterViews() =
        Map [
            VM.Key<HomeViewModel>(), View.Singleton<HomeView>()
            VM.Key<SettingsViewModel>(), View.Singleton<SettingsView>()
        ]

