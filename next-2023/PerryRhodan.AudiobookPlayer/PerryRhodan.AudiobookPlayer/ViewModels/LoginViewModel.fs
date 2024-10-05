namespace PerryRhodan.AudiobookPlayer.ViewModels

open CherylUI.Controls
open Dependencies
open Global
open ReactiveElmish.Avalonia
open ReactiveElmish
open Elmish.SideEffect
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
        | CloseDialog
        | KeyboardStateChanged

    
    type ExternalMsg = 
        | GotoForwardToBrowsing of Map<string,string> * LoginRequestCameFrom
        
    
    [<RequireQualifiedAccess>]
    type SideEffect =
        | None
        | TryLogin
        | LoadStoredCredentials
        | LoginSuccessfullAfterMath of rememberLogin:bool * cookie:Map<string,string>
        | ShowErrorMessage of string
        | CloseDialog


    let init () =
        { Username = ""; Password = ""; LoginFailed = false; RememberLogin = false; IsLoading = false }, SideEffect.LoadStoredCredentials


    let update msg (state:State) =
        match msg with
        | TryLogin ->            
            match state.Username,state.Password with
            | "", "" ->
                state, SideEffect.ShowErrorMessage Translations.current.MissingLoginCredentials
            | _, _ ->
                state, SideEffect.TryLogin
                
        | LoginSucceeded cc ->
            state, SideEffect.LoginSuccessfullAfterMath (state.RememberLogin, cc)
        
        | LoginFailed ->
            {state with LoginFailed = true}, SideEffect.None
            
        | ChangeRememberLogin b ->
            {state with RememberLogin = b}, SideEffect.None
            
        | ChangeUsername u ->
            {state with Username = u}, SideEffect.None
            
        | ChangePassword p ->
            {state with Password = p}, SideEffect.None
            
        | SetStoredCredentials (u,p,r) ->
            {state with Username= u; Password = p; RememberLogin = r}, SideEffect.None
            
        | ChangeBusyState busy -> 
            { state with IsLoading = busy}, SideEffect.None
            
        | CloseDialog ->
            state, SideEffect.CloseDialog
            
        | KeyboardStateChanged ->
            state, SideEffect.None
            
    

    module SideEffects =
        open Common
        
        
        let runSideEffects (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) =
            task {
                match sideEffect with
                | SideEffect.None ->
                    return ()
                    
                | SideEffect.TryLogin ->
                    dispatch <| ChangeBusyState true
                    try
                        let! cc = WebAccess.login state.Username state.Password
                        match cc with
                        | Ok cc ->
                            match cc with
                            | None ->
                                Notifications.showToasterMessage "Login fehlgeschlagen"
                                dispatch LoginFailed
                            | Some c ->
                                dispatch <| LoginSucceeded c
                        | Error e ->
                            match e with
                            | SessionExpired e ->
                                do! Notifications.showErrorMessage e
                                ()
                            | Other e ->
                                do! Notifications.showErrorMessage e
                                ()
                            | Exception e ->
                                let ex = e.GetBaseException()
                                let msg = ex.Message + "|" + ex.StackTrace
                                do! Notifications.showErrorMessage msg
                                ()
                            | Network msg ->
                                do! Notifications.showErrorMessage msg
                                ()
                                
                            dispatch <| ChangeBusyState false
                    with
                    | ex ->
                        do! Notifications.showErrorMessage ex.Message
                        dispatch <| ChangeBusyState false
                        return ()
                   
                | SideEffect.LoadStoredCredentials ->
                        dispatch <| ChangeBusyState true
                        let! res = SecureLoginStorage.loadLoginCredentials ()
                        match res with
                        | Error e -> 
                            System.Diagnostics.Debug.WriteLine("Error loading cred: " + e)
                            do! Notifications.showErrorMessage e
                            dispatch <| ChangeBusyState false
                            return ()
                        | Ok (username,password,rl) ->
                            match username,password with
                            | Some usr, Some pw ->
                                dispatch <| SetStoredCredentials (usr,pw,rl)
                            | _, _ ->
                                dispatch <| SetStoredCredentials ("","",rl)
                                
                        dispatch <| ChangeBusyState false
                        
                                
                | SideEffect.LoginSuccessfullAfterMath (rememberLogin, cookie) ->
                    dispatch <| ChangeBusyState true
                    
                    if rememberLogin then
                        let! res = SecureLoginStorage.saveLoginCredentials state.Username state.Password state.RememberLogin
                        match res with
                        | Error e ->
                            let msg = $"Error storing cred: {e}"
                            System.Diagnostics.Debug.WriteLine(msg)
                            do! Notifications.showErrorMessage msg
                        | Ok _ -> ()
                        
                    let! storeRes =  SecureLoginStorage.saveCookie cookie
                    match storeRes with
                    | Error e ->
                        let msg = $"Error storing cookie: {e}"
                        System.Diagnostics.Debug.WriteLine(msg)
                        do! Notifications.showErrorMessage msg
                    | Ok _ ->
                        Notifications.showToasterMessage "Login erfolgreich"
                        dispatch CloseDialog
                        
                    dispatch <| ChangeBusyState true
                                                                                

                        
                    
                | SideEffect.ShowErrorMessage s ->
                    do! Notifications.showErrorMessage s
                    return ()
                    
                | SideEffect.CloseDialog ->
                    InteractiveContainer.CloseDialog()
                    // Backbutton back to default
                    DependencyService.Get<INavigationService>().ResetBackbuttonPressed()
                    return ()
            }
            
 
 
open LoginPage

type LoginViewModel() =
    inherit ReactiveElmishViewModel()
    
    let local =
        Program.mkAvaloniaProgrammWithSideEffect init update SideEffects.runSideEffects
        |> Program.mkStore
        
    do
        base.Subscribe(InputPaneService.InputPane.StateChanged, fun _ -> local.Dispatch KeyboardStateChanged)
        let notificationService = DependencyService.Get<INavigationService>()
        notificationService.RegisterBackbuttonPressed (fun () -> local.Dispatch CloseDialog)
        ()
    
    interface ILoginViewModel
            
    member this.Username 
        with get() = this.Bind(local, _.Username)
        and set v = local.Dispatch (ChangeUsername v)
    member this.Password
        with get() = this.Bind(local, _.Password)
        and set v = local.Dispatch (ChangePassword v)
        
    member this.RememberLogin
        with get() = this.Bind(local, _.RememberLogin)
        and set v = local.Dispatch (ChangeRememberLogin v)
    
    member this.IsLoading = this.Bind(local, _.IsLoading)
        
    member this.TryLogin() = local.Dispatch TryLogin
    member this.Cancel() = local.Dispatch CloseDialog
    
    /// return the screen size for the login form dialog
    member this.DialogWidth = this.Bind(local, fun _ ->
        let screenService = DependencyService.Get<IScreenService>()
        let screenSize = screenService.GetScreenSize()
        let width = ((screenSize.Width |> float) / screenSize.ScaledDensity) |> int
        width
        )
    member this.InputPaneHeight = this.Bind(local, fun _ ->
        match InputPaneService.InputPane with
        | null -> 0.0
        | ip -> ip.OccludedRect.Height
        )
    static member DesignVM = new LoginViewModel()