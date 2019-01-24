module SettingsPage


    open Xamarin.Forms
    open Fabulous.DynamicViews
    open Fabulous.Core
    open Common

    type Language =
        | English
        | German

    
    type Model = 
        { Language: Language 
          FirstStart: bool
          DataProtectionStuff: bool }

    
    type Msg =
        | SetLanguage of Language
        | ShowDataProtectionStuff
        | HideDataProtectionStuff

    
    let initModel firstStart = 
        { Language = English
          FirstStart = firstStart 
          DataProtectionStuff = false }


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
                                        yield (Controls.secondaryTextColorLabel 16.0 Common.Texts.dataProtectionText)
                                            .Margin(10.0)
                                            .HorizontalOptions(LayoutOptions.Fill)
                                            .HorizontalTextAlignment(TextAlignment.Start)
                                           
                                        //yield View.Picker(
                                        //    itemsSource=["English";"German"],
                                        //    selectedIndex=0,                                            
                                        //    title="Language: ",
                                        //    inputTransparent = true,
                                        //    textColor = Consts.secondaryTextColor
                                        //)

                                        //yield View.Button(
                                        //    text=(if model.DataProtectionStuff then "Hide Data Protection Agreement" else "Show Data Protection Agreement"),                                                                                        
                                        //    inputTransparent = true,
                                        //    fontSize = 20.0,
                                        //    textColor = Consts.secondaryTextColor
                                        //)
                                    ]
                                )
                        )                    
                ]
            )
                
                   
        )

    



