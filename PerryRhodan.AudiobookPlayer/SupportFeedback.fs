﻿module SupportFeedback


    open Xamarin.Forms
    open Fabulous
    open Fabulous.XamarinForms
    open Common

    type Model = {
        Category:string
        Name:string
        Message:string
    }

    type Msg =
        | UpdateName of string
        | UpdateCategory of string
        | UpdateMessage of string
        | SendMessage
        | SendSuccessful
        | Error of string


    let init () =
        {
            Category = ""
            Message = ""
            Name = ""
        }, Cmd.none


    let update msg model =
        match msg with
        | UpdateName name ->
            {model with Name = name}, Cmd.none
        | UpdateCategory cat ->
            {model with Category = cat}, Cmd.none
        | UpdateMessage msg ->
            {model with Message = msg}, Cmd.none
        
        | SendMessage ->

            let sendCmd =
                async {
                    if (model.Message = "") then
                        return Error "Bitte eine Nachricht eingeben."
                    else
                        let! result = Services.SupportFeedback.sendSupportFeedBack model.Name model.Category model.Message
                        match result with
                        | Ok _ ->
                            return SendSuccessful
                        | Result.Error msg ->
                            return Error msg
                }
                |> Cmd.ofAsyncMsg

            model,Cmd.batch [sendCmd ]
        | SendSuccessful ->
            let cmd = Cmd.ofSub <| fun _ -> Common.Helpers.displayAlert("Vielen Dank!","Für ihre Nachricht!","OK") |> Async.StartImmediate
            model, cmd
        | Error errorMsg ->
            let cmd = Cmd.ofSub <| fun _ -> Common.Helpers.displayAlert("Ein Fehler ist aufgetreten!",errorMsg,"OK") |> Async.StartImmediate
            model, cmd


    let view model dispatch =
        View.ContentPage(
            automationId="supportFeedback",
            title=Translations.current.FeedbackPage,
            backgroundColor = Consts.backgroundColor,
            content = View.Grid(
                rowdefs = [ Auto;Auto;Auto;Auto;Auto;Star ],
                children = [
                    (Controls.primaryTextColorLabel 22.0 "Support und Feedback").Row(0)
                    
                    (Controls.primaryTextColorLabel 14.0 "Senden Sie uns ein Feedback oder eine Anfrage bei Problemen oder Verbesserungsvorschlägen. Die Anfrage geht direkt zum Entwickler. Es wird nur die E-Mail (falls angegeben) und der Text übermittelt. Mehr nicht! Bei Problemen wäre eine Beschreibung hilfreich und die Angabe ihrer Mailadresse, damit wir uns bei Ihnen melden können.").Row(1)

                    View.Button(text = "Nachricht senden!", command = (fun () -> dispatch SendMessage), horizontalOptions = LayoutOptions.Center).Row(2)


                    View.Entry(text = model.Name
                        , placeholder = "EMail (freiwillig)"
                        , textColor = Consts.primaryTextColor
                        , backgroundColor = Consts.backgroundColor
                        , placeholderColor = Consts.secondaryTextColor                                
                        , keyboard=Keyboard.Email
                        , completed = (fun t  -> if t <> model.Name then dispatch (UpdateName t))
                        , created = (fun e -> e.Unfocused.Add(fun args -> if model.Name<>e.Text then dispatch (UpdateName e.Text)))
                        ).Row(3)

                    
                    View.Editor(text = model.Message
                        , placeholder = "Hier die Nachricht eingeben"
                        , textColor = Consts.primaryTextColor
                        , backgroundColor = Consts.backgroundColor
                        , placeholderColor = Consts.secondaryTextColor                                
                        , keyboard=Keyboard.Chat
                        , completed = (fun t  -> if t <> model.Message then dispatch (UpdateMessage t))
                        , created = (fun e -> e.Unfocused.Add(fun args -> if model.Message<>e.Text then dispatch (UpdateMessage e.Text)))
                        ).Row(4)
                    

                ]
            )
        )

