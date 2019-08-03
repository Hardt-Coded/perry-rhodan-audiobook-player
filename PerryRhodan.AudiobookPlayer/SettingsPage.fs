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
        RewindWhenStartAfterShortPeriodInSecModalModel:NumberPickerModal.Model option
        
        RewindWhenStartAfterLongPeriodInSec:int
        RewindWhenStartAfterLongPeriodInSecModalModel:NumberPickerModal.Model option
        
        LongPeriodBeginsAfterInMinutes:int
        LongPeriodBeginsAfterInMinutesModalModel:NumberPickerModal.Model option

        JumpDistance:int
        JumpDistanceModalModel:NumberPickerModal.Model option

        ShellRef:ViewRef<Shell>
    }

    
    type Msg =
        | SetLanguage of Language
        | ShowDataProtectionStuff
        | HideDataProtectionStuff
        

        | OpenJumpDistancePicker 
        | JumpDistanceMsg of NumberPickerModal.Msg
        | CloseJumpDistancePicker
        | SetJumpDistanceValue of int

        | OpenRewindWhenStartAfterShortPeriodInSecPicker 
        | RewindWhenStartAfterShortPeriodInSecMsg of NumberPickerModal.Msg
        | CloseRewindWhenStartAfterShortPeriodInSecPicker
        | SetRewindWhenStartAfterShortPeriodInSecValue of int

        | OpenRewindWhenStartAfterLongPeriodInSecPicker 
        | RewindWhenStartAfterLongPeriodInSecMsg of NumberPickerModal.Msg
        | CloseRewindWhenStartAfterLongPeriodInSecPicker
        | SetRewindWhenStartAfterLongPeriodInSecValue of int

        | OpenLongPeriodBeginsAfterInMinutesPicker 
        | LongPeriodBeginsAfterInMinutesMsg of NumberPickerModal.Msg
        | CloseLongPeriodBeginsAfterInMinutesPicker
        | SetLongPeriodBeginsAfterInMinutesValue of int


    let strToOptInt str =
        let (isInt,value) = System.Int32.TryParse(str)
        if isInt then Some value else None
    
    let initModel shellref firstStart = 
        { 
            Language = English
            FirstStart = firstStart 
            DataProtectionStuff = false
            RewindWhenStartAfterShortPeriodInSec = 
                Services.SystemSettings.getRewindWhenStartAfterShortPeriodInSec() 
                |> Async.RunSynchronously
            RewindWhenStartAfterShortPeriodInSecModalModel = None
            RewindWhenStartAfterLongPeriodInSec = 
                Services.SystemSettings.getRewindWhenStartAfterLongPeriodInSec() 
                |> Async.RunSynchronously
            RewindWhenStartAfterLongPeriodInSecModalModel = None
            LongPeriodBeginsAfterInMinutes = 
                Services.SystemSettings.getLongPeriodBeginsAfterInMinutes() 
                |> Async.RunSynchronously
            LongPeriodBeginsAfterInMinutesModalModel = None
            JumpDistance =
                (Services.SystemSettings.getJumpDistance() 
                |> Async.RunSynchronously) / 1000
            JumpDistanceModalModel = None
            ShellRef = shellref
        }


    let init shellref firstStart =
        initModel shellref firstStart, Cmd.none, None


    let rec update msg (model:Model) =
        match msg with
        | SetLanguage language ->
            model |> onSetLanguageMsg language
        | ShowDataProtectionStuff ->
            model |> onShowDataProtectionStuffMsg
        | HideDataProtectionStuff ->
            model |> onHideDataProtectionStuffMsg
        

        | OpenJumpDistancePicker ->
            model |> onOpenJumpDistancePicker
        | JumpDistanceMsg msg ->
            model |> onProcessJumpDistanceMsg msg
        | CloseJumpDistancePicker ->
            model |> onCloseJumpDistancePicker
        | SetJumpDistanceValue jd ->
            model |> onSetJumpDistanceValue jd


        | OpenRewindWhenStartAfterShortPeriodInSecPicker ->
            model |> onOpenRewindWhenStartAfterShortPeriodInSecPicker
        | RewindWhenStartAfterShortPeriodInSecMsg msg ->
            model |> onProcessRewindWhenStartAfterShortPeriodInSecMsg msg
        | CloseRewindWhenStartAfterShortPeriodInSecPicker ->
            model |> onCloseRewindWhenStartAfterShortPeriodInSecPicker
        | SetRewindWhenStartAfterShortPeriodInSecValue sec ->
            model |> onSetRewindWhenStartAfterShortPeriodInSecValue sec

        | OpenRewindWhenStartAfterLongPeriodInSecPicker ->
            model |> onOpenRewindWhenStartAfterLongPeriodInSecPicker
        | RewindWhenStartAfterLongPeriodInSecMsg msg ->
            model |> onProcessRewindWhenStartAfterLongPeriodInSecMsg msg
        | CloseRewindWhenStartAfterLongPeriodInSecPicker ->
            model |> onCloseRewindWhenStartAfterLongPeriodInSecPicker
        | SetRewindWhenStartAfterLongPeriodInSecValue sec ->
            model |> onSetRewindWhenStartAfterLongPeriodInSecValue sec

        | OpenLongPeriodBeginsAfterInMinutesPicker ->
            model |> onOpenLongPeriodBeginsAfterInMinutesPicker
        | LongPeriodBeginsAfterInMinutesMsg msg ->
            model |> onProcessLongPeriodBeginsAfterInMinutesMsg msg
        | CloseLongPeriodBeginsAfterInMinutesPicker ->
            model |> onCloseLongPeriodBeginsAfterInMinutesPicker
        | SetLongPeriodBeginsAfterInMinutesValue min ->
            model |> onSetLongPeriodBeginsAfterInMinutesValue min



    

    and onSetJumpDistanceValue jd model =
        let jdMs = jd * 1000
        Services.SystemSettings.setJumpDistance jdMs |> Async.RunSynchronously
        Common.ModalBaseHelpers.closeCurrentModal model.ShellRef
        {model with JumpDistance = jd; JumpDistanceModalModel = None},Cmd.none,None


    and onOpenJumpDistancePicker model =
        let (mdl,cmd) = NumberPickerModal.init Translations.current.SelectJumpDistance [5..5..60] model.JumpDistance
        {model with JumpDistanceModalModel = Some mdl},Cmd.none,None


    and onCloseJumpDistancePicker model =
        {model with JumpDistanceModalModel = None},Cmd.none,None

    and onProcessJumpDistanceMsg msg model =
        match model.JumpDistanceModalModel with
        | None ->
            model,Cmd.none,None
        | Some jdModel ->
            let (mdl,cmd,exCmd) = NumberPickerModal.update msg jdModel
            let cmd = cmd |> Cmd.map JumpDistanceMsg
            let exCmd =
                exCmd
                |> Option.map (fun c->
                    match c with
                    | NumberPickerModal.ExternalMsg.TakeValue ->
                        Cmd.ofMsg (SetJumpDistanceValue (mdl.Value))
                )
                |> Option.defaultValue Cmd.none
            {model with JumpDistanceModalModel = Some mdl},Cmd.batch [ cmd;exCmd],None



    and onSetRewindWhenStartAfterShortPeriodInSecValue sec model =
        Services.SystemSettings.setRewindWhenStartAfterShortPeriodInSec sec |> Async.RunSynchronously
        Common.ModalBaseHelpers.closeCurrentModal model.ShellRef
        {model with RewindWhenStartAfterShortPeriodInSec = sec; RewindWhenStartAfterShortPeriodInSecModalModel = None},Cmd.none,None


    and onOpenRewindWhenStartAfterShortPeriodInSecPicker model =
        let (mdl,cmd) = NumberPickerModal.init Translations.current.SelectRewindWhenStartAfterShortPeriodInSec [0..5..60] model.RewindWhenStartAfterShortPeriodInSec
        {model with RewindWhenStartAfterShortPeriodInSecModalModel = Some mdl},Cmd.none,None


    and onCloseRewindWhenStartAfterShortPeriodInSecPicker model =
        {model with RewindWhenStartAfterShortPeriodInSecModalModel = None},Cmd.none,None

    and onProcessRewindWhenStartAfterShortPeriodInSecMsg msg model =
        match model.RewindWhenStartAfterShortPeriodInSecModalModel with
        | None ->
            model,Cmd.none,None
        | Some jdModel ->
            let (mdl,cmd,exCmd) = NumberPickerModal.update msg jdModel
            let cmd = cmd |> Cmd.map RewindWhenStartAfterShortPeriodInSecMsg
            let exCmd =
                exCmd
                |> Option.map (fun c->
                    match c with
                    | NumberPickerModal.ExternalMsg.TakeValue ->
                        Cmd.ofMsg (SetRewindWhenStartAfterShortPeriodInSecValue (mdl.Value))
                )
                |> Option.defaultValue Cmd.none
            {model with RewindWhenStartAfterShortPeriodInSecModalModel = Some mdl},Cmd.batch [ cmd;exCmd],None



    and onSetRewindWhenStartAfterLongPeriodInSecValue sec model =
        Services.SystemSettings.setRewindWhenStartAfterLongPeriodInSec sec |> Async.RunSynchronously
        Common.ModalBaseHelpers.closeCurrentModal model.ShellRef
        {model with RewindWhenStartAfterLongPeriodInSec = sec; RewindWhenStartAfterLongPeriodInSecModalModel = None},Cmd.none,None


    and onOpenRewindWhenStartAfterLongPeriodInSecPicker model =
        let (mdl,cmd) = NumberPickerModal.init Translations.current.SelectRewindWhenStartAfterLongPeriodInSec [0..5..120] model.RewindWhenStartAfterLongPeriodInSec
        {model with RewindWhenStartAfterLongPeriodInSecModalModel = Some mdl},Cmd.none,None


    and onCloseRewindWhenStartAfterLongPeriodInSecPicker model =
        {model with RewindWhenStartAfterLongPeriodInSecModalModel = None},Cmd.none,None

    and onProcessRewindWhenStartAfterLongPeriodInSecMsg msg model =
        match model.RewindWhenStartAfterLongPeriodInSecModalModel with
        | None ->
            model,Cmd.none,None
        | Some jdModel ->
            let (mdl,cmd,exCmd) = NumberPickerModal.update msg jdModel
            let cmd = cmd |> Cmd.map RewindWhenStartAfterLongPeriodInSecMsg
            let exCmd =
                exCmd
                |> Option.map (fun c->
                    match c with
                    | NumberPickerModal.ExternalMsg.TakeValue ->
                        Cmd.ofMsg (SetRewindWhenStartAfterLongPeriodInSecValue (mdl.Value))
                )
                |> Option.defaultValue Cmd.none
            {model with RewindWhenStartAfterLongPeriodInSecModalModel = Some mdl},Cmd.batch [ cmd;exCmd],None



    and onSetLongPeriodBeginsAfterInMinutesValue min model =
        Services.SystemSettings.setLongPeriodBeginsAfterInMinutes min |> Async.RunSynchronously
        Common.ModalBaseHelpers.closeCurrentModal model.ShellRef
        {model with LongPeriodBeginsAfterInMinutes = min; LongPeriodBeginsAfterInMinutesModalModel = None},Cmd.none,None


    and onOpenLongPeriodBeginsAfterInMinutesPicker model =
        let (mdl,cmd) = NumberPickerModal.init Translations.current.SelectLongPeriodBeginsAfterInMinutes [10..10..600] model.LongPeriodBeginsAfterInMinutes
        {model with LongPeriodBeginsAfterInMinutesModalModel = Some mdl},Cmd.none,None


    and onCloseLongPeriodBeginsAfterInMinutesPicker model =
        {model with LongPeriodBeginsAfterInMinutesModalModel = None},Cmd.none,None

    and onProcessLongPeriodBeginsAfterInMinutesMsg msg model =
        match model.LongPeriodBeginsAfterInMinutesModalModel with
        | None ->
            model,Cmd.none,None
        | Some jdModel ->
            let (mdl,cmd,exCmd) = NumberPickerModal.update msg jdModel
            let cmd = cmd |> Cmd.map LongPeriodBeginsAfterInMinutesMsg
            let exCmd =
                exCmd
                |> Option.map (fun c->
                    match c with
                    | NumberPickerModal.ExternalMsg.TakeValue ->
                        Cmd.ofMsg (SetLongPeriodBeginsAfterInMinutesValue (mdl.Value))
                )
                |> Option.defaultValue Cmd.none
            {model with LongPeriodBeginsAfterInMinutesModalModel = Some mdl},Cmd.batch [ cmd;exCmd],None
    
    
    
    
    and onSetLanguageMsg language (model:Model) =
        { model with Language = language }, Cmd.none, None
    
    
    and onShowDataProtectionStuffMsg model =
        { model with DataProtectionStuff = true }, Cmd.none, None
    

    and onHideDataProtectionStuffMsg model =
        { model with DataProtectionStuff = false }, Cmd.none, None


    



    let settingsEntry label value onTap =
        
        View.Grid(
            rowdefs=["auto";"auto";1.0],
            horizontalOptions=LayoutOptions.Start,
            children=[
                (Controls.primaryTextColorLabel Common.FontSizeHelper.mediumLabel label)
                    .GridRow(0)
                    .Margin(Thickness(10.,0.,0.,0.))
                    .HorizontalTextAlignment(TextAlignment.Start)
                (Controls.secondaryTextColorLabel Common.FontSizeHelper.smallLabel value)
                    .GridRow(1)
                    .Margin(Thickness(30.,0.,0.,0.))
                    .HorizontalTextAlignment(TextAlignment.Start)
                View.BoxView(color=Common.Consts.cardColor)
                    .GridRow(2)
                    .HorizontalOptions(LayoutOptions.Fill)

            ]
        ).GestureRecognizers([
            View.TapGestureRecognizer(command=onTap)
        ]).HorizontalOptions(LayoutOptions.Fill)



    let view model dispatch =

        // modals
        model.JumpDistanceModalModel
        |> Option.map (fun mdl ->
            let page = NumberPickerModal.view mdl (JumpDistanceMsg >> dispatch)
            Common.ModalBaseHelpers.pushOrUpdateModal dispatch CloseJumpDistancePicker NumberPickerModal.modalTitle model.ShellRef page
        ) |> ignore

        model.RewindWhenStartAfterShortPeriodInSecModalModel
        |> Option.map (fun mdl ->
            let page = NumberPickerModal.view mdl (RewindWhenStartAfterShortPeriodInSecMsg >> dispatch)
            Common.ModalBaseHelpers.pushOrUpdateModal dispatch CloseRewindWhenStartAfterShortPeriodInSecPicker NumberPickerModal.modalTitle model.ShellRef page
        ) |> ignore

        model.RewindWhenStartAfterLongPeriodInSecModalModel
        |> Option.map (fun mdl ->
            let page = NumberPickerModal.view mdl (RewindWhenStartAfterLongPeriodInSecMsg >> dispatch)
            Common.ModalBaseHelpers.pushOrUpdateModal dispatch CloseRewindWhenStartAfterLongPeriodInSecPicker NumberPickerModal.modalTitle model.ShellRef page
        ) |> ignore

        model.LongPeriodBeginsAfterInMinutesModalModel
        |> Option.map (fun mdl ->
            let page = NumberPickerModal.view mdl (LongPeriodBeginsAfterInMinutesMsg >> dispatch)
            Common.ModalBaseHelpers.pushOrUpdateModal dispatch CloseLongPeriodBeginsAfterInMinutesPicker NumberPickerModal.modalTitle model.ShellRef page
        ) |> ignore


        dependsOn model (fun _ mdl ->
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
                                                coldefs=[ "*" ],
                                                rowdefs=[ "auto"; "auto"; "auto";"auto" ],
                                                children=[

                                                    (settingsEntry 
                                                        Translations.current.RewindWhenStartAfterShortPeriodInSec 
                                                        (sprintf "%i %s" model.RewindWhenStartAfterShortPeriodInSec Translations.current.Seconds)
                                                        (fun ()->dispatch OpenRewindWhenStartAfterShortPeriodInSecPicker)).GridRow(0)

                                                    (settingsEntry 
                                                        Translations.current.RewindWhenStartAfterLongPeriodInSec 
                                                        (sprintf "%i %s" model.RewindWhenStartAfterLongPeriodInSec Translations.current.Seconds)
                                                        (fun ()->dispatch OpenRewindWhenStartAfterLongPeriodInSecPicker)).GridRow(1)

                                                    (settingsEntry 
                                                        Translations.current.LongPeriodBeginsAfterInMinutes 
                                                        (sprintf "%i %s" model.LongPeriodBeginsAfterInMinutes Translations.current.Minutes)
                                                        (fun ()->dispatch OpenLongPeriodBeginsAfterInMinutesPicker)).GridRow(2)

                                                    (settingsEntry 
                                                        Translations.current.JumpDistance 
                                                        (sprintf "%i %s" model.JumpDistance Translations.current.Seconds)
                                                        (fun () -> dispatch OpenJumpDistancePicker)).GridRow(3)
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
        )
        

    



