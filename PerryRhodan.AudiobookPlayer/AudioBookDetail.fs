module AudioBookDetailPage

    open Domain
    open Xamarin.Forms
    open Fabulous.DynamicViews
    open Fabulous.Core
    open Common

    type Model = 
        { AudioBook: AudioBook
          Description: string option 
          Image: string option
          IsLoading:bool }

    type Msg =
        | SetAudioBookDetails of string option * string option
        
        | Close
        | ShowErrorMessage of string
        | ChangeBusyState of bool

    type ExternalMsg =
        | CloseAudioBookDetailPage
    
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
        | Close -> 
            model |> onCloseMsg
        | ShowErrorMessage e ->
            model |> onShowErrorMessageMsg e
        | ChangeBusyState state -> 
            model |> onChangeBusyStateMsg state

    and onCloseMsg model =
        model, Cmd.none, Some CloseAudioBookDetailPage


    and onShowErrorMessageMsg e model =
        Common.Helpers.displayAlert("Error",e,"OK") |> ignore
        model, Cmd.ofMsg (ChangeBusyState false), None
    

    and onChangeBusyStateMsg state model =
        {model with IsLoading = state}, Cmd.none, None
    
    
    
    let view model dispatch =
        View.ContentPage(
            title="Detail",useSafeArea=true,
            backgroundColor = Consts.backgroundColor,
            content = View.Grid(
                children = [
                    yield View.ScrollView(
                            content=
                                View.StackLayout(
                                    orientation=StackOrientation.Vertical,
                                    children=[
                                        yield (Controls.primaryTextColorLabel 35 model.AudioBook.FullName)
                                           
                                        if (model.Image.IsSome) then
                                            yield View.Image(
                                                source=model.Image.Value,
                                                aspect=Aspect.AspectFit,
                                                horizontalOptions=LayoutOptions.Fill,
                                                verticalOptions=LayoutOptions.Fill
                                                )
                                                .Margin(Thickness(10.0,10.0,10.0,10.0))
                                        if (model.Description.IsSome) then
                                            yield View.Label(
                                                fontSize=20.0,
                                                text=model.Description.Value,
                                                horizontalTextAlignment=TextAlignment.Start,
                                                textColor=Common.Consts.secondaryTextColor,
                                                margin=Thickness(10.0,10.0,10.0,10.0)
                                                )
                                                
                                        yield View.Button(text="Close",command= (fun ()-> dispatch Close))
                                    ]
                                )
                        )

                    if model.IsLoading then
                        yield Common.createBusyLayer()
                ]
            )
                
                   
        )