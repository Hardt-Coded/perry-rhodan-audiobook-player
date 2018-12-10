module LoginPage

open Fabulous
open Fabulous.Core
open Fabulous.DynamicViews
open Xamarin.Forms
open Xamarin
open System.Net
open Common
open Common.Extensions
open Controls


    
    type Model = 
      { Username:string
        Password:string
        LoginFailed:bool
        RememberLogin:bool 
        IsLoading:bool }

    type Msg = 
        | TryLogin 
        | LoginFailed
        | LoginSucceeded of Map<string,string>
        | ChangeRememberLogin of bool
        | ChangeUsername of string
        | ChangePassword of string
        | SetStoredCredentials of username:string * password:string * rememberLogin:bool
        | ChangeBusyState of bool
        | DoNothing
    
    type ExternalMsg = 
        | GotoForwardToBrowsing of Map<string,string>

    let initModel () = { Username = ""; Password = ""; LoginFailed = false; RememberLogin = false; IsLoading = false }


    let loadStoredCredentials = 
        async {
            
            let! res = Services.loadLoginCredentials ()
            match res with
            | Error e -> 
                System.Diagnostics.Debug.WriteLine("Error loading cred: " + e)
                return DoNothing
            | Ok (username,password,rl) ->
                return SetStoredCredentials (username,password,rl)
        } |> Cmd.ofAsyncMsg

    let init cameFrom = initModel cameFrom, loadStoredCredentials
    

    let login model =        
        async {
            let! cc = Services.login model.Username model.Password
            match cc with
            | None -> return LoginFailed
            | Some c -> return LoginSucceeded c
        } |> Cmd.ofAsyncMsg
        
    
    let storeCredentials model =
        async {
            let! sRes = (Services.saveLoginCredentials model.Username model.Password model.RememberLogin)
            match sRes with
            | Error e -> 
                System.Diagnostics.Debug.WriteLine("credential store failed:" + e)
                return DoNothing
            | Ok _ -> return DoNothing
        } |> Cmd.ofAsyncMsg

    let update msg model =
        match msg with
        | TryLogin ->            
            model, Cmd.batch [(login model);Cmd.ofMsg (ChangeBusyState true)], None
        | LoginSucceeded cc ->
            let cmd = if model.RememberLogin then (storeCredentials model) else Cmd.none
            model, Cmd.batch [cmd;Cmd.ofMsg (ChangeBusyState false)], Some (GotoForwardToBrowsing cc)
        | LoginFailed ->
            {model with LoginFailed = true}, Cmd.ofMsg (ChangeBusyState false), None
        | ChangeRememberLogin b ->
            {model with RememberLogin = b}, Cmd.none, None
        | ChangeUsername u ->
            {model with Username = u}, Cmd.none, None
        | ChangePassword p ->
            {model with Password = p}, Cmd.none, None
        | SetStoredCredentials (u,p,r) ->
            {model with Username= u; Password = p; RememberLogin = r}, Cmd.none, None
        | ChangeBusyState state -> 
            {model with IsLoading = state}, Cmd.none, None
        | DoNothing -> model,  Cmd.none, None

    
    
    
    
    
    
    let view (model: Model) dispatch =
        View.ContentPage(
          title="Login",useSafeArea=true,
          backgroundColor = Consts.backgroundColor,
          content = View.Grid(
                children = [
                    yield View.StackLayout(padding = 10.0, 
                        verticalOptions = LayoutOptions.Center,
                        children = [ 
                            yield View.Label(text="Login to your EinsAMedien-Account"
                                , horizontalOptions = LayoutOptions.Center                                
                                , horizontalTextAlignment=TextAlignment.Center
                                , textColor = Consts.primaryTextColor
                                , backgroundColor = Consts.cardColor
                                , fontSize=16.0)
                            // TextChange Event cause actually a invite look, the debouncer doen't help
                            // Move to complete and lost focus event
                            yield View.Entry(text = model.Username
                                , placeholder = "Username"
                                , textColor = Consts.primaryTextColor
                                , backgroundColor = Consts.backgroundColor
                                , placeholderColor = Consts.secondaryTextColor                                
                                , keyboard=Keyboard.Email
                                , completed = (fun t  -> if t <> model.Username then dispatch (ChangeUsername t))
                                , created = (fun e -> e.Unfocused.Add(fun args -> if model.Username<>e.Text then dispatch (ChangeUsername e.Text)))
                                )
                            yield View.Entry(text = model.Password
                                , placeholder = "Password"
                                , textColor = Consts.primaryTextColor
                                , backgroundColor = Consts.backgroundColor
                                , placeholderColor = Consts.secondaryTextColor                                
                                , isPassword = true
                                , keyboard=Keyboard.Default, completed =(fun t  -> if t <> model.Password then dispatch (ChangePassword t))
                                , created = (fun e -> e.Unfocused.Add(fun args -> if model.Password<>e.Text then dispatch (ChangePassword e.Text)))
                                )
                
                            yield View.Switch(isToggled = model.RememberLogin, toggled = (fun on -> dispatch (ChangeRememberLogin on.Value)), horizontalOptions = LayoutOptions.Center)
                            yield View.Button(text = "Login", command = (fun () -> dispatch TryLogin), horizontalOptions = LayoutOptions.Center)
                            if model.LoginFailed then
                                yield View.Label(text="Login Failed !!!!", textColor = Color.Red, horizontalOptions = LayoutOptions.Center, widthRequest=200.0, horizontalTextAlignment=TextAlignment.Center,fontSize=20.0)

                            ]    
                        )
                    if model.IsLoading then 
                        yield Common.createBusyLayer()
                    ]
            )
            
          )
        
                

