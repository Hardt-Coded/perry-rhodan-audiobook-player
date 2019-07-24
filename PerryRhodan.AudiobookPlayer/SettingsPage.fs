module SettingsPage


    open Xamarin.Forms
    open Fabulous
    open Fabulous.XamarinForms
    open Common


    let hideLastListendSettingsKey = "Rhodan_HideLastListendWhenOnlyOneAudioBookOnDevice"

    type Language =
        | English
        | German

    
    type Model = 
        { Language: Language 
          FirstStart: bool
          DataProtectionStuff: bool
          HideLastListendWhenOnlyOneAudioBookOnDevice: bool }

    
    type Msg =
        | SetLanguage of Language
        | ShowDataProtectionStuff
        | HideDataProtectionStuff
        | ToggleHideLastListend of bool

    
    let initModel firstStart = 
        let getHideLastListendSetting () = 
            Services.SecureLoginStorage.getSecuredValue hideLastListendSettingsKey |> Async.RunSynchronously
            |> Option.map (fun i -> if i="1" then true else false)
            |> Option.defaultValue false

        { Language = English
          FirstStart = firstStart 
          DataProtectionStuff = false
          HideLastListendWhenOnlyOneAudioBookOnDevice = getHideLastListendSetting () }


    let init firstStart =
        initModel firstStart, Cmd.none, None


    let rec update msg (model:Model) =
        match msg with
        | SetLanguage language ->
            model |> onSetLanguageMsg language
        | ShowDataProtectionStuff ->
            model |> onShowDataProtectionStuffMsg
        | HideDataProtectionStuff ->
            model |> onHideDataProtectionStuffMsg
        | ToggleHideLastListend b ->
            model |> onHideLastListendMsg b
    
    and onHideLastListendMsg b model =
        (hideLastListendSettingsKey |> Services.SecureLoginStorage.setSecuredValue (if b then "1" else "0") )|> Async.RunSynchronously
        {model with HideLastListendWhenOnlyOneAudioBookOnDevice = b}, Cmd.none, None

    and onSetLanguageMsg language (model:Model) =
        { model with Language = language }, Cmd.none, None
    
    
    and onShowDataProtectionStuffMsg model =
        { model with DataProtectionStuff = true }, Cmd.none, None
    

    and onHideDataProtectionStuffMsg model =
        { model with DataProtectionStuff = false }, Cmd.none, None




    let view model dispatch =
        View.ContentPage(
            title=Translations.current.SettingsPage,useSafeArea=true,
            backgroundColor = Consts.backgroundColor,
            content = View.Grid(
                children = [
                    yield View.ScrollView(
                            content=
                                View.StackLayout(
                                    orientation=StackOrientation.Vertical,
                                    children=[
                                        
                                        yield View.Button(
                                            text=(if model.DataProtectionStuff then Translations.current.HideDataProtection else Translations.current.ShowDataProtection),                                                                                        
                                            command=(fun ()-> dispatch (if model.DataProtectionStuff then HideDataProtectionStuff else ShowDataProtectionStuff))
                                        )

                                        //yield View.StackLayout(orientation=StackOrientation.Horizontal,
                                        //    horizontalOptions = LayoutOptions.Center,
                                        //    children =[
                                        //        Controls.secondaryTextColorLabel 18. Translations.current.HideLastListendWhenOnlyOneAudioBookOnDevice
                                        //        View.Switch(isToggled = model.HideLastListendWhenOnlyOneAudioBookOnDevice, toggled = (fun on -> dispatch (ToggleHideLastListend on.Value)), horizontalOptions = LayoutOptions.Center)
                                        //    ]
                                        //)

                                        if (model.DataProtectionStuff) then
                                            let viewSource = UrlWebViewSource(Url="http://hardt-solutions.com/PrivacyPolicies/EinsAMedienAudiobookPlayer.html")

                                            yield View.WebView(source=viewSource,
                                                horizontalOptions=LayoutOptions.FillAndExpand,
                                                verticalOptions=LayoutOptions.FillAndExpand
                                                )
                                           
                                        

                                        
                                    ]
                                )
                        )                    
                ]
            )
                
                   
        )

    



