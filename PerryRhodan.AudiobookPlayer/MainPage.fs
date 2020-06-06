module MainPage

open System
open System.Resources
open Fabulous
open Fabulous.XamarinForms
open Xamarin.Forms
open Domain
open Plugin.Permissions.Abstractions
open Common
open Services

    

    type Model = {
        Audiobooks: AudioBookItemNew.AudioBookItem []
        LastTimeListendAudioBook: AudioBookItemNew.AudioBookItem option
        IsLoading:bool
    }

    type Msg = 
        | ChangeBusyState of bool

    
    let emptyModel = { Audiobooks = [||] ; IsLoading = true; LastTimeListendAudioBook = None }
    
    
    let init (audiobooks:AudioBookItemNew.AudioBookItem []) = 
        let lastListenAudioBook =
            audiobooks 
            |> Array.filter (fun i -> i.Model.DownloadState = AudioBookItemNew.Downloaded)
            |> Array.sortByDescending (fun i -> i.Model.AudioBook.State.LastTimeListend) 
            |> Array.tryHead
            |> Option.bind (fun i -> 
                match i.Model.AudioBook.State.LastTimeListend with
                | None -> None
                | Some _ -> Some i
            ) 
        let model = {
            Audiobooks = 
                audiobooks
                |> Array.filter (fun x -> match x.Model.DownloadState with | AudioBookItemNew.Downloaded | AudioBookItemNew.Downloading _ -> true | _ -> false)
                |> Array.filter (fun i -> 
                    lastListenAudioBook 
                    |> Option.map (fun o -> o.Model.AudioBook.Id <> i.Model.AudioBook.Id)
                    |> Option.defaultValue true
                )
            LastTimeListendAudioBook = lastListenAudioBook
            IsLoading = false
        }
        let a = function 
            | Some x -> x 
            | None -> ""
        model, Cmd.none


    let rec update msg model =
        match msg with
        | ChangeBusyState state -> 
            model |> onChangeBusyStateMsg state

    
    and onChangeBusyStateMsg state model =
        {model with IsLoading = state}, Cmd.none



    let view (model: Model) dispatch =
        View.Grid(
            rowdefs= [Auto; Auto; Auto; Star],
            rowSpacing = 0.,
            verticalOptions = LayoutOptions.Fill,
            children = [

                match model.LastTimeListendAudioBook with
                | None ->()
                | Some abItem ->
                    View.Label(text=Translations.current.LastListendAudioBookTitle, fontAttributes = FontAttributes.Bold,
                        fontSize = FontSize 25.,
                        horizontalOptions = LayoutOptions.Fill,
                        horizontalTextAlignment = TextAlignment.Center,
                        textColor = Consts.primaryTextColor,
                        backgroundColor = Consts.cardColor,
                        margin=Thickness 0.).Row(0)
                    
                    (AudioBookItemNew.view abItem.Model abItem.Dispatch).Margin(Thickness 10.).Row(1)

                View.Label(text=Translations.current.AudiobookOnDevice, fontAttributes = FontAttributes.Bold,
                                                fontSize = FontSize 25.,
                                                horizontalOptions = LayoutOptions.Fill,
                                                horizontalTextAlignment = TextAlignment.Center,
                                                textColor = Consts.primaryTextColor,
                                                backgroundColor = Consts.cardColor,
                                                margin=Thickness 0.).Row(2)

                    
                View.StackLayout(padding = Thickness 10., verticalOptions = LayoutOptions.Start,
                    children = [ 
                        if not model.IsLoading then
                            match model.Audiobooks,model.LastTimeListendAudioBook with
                            | [||], None  ->
                                View.Label(text=Translations.current.NoAudiobooksOnDevice, fontSize=FontSize 25., textColor=Consts.secondaryTextColor)
                            | [||], Some _  ->
                                View.Label(text="...", fontSize=FontSize 25., textColor=Consts.secondaryTextColor)
                            | _, _ ->
                                View.ScrollView(horizontalOptions = LayoutOptions.Fill,
                                    verticalOptions = LayoutOptions.Fill,
                                    content = 
                                        View.StackLayout(orientation=StackOrientation.Vertical,
                                            children= [
                                                for item in model.Audiobooks do
                                                    AudioBookItemNew.view item.Model item.Dispatch
                                            ]
                                        )
                                )
                    ]).Row(3)


                if model.IsLoading then 
                    Common.createBusyLayer().RowSpan(4)
            ]
            )
    
