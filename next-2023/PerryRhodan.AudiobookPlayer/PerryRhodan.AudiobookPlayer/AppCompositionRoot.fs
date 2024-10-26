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
            VM.Key<MainViewModel>(), View.Singleton<MainView>()
            VM.Key<HomeViewModel>(), View.Transient<HomeView>()
            VM.Key<BrowserViewModel>(), View.Transient<BrowserView>()
            VM.Key<SettingsViewModel>(), View.Transient<SettingsView>()
        ]

