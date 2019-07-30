module SettingsPage


    open Xamarin.Forms
    open Fabulous
    open Fabulous.XamarinForms
    open Common


    let hideLastListendSettingsKey = "Rhodan_HideLastListendWhenOnlyOneAudioBookOnDevice"

    type Language =
        | English
        | German

    
    type Model = { 
        Language: Language 
        FirstStart: bool
        DataProtectionStuff: bool
        RewindWhenStartAfterShortPeriodInSec:int
        RewindWhenStartAfterLongPeriodInSec:int
        LongPeriodBeginsAfterInMinutes:int
    }

    
    type Msg =
        | SetLanguage of Language
        | ShowDataProtectionStuff
        | HideDataProtectionStuff
        | SetRewindWhenStartAfterShortPeriodInSec of string
        | SetRewindWhenStartAfterLongPeriodInSec of string
        | SetLongPeriodBeginsAfterInMinutes of string


    let strToOptInt str =
        let (isInt,value) = System.Int32.TryParse(str)
        if isInt then Some value else None
    
    let initModel firstStart = 
        
        


        { 
            Language = English
            FirstStart = firstStart 
            DataProtectionStuff = false
            RewindWhenStartAfterShortPeriodInSec = 
                Services.SystemSettings.getRewindWhenStartAfterShortPeriodInSec() 
                |> Async.RunSynchronously
            RewindWhenStartAfterLongPeriodInSec = 
                Services.SystemSettings.getRewindWhenStartAfterLongPeriodInSec() 
                |> Async.RunSynchronously
            LongPeriodBeginsAfterInMinutes = 
                Services.SystemSettings.getLongPeriodBeginsAfterInMinutes() 
                |> Async.RunSynchronously
        }


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
        | SetRewindWhenStartAfterShortPeriodInSec i ->
            model |> onRewindWhenStartAfterShortPeriodInSec i
        | SetRewindWhenStartAfterLongPeriodInSec i ->
            model |> onRewindWhenStartAfterLongPeriodInSec i
        | SetLongPeriodBeginsAfterInMinutes i ->
            model |> onLongPeriodBeginsAfterInMinutes i
    
    

    and onSetLanguageMsg language (model:Model) =
        { model with Language = language }, Cmd.none, None
    
    
    and onShowDataProtectionStuffMsg model =
        { model with DataProtectionStuff = true }, Cmd.none, None
    

    and onHideDataProtectionStuffMsg model =
        { model with DataProtectionStuff = false }, Cmd.none, None


    and onRewindWhenStartAfterShortPeriodInSec value model =
        let intValue = 
            value 
            |> strToOptInt 
            |> Option.defaultValue Services.SystemSettings.defaultRewindWhenStartAfterShortPeriodInSec

        Services.SystemSettings.setRewindWhenStartAfterShortPeriodInSec intValue 
        |> Async.RunSynchronously
        {model with RewindWhenStartAfterShortPeriodInSec = intValue}, Cmd.none, None


    and onRewindWhenStartAfterLongPeriodInSec value model =
        let intValue = 
            value 
            |> strToOptInt 
            |> Option.defaultValue Services.SystemSettings.defaultRewindWhenStartAfterLongPeriodInSec

        Services.SystemSettings.setRewindWhenStartAfterLongPeriodInSec intValue 
        |> Async.RunSynchronously
        {model with RewindWhenStartAfterLongPeriodInSec= intValue}, Cmd.none, None


    and onLongPeriodBeginsAfterInMinutes value model =
        let intValue = 
            value 
            |> strToOptInt 
            |> Option.defaultValue Services.SystemSettings.defaultLongPeriodBeginsAfterInMinutes

        Services.SystemSettings.setLongPeriodBeginsAfterInMinutes intValue 
        |> Async.RunSynchronously
        {model with LongPeriodBeginsAfterInMinutes = intValue}, Cmd.none, None




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
                                    margin=5.0,
                                    children=[
                                        
                                        yield View.Grid(
                                            coldefs=[ "*"; 100.0 ; "auto" ],
                                            rowdefs=[ "auto"; "auto"; "auto" ],
                                            children=[
                                                (Controls.secondaryTextColorLabel 20.0 Translations.current.RewindWhenStartAfterShortPeriodInSec).GridColumn(0).GridRow(0)
                                                (Controls.secondaryTextColorLabel 20.0 Translations.current.RewindWhenStartAfterLongPeriodInSec).GridColumn(0).GridRow(1)
                                                (Controls.secondaryTextColorLabel 20.0 Translations.current.LongPeriodBeginsAfterInMinutes).GridColumn(0).GridRow(2)
                                                View.Entry(text=model.RewindWhenStartAfterShortPeriodInSec.ToString()
                                                    , keyboard=Keyboard.Numeric
                                                    , completed = (fun t  -> if t <> model.RewindWhenStartAfterShortPeriodInSec.ToString() then dispatch (SetRewindWhenStartAfterShortPeriodInSec t))
                                                    , created = (fun e -> e.Unfocused.Add(fun args -> if model.RewindWhenStartAfterShortPeriodInSec.ToString()<>e.Text then dispatch (SetRewindWhenStartAfterShortPeriodInSec e.Text)))
                                                ).GridColumn(1).GridRow(0)
                                                View.Entry(text=model.RewindWhenStartAfterLongPeriodInSec.ToString()
                                                    , keyboard=Keyboard.Numeric
                                                    , completed = (fun t  -> if t <> model.RewindWhenStartAfterLongPeriodInSec.ToString() then dispatch (SetRewindWhenStartAfterLongPeriodInSec t))
                                                    , created = (fun e -> e.Unfocused.Add(fun args -> if model.RewindWhenStartAfterLongPeriodInSec.ToString()<>e.Text then dispatch (SetRewindWhenStartAfterLongPeriodInSec e.Text)))
                                                ).GridColumn(1).GridRow(1)
                                                View.Entry(text=model.LongPeriodBeginsAfterInMinutes.ToString()
                                                    , keyboard=Keyboard.Numeric
                                                    , completed = (fun t  -> if t <> model.LongPeriodBeginsAfterInMinutes.ToString() then dispatch (SetLongPeriodBeginsAfterInMinutes t))
                                                    , created = (fun e -> e.Unfocused.Add(fun args -> if model.LongPeriodBeginsAfterInMinutes.ToString()<>e.Text then dispatch (SetLongPeriodBeginsAfterInMinutes e.Text)))

                                                ).GridColumn(1).GridRow(2)
                                                (Controls.secondaryTextColorLabel 20.0 "s").GridColumn(2).GridRow(0)
                                                (Controls.secondaryTextColorLabel 20.0 "s").GridColumn(2).GridRow(1)
                                                (Controls.secondaryTextColorLabel 20.0 "min").GridColumn(2).GridRow(2)
                                            ]
                                        )
                                        
                                        yield View.Button(
                                            text=(if model.DataProtectionStuff then Translations.current.HideDataProtection else Translations.current.ShowDataProtection),                                                                                        
                                            command=(fun ()-> dispatch (if model.DataProtectionStuff then HideDataProtectionStuff else ShowDataProtectionStuff)),
                                            margin=Thickness(0.,10.,0.,0.)
                                        )

                                        //yield View.StackLayout(orientation=StackOrientation.Horizontal,
                                        //    horizontalOptions = LayoutOptions.Center,
                                        //    children =[
                                        //        Controls.secondaryTextColorLabel 18. Translations.current.HideLastListendWhenOnlyOneAudioBookOnDevice
                                        //        View.Switch(isToggled = model.HideLastListendWhenOnlyOneAudioBookOnDevice, toggled = (fun on -> dispatch (ToggleHideLastListend on.Value)), horizontalOptions = LayoutOptions.Center)
                                        //    ]
                                        //)

                                        if (model.DataProtectionStuff) then
                                            let viewSource = UrlWebViewSource(Url="https://hardt-solutions.com/PrivacyPolicies/EinsAMedienAudiobookPlayer.html")

                                            yield View.ScrollView(
                                                verticalScrollBarVisibility=ScrollBarVisibility.Always,
                                                horizontalOptions=LayoutOptions.FillAndExpand,
                                                verticalOptions=LayoutOptions.FillAndExpand,
                                                content=View.WebView(source=viewSource,
                                                    horizontalOptions=LayoutOptions.FillAndExpand,
                                                    verticalOptions=LayoutOptions.FillAndExpand
                                                )
                                            )
                                        
                                    ]
                                )
                        )                    
                ]
            )
                
                   
        )

    



