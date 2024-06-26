namespace PerryRhodan.AudiobookPlayer


open PerryRhodan.AudiobookPlayer.Views
open PerryRhodan.AudiobookPlayer.ViewModels
open ReactiveElmish.Avalonia
open Microsoft.Extensions.DependencyInjection

type AppCompositionRoot() =
    inherit CompositionRoot()

    let mainView = MainView()

    override this.RegisterServices services = 
        base.RegisterServices(services)                        // Auto-registers view models
            //.AddSingleton<>()  // Add any additional services

    override this.RegisterViews() = 
        Map [
            VM.Key<MainViewModel>(), View.Singleton(mainView)
            VM.Key<HomeViewModel>(), View.Transient<HomeView>()
            VM.Key<PlayerViewModel>(), View.Transient<PlayerView>()
            VM.Key<AudioBookItemViewModel>(), View.Transient<AudioBookItemView>()
            (*VM.Key<CounterViewModel>(), View.Singleton<CounterView>()
            VM.Key<AboutViewModel>(), View.Singleton<AboutView>()
            VM.Key<ChartViewModel>(), View.Singleton<ChartView>()
            VM.Key<FilePickerViewModel>(), View.Singleton<FilePickerView>()*)
        ]

