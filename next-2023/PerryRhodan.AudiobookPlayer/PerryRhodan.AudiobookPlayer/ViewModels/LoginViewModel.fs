namespace PerryRhodan.AudiobookPlayer.ViewModels

open Avalonia.Controls
open CherylUI.Controls
open Dependencies
open DialogHostAvalonia
open Global
open PerryRhodan.AudiobookPlayer.Views
open ReactiveElmish
open Services
open Services.DependencyServices
open Services.Helpers


module LoginPage =
    type State = {
        Username:string
        Password:string
        LoginFailed:bool
        RememberLogin:bool 
        IsLoading:bool
    }

    type Msg = 
        | TryLogin 
        | LoginFailed
        | LoginSucceeded of Map<string,string>
        | ChangeRememberLogin of bool
        | ChangeUsername of string
        | ChangePassword of string
        | SetStoredCredentials of username:string * password:string * rememberLogin:bool
        | ChangeBusyState of bool
        | Cancel
        | KeyboardStateChanged

    
    type ExternalMsg = 
        | GotoForwardToBrowsing of Map<string,string> * LoginRequestCameFrom
        
    
    [<RequireQualifiedAccess>]
    type SideEffect =
        | None
        | TryLogin
        | LoadStoredCredentials
        | StoreCredentials
        | ShowErrorMessage of string
        | CloseDialog


    let init () =
        { Username = ""; Password = ""; LoginFailed = false; RememberLogin = false; IsLoading = false }, SideEffect.LoadStoredCredentials


    let update msg (model:State) =
        match msg with
        | TryLogin ->            
            match model.Username,model.Password with
            | "", "" ->
                model, SideEffect.ShowErrorMessage Translations.current.MissingLoginCredentials
            | _, _ ->
                model, SideEffect.TryLogin
                
        | LoginSucceeded cc ->
            let cmd =
                if model.RememberLogin then
                    SideEffect.StoreCredentials
                else
                    SideEffect.None
            model, cmd // Todo: Close Form
        
        | LoginFailed ->
            {model with LoginFailed = true}, SideEffect.None
            
        | ChangeRememberLogin b ->
            {model with RememberLogin = b}, SideEffect.None
            
        | ChangeUsername u ->
            {model with Username = u}, SideEffect.None
            
        | ChangePassword p ->
            {model with Password = p}, SideEffect.None
            
        | SetStoredCredentials (u,p,r) ->
            {model with Username= u; Password = p; RememberLogin = r}, SideEffect.None
            
        | ChangeBusyState state -> 
            {model with IsLoading = state}, SideEffect.None
        | Cancel -> model, SideEffect.CloseDialog
        | KeyboardStateChanged -> model, SideEffect.None
            
    

    module SideEffects =
        open Common
        
        let runSideEffects (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) =
            task {
                match sideEffect with
                | SideEffect.None ->
                    return ()
                    
                | SideEffect.TryLogin ->
                    try
                        let! cc = WebAccess.login state.Username state.Password
                        match cc with
                        | Ok cc ->
                            match cc with
                            | None -> dispatch LoginFailed
                            | Some c -> dispatch (LoginSucceeded c)
                        | Error e ->
                            match e with
                            | SessionExpired e ->
                                let! _ = DialogHost.Show(MessageBoxViewModel("Achtung!", e))
                                ()
                            | Other e ->
                                let! _ = DialogHost.Show(MessageBoxViewModel("Achtung!", e))
                                ()
                            | Exception e ->
                                let ex = e.GetBaseException()
                                let msg = ex.Message + "|" + ex.StackTrace
                                let! _ = DialogHost.Show(MessageBoxViewModel("Achtung!", msg))
                                ()
                            | Network msg ->
                                let! _ = DialogHost.Show(MessageBoxViewModel("Achtung!", msg))
                                ()
                    with
                    | ex ->
                        let! _ = DialogHost.Show(MessageBoxViewModel("Achtung!", ex.Message))
                        return ()
                   
                | SideEffect.LoadStoredCredentials ->
                        let! res = SecureLoginStorage.loadLoginCredentials ()
                        match res with
                        | Error e -> 
                            System.Diagnostics.Debug.WriteLine("Error loading cred: " + e)
                            let! _ = DialogHost.Show(MessageBoxViewModel("Achtung!", e))
                            return ()
                        | Ok (username,password,rl) ->
                            match username,password with
                            | Some usr, Some pw ->
                                dispatch <| SetStoredCredentials (usr,pw,rl)
                            | _, _ ->
                                dispatch <| SetStoredCredentials ("","",rl)
                | SideEffect.StoreCredentials ->
                    let! res = SecureLoginStorage.saveLoginCredentials state.Username state.Password state.RememberLogin
                    match res with
                    | Error e -> 
                        System.Diagnostics.Debug.WriteLine("Error storing cred: " + e)
                    | Ok _ -> ()
                | SideEffect.ShowErrorMessage s ->
                    let! _ = DialogHost.Show(MessageBoxViewModel("Achtung!", s))
                    return ()
                | SideEffect.CloseDialog ->
                    InteractiveContainer.CloseDialog()
                    return ()
            }
            
 
 
open LoginPage
open ReactiveElmish.Avalonia
open ReactiveElmish
open Elmish.SideEffect
open System

type LoginViewModel() =
    inherit ReactiveElmishViewModel()
    
    let local =
        Program.mkAvaloniaProgrammWithSideEffect init update SideEffects.runSideEffects
        |> Program.mkStore
        
    do
        base.Subscribe(InputPaneService.InputPane.StateChanged, fun _ -> local.Dispatch KeyboardStateChanged)
    
            
    member this.Username = this.Bind(local, _.Username)        
    member this.Password = this.Bind(local, _.Password)        
    member this.RememberLogin = this.Bind(local, _.RememberLogin)        
    member this.TryLogin() = local.Dispatch (TryLogin)
    member this.Cancel() = local.Dispatch (Cancel)
    member this.ScreenWidth = this.Bind(local, fun _ ->
        let screenService = DependencyService.Get<IScreenService>()
        let screenSize = screenService.GetScreenSize()
        screenSize.Width
        )
    member this.InputPaneHeight = this.Bind(local, fun _ ->
        match InputPaneService.InputPane with
        | null -> 0.0
        | ip -> ip.OccludedRect.Height
        )
    static member DesignVM = new LoginViewModel()