module AudioBookDetailPage

    open Domain
    open Xamarin.Forms
    open Fabulous
    open Fabulous.XamarinForms
    open Common

    type Model = 
        { AudioBook: AudioBook
          Description: string option 
          Image: string option
          IsLoading:bool }

    type Msg =
        | SetAudioBookDetails of string option * string option
        | ShowErrorMessage of string
        | ChangeBusyState of bool

   
    
    let initModel audiobook = 
        { AudioBook = audiobook
          Description = None
          Image = None 
          IsLoading = true }

    let loadAudioBookInfos audioBook =
        async {
            let! res = Services.WebAccess.loadDescription audioBook
            match res with           
            | Error e -> 
                match e with
                | SessionExpired e -> return (ShowErrorMessage e)
                | Other e -> return (ShowErrorMessage e)
                | Network e -> return (ShowErrorMessage e)
                | Exception e ->
                    let ex = e.GetBaseException()
                    let msg = ex.Message + "|" + ex.StackTrace
                    return (ShowErrorMessage msg)
            | Ok (description, image) ->
                return SetAudioBookDetails (description, image)
                
        }
        |> Cmd.ofAsyncMsg
    
    
    let init audiobook =
        let model = audiobook |> initModel
        let cmd = audiobook |> loadAudioBookInfos
        model, cmd

    let rec update msg model =
        match msg with
        | SetAudioBookDetails (description,image) ->
            {model with Image = image; Description = description }, Cmd.ofMsg (ChangeBusyState false), None
        | ShowErrorMessage e ->
            model |> onShowErrorMessageMsg e
        | ChangeBusyState state -> 
            model |> onChangeBusyStateMsg state


    and onShowErrorMessageMsg e model =
        Common.Helpers.displayAlert(Translations.current.Error,e,"OK") |> ignore
        model, Cmd.ofMsg (ChangeBusyState false), None
    

    and onChangeBusyStateMsg state model =
        {model with IsLoading = state}, Cmd.none, None
    
    
    
    let view model dispatch =
        View.ContentPage(
            title=Translations.current.AudioBookDetailPage,
            backgroundColor = Consts.backgroundColor,
            content = View.Grid(
                children = [
                    yield View.ScrollView(
                            content=
                                View.StackLayout(
                                    orientation=StackOrientation.Vertical,
                                    children=[
                                        yield (Controls.primaryTextColorLabel 35. model.AudioBook.FullName)
                                           
                                        if (model.Image.IsSome) then
                                            yield View.Image(
                                                source=ImagePath model.Image.Value,
                                                aspect=Aspect.AspectFit,
                                                horizontalOptions=LayoutOptions.Fill,
                                                verticalOptions=LayoutOptions.Fill
                                                )
                                                .Margin(Thickness(10.,10.,10.,10.))
                                        if (model.Description.IsSome) then
                                            yield View.Label(
                                                fontSize=FontSize 20.,
                                                text=model.Description.Value,
                                                horizontalTextAlignment=TextAlignment.Start,
                                                textColor=Common.Consts.secondaryTextColor,
                                                margin=Thickness(10.,10.,10.,10.)
                                                )
                                    ]
                                )
                        )

                    if model.IsLoading then
                        yield Common.createBusyLayer()
                ]
            )
                
                   
        )