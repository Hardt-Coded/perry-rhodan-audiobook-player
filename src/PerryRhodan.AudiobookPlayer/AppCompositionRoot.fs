namespace PerryRhodan.AudiobookPlayer


open Dependencies
open PerryRhodan.AudiobookPlayer.Controls
open PerryRhodan.AudiobookPlayer.Services
open PerryRhodan.AudiobookPlayer.Services.AudioPlayer
open PerryRhodan.AudiobookPlayer.Services
open PerryRhodan.AudiobookPlayer.Services.DataBaseCommon
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open PerryRhodan.AudiobookPlayer.ViewModel
open PerryRhodan.AudiobookPlayer.Views
open PerryRhodan.AudiobookPlayer.ViewModels
open ReactiveElmish.Avalonia
open Services.DependencyServices




type ViewModelCompositionRoot() =
    inherit CompositionRoot()


    override this.RegisterServices services =
        base.RegisterServices(services)                        // Auto-registers view models
            

    override this.RegisterViews() =
        Map [
            VM.Key<HomeViewModel>(), View.Singleton<HomeView>()
            VM.Key<SettingsViewModel>(), View.Singleton<SettingsView>()
        ]


module AppCompositionRoot =
    
    open Microsoft.Extensions.DependencyInjection
    
    let registerDatabaseAccess (serviceCollection:IServiceCollection) =
        serviceCollection
            .AddSingleton<IOldShopDatabase>(fun sp -> DatabaseProcessor(DataBaseCommon.OldShopDatabaseCollection) :> IOldShopDatabase)
            .AddSingleton<INewShopDatabase>(fun sp -> DatabaseProcessor(DataBaseCommon.NewShopDatabaseCollection) :> INewShopDatabase)
        |> ignore
    
    let private registerViewModels (serviceCollection:IServiceCollection) =
        serviceCollection
            .AddSingleton<ILoginViewModel, LoginViewModel>(fun sp -> LoginViewModel(Domain.Shop.NewShop))
            .AddSingleton<MainViewModel>(fun _ -> new MainViewModel(ViewModelCompositionRoot()))
            .AddSingleton<IMainViewModel>(fun sp -> sp.GetRequiredService<MainViewModel>() :> IMainViewModel)
            .AddSingleton<IMainViewAccessService>(fun sp ->
                {
                    new IMainViewAccessService with
                        member this.GetMainViewModel() = sp.GetRequiredService<IMainViewModel>()
                }
            )
            
            
    let registerServices additionalRegistrations =
        DependencyService.ServiceCollection
            .AddSingleton<INavigationService, NavigationService>()
            .AddSingleton<AudioPlayerService, AudioPlayerService>()
            .AddSingleton<IAudioPlayer>(fun sp -> sp.GetRequiredService<AudioPlayerService>() :> IAudioPlayer)
            .AddSingleton<IAudioPlayerPause>(fun sp -> sp.GetRequiredService<AudioPlayerService>() :> IAudioPlayerPause)
            .AddSingleton<IActionMenuService, ActionMenuService>()
            .AddSingleton<GlobalSettingsService>(GlobalSettingsService())
            
        |> ignore
        
        registerDatabaseAccess DependencyService.ServiceCollection |> ignore
        
        registerViewModels DependencyService.ServiceCollection |> ignore
            
        additionalRegistrations DependencyService.ServiceCollection
        
        DependencyService.SetComplete()
        
        
        
        
        
    
            