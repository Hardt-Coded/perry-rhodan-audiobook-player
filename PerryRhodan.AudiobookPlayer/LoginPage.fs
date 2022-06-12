module LoginPage

open Fabulous
open Fabulous.XamarinForms
open Xamarin.Forms
open Xamarin
open System.Net
open Common
open Common.Extensions
open Controls
open Services
open Global


    
    type Model = 
      { Username:string
        Password:string
        LoginFailed:bool
        RememberLogin:bool 
        IsLoading:bool
        CameFrom: LoginRequestCameFrom }

    type Msg = 
        | TryLogin 
        | LoginFailed
        | LoginSucceeded of Map<string,string>
        | ChangeRememberLogin of bool
        | ChangeUsername of string
        | ChangePassword of string
        | SetStoredCredentials of username:string * password:string * rememberLogin:bool
        | ChangeBusyState of bool
        | ShowErrorMessage of string
        | DoNothing
    
    type ExternalMsg = 
        | GotoForwardToBrowsing of Map<string,string> * LoginRequestCameFrom


    let initModel cameFrom = { Username = ""; Password = ""; LoginFailed = false; RememberLogin = false; IsLoading = false; CameFrom = cameFrom }


    let loadStoredCredentials = 
        async {
            
            let! res = SecureLoginStorage.loadLoginCredentials ()
            match res with
            | Error e -> 
                System.Diagnostics.Debug.WriteLine("Error loading cred: " + e)
                return DoNothing
            | Ok (username,password,rl) ->
                match username,password with
                | Some usr, Some pw ->
                    return SetStoredCredentials (usr,pw,rl)
                | _, _ ->
                    return SetStoredCredentials ("","",rl)
                    
                
        } |> Cmd.ofAsyncMsg

    let init cameFrom = initModel cameFrom, loadStoredCredentials
    

    let login model =        
        async {
            let! cc = WebAccess.login model.Username model.Password
            match cc with
            | Ok cc ->
                match cc with
                | None -> return LoginFailed
                | Some c -> return LoginSucceeded c
            | Error e ->
                match e with
                | SessionExpired e -> return (ShowErrorMessage e)
                | Other e -> return (ShowErrorMessage e)
                | Exception e ->
                    let ex = e.GetBaseException()
                    let msg = ex.Message + "|" + ex.StackTrace
                    return (ShowErrorMessage msg)
                | Network msg ->
                    return (ShowErrorMessage msg)
        } |> Cmd.ofAsyncMsg
        
    
    let storeCredentials model =
        async {
            let! sRes = (SecureLoginStorage.saveLoginCredentials model.Username model.Password model.RememberLogin)
            match sRes with
            | Error e -> 
                System.Diagnostics.Debug.WriteLine("credential store failed:" + e)
                return DoNothing
            | Ok _ -> return DoNothing
        } |> Cmd.ofAsyncMsg

    let rec update msg (model:Model) =
        match msg with
        | TryLogin ->            
            model |> onTryLoginMsg
        | LoginSucceeded cc ->
            model |> onLoginSucceededMsg cc
        | LoginFailed ->
            model |> onLoginFailedMsg
        | ChangeRememberLogin b ->
            model |> onChangeRememberLoginMsg b
        | ChangeUsername u ->
            model |> onChangeUsernameMsg u
        | ChangePassword p ->
            model |> onChangePasswordMsg p
        | SetStoredCredentials (u,p,r) ->
            model |> onSetStoredCredentialsMsg (u,p,r)
        | ChangeBusyState state -> 
            model |> onChangeBusyStateMsg state
        | ShowErrorMessage e ->
            model |> onShowErrorMessageMsg e
        | DoNothing -> 
            model,  Cmd.none, None
     
    
    and onTryLoginMsg model =
        match model.Username,model.Password with
        | "", "" ->
            model, Cmd.ofMsg (ShowErrorMessage Translations.current.MissingLoginCredentials), None
        | _, _ ->
            model, Cmd.batch [(login model);Cmd.ofMsg (ChangeBusyState true)], None
    

    and onLoginSucceededMsg cc model =
        let cmd = if model.RememberLogin then (storeCredentials model) else Cmd.none
        model, Cmd.batch [cmd;Cmd.ofMsg (ChangeBusyState false)], Some (GotoForwardToBrowsing (cc,model.CameFrom))
    

    and onLoginFailedMsg model =
        {model with LoginFailed = true}, Cmd.ofMsg (ChangeBusyState false), None


    and onChangeRememberLoginMsg b model =
        {model with RememberLogin = b}, Cmd.none, None


    and onChangeUsernameMsg u model =
        {model with Username = u}, Cmd.none, None


    and onChangePasswordMsg p model =
        {model with Password = p}, Cmd.none, None


    and onSetStoredCredentialsMsg (u,p,r) model  =
        {model with Username= u; Password = p; RememberLogin = r}, Cmd.none, None


    and onChangeBusyStateMsg state model =
        {model with IsLoading = state}, Cmd.none, None


    and onShowErrorMessageMsg e model =
        Common.Helpers.displayAlert(Translations.current.Error,e,"OK") |> Async.StartImmediate
        model, Cmd.ofMsg (ChangeBusyState false), None

    
    
    
    
    
    
    let view (model: Model) dispatch =
        View.ContentPage(
          title=Translations.current.LoginPage,useSafeArea=true,
          backgroundColor = Consts.backgroundColor,
          tag="loginpage",
          automationId="loginPage",
          content = View.Grid(
                children = [
                    yield View.StackLayout(padding = Thickness 10., 
                        verticalOptions = LayoutOptions.Center,
                        children = [ 
                            yield View.Label(text=Translations.current.LoginToEinsAMedienAccount
                                , horizontalOptions = LayoutOptions.Center                                
                                , horizontalTextAlignment=TextAlignment.Center
                                , textColor = Consts.primaryTextColor
                                , backgroundColor = Consts.cardColor
                                , fontSize=FontSize.fromValue 16.)
                            // TextChange Event cause actually a invite look, the debouncer doen't help
                            // Move to complete and lost focus event
                            yield View.Entry(text = model.Username
                                , placeholder = Translations.current.Username
                                , textColor = Consts.primaryTextColor
                                , backgroundColor = Consts.backgroundColor
                                , placeholderColor = Consts.secondaryTextColor                                
                                , keyboard=Keyboard.Email
                                , styleId = "login_username"
                                , completed = (fun t  -> if t <> model.Username then dispatch (ChangeUsername t))
                                , created = (fun e -> e.Unfocused.Add(fun args -> if model.Username<>e.Text then dispatch (ChangeUsername e.Text)))
                                )
                            yield View.Entry(text = model.Password
                                , placeholder = Translations.current.Password
                                , textColor = Consts.primaryTextColor
                                , backgroundColor = Consts.backgroundColor
                                , placeholderColor = Consts.secondaryTextColor    
                                , styleId = "login_password"
                                , isPassword = true
                                , keyboard=Keyboard.Default, completed =(fun t  -> if t <> model.Password then dispatch (ChangePassword t))
                                , created = (fun e -> e.Unfocused.Add(fun args -> if model.Password<>e.Text then dispatch (ChangePassword e.Text)))
                                )
                
                            yield View.StackLayout(orientation=StackOrientation.Horizontal,
                                horizontalOptions = LayoutOptions.Center,
                                children =[
                                    Controls.secondaryTextColorLabel 16. Translations.current.RememberLogin
                                    View.Switch(isToggled = model.RememberLogin, toggled = (fun on -> dispatch (ChangeRememberLogin on.Value)), horizontalOptions = LayoutOptions.Center)
                                ]
                            )
                                
                            yield View.Button(text = Translations.current.Login, command = (fun () -> dispatch TryLogin), horizontalOptions = LayoutOptions.Center)
                            //yield View.Button(text = "Abbrechen", command = (fun () -> dispatch Cancel), horizontalOptions = LayoutOptions.Center)
                            if model.LoginFailed then
                                yield View.Label(text=Translations.current.LoginFailed, textColor = Color.Red, horizontalOptions = LayoutOptions.Center, width=200., horizontalTextAlignment=TextAlignment.Center,fontSize=FontSize.fromValue 20.)

                            ]    
                        )
                    if model.IsLoading then 
                        yield Common.createBusyLayer()
                    ]
            )
            
          )
        
                

