module rec BrowserPage

open Fabulous
open Fabulous
open Fabulous.XamarinForms
open Xamarin.Forms
open System
open System.Net
open System.IO
open Domain
open Common
open Services
open Global


    type Model = {
        AudioBookItems: AudioBookItemNew.AudioBookItem []
        CurrentSessionCookieContainer:Map<string,string> option
        SelectedGroups:string list
        SelectedGroupItems: AudioBookListType
        IsLoading:bool 
    }


    type Msg = 
        | LoadOnlineAudiobooks 
        | AddSelectGroup of string
        | RemoveLastSelectGroup of string
        | ShowErrorMessage of string
        | ChangeBusyState of bool
        | GoToLoginPage of LoginRequestCameFrom

    let pageRef = ViewRef<CustomContentPage>()

    module Commands =
        let unbusyCmd = Cmd.ofMsg (ChangeBusyState false)


        let busyCmd = Cmd.ofMsg (ChangeBusyState true)


   

    let init selectedGroups (audioBookItems:AudioBookItemNew.AudioBookItem []) = 

        let audioBooks = 
            audioBookItems 
            |> Array.map (fun i -> i.Model.AudioBook)

        let groupedAudioBooks = audioBooks |> Domain.Filters.nameFilter
        


        // filter audio books
        let filteredAb =
            match selectedGroups with
            | [] -> 
                (GroupList groupedAudioBooks)
            | _ ->                
                ((GroupList groupedAudioBooks) |> Filters.groupsFilter (selectedGroups))

        let audioBooks =
            match filteredAb with
            | GroupList _ -> [||]
            | AudioBookList (_, ab) ->
                audioBookItems
                |> Array.filter (fun i -> ab |> Array.exists (fun a -> a.Id = i.Model.AudioBook.Id))


        let newModel = {
            AudioBookItems = audioBooks
            CurrentSessionCookieContainer = None
            IsLoading = false
            SelectedGroups = selectedGroups
            SelectedGroupItems = filteredAb
        }
    
        newModel, Cmd.none


    let rec update msg (model:Model) =
        match msg with
        | LoadOnlineAudiobooks ->
            model |> onLoadOnlineAudiobooksMsg
        | AddSelectGroup group ->
            model, Cmd.none

        | RemoveLastSelectGroup groupName ->
            model, Cmd.none
            
        | ShowErrorMessage e ->
            model |> onShowErrorMessageMsg e
        | ChangeBusyState state -> 
            model |> onChangeBusyStateMsg state
        | GoToLoginPage cameFrom ->
            model, Cmd.none

    and onLoadOnlineAudiobooksMsg model =
        model, Cmd.none
    and onShowErrorMessageMsg e model =
        Common.Helpers.displayAlert("Error",e,"OK") |> Async.StartImmediate
        model, Cmd.ofMsg (ChangeBusyState false)
    

    and onChangeBusyStateMsg state model =
        let newModel = {model with IsLoading = state}
        newModel, Cmd.none
    

    and onGotoLoginPageMsg cameFrom model =
        model, Cmd.none
    and onProcessDownloadQueueMsg msg model =
        model, Cmd.none
    
    let rec browseView (model: Model) dispatch =
        View.Grid(
            rowdefs= [Auto; Star;Auto; Auto],
            verticalOptions = LayoutOptions.Fill,
            children = [
                let browseTitle = 
                    
                    match model.SelectedGroups with
                    | [] -> Translations.current.BrowseAudioBooks
                    | head::tail -> sprintf "%s: %s" Translations.current.Browse head                   
                        

                yield View.Label(text=browseTitle, fontAttributes = FontAttributes.Bold,
                                    fontSize = FontSize.fromValue 25.,
                                    verticalOptions=LayoutOptions.Fill,
                                    horizontalOptions=LayoutOptions.Fill,
                                    horizontalTextAlignment=TextAlignment.Center,
                                    verticalTextAlignment=TextAlignment.Center,
                                    textColor = Consts.primaryTextColor,
                                    backgroundColor = Consts.cardColor,
                                    margin=Thickness 0.).Row(0)
                    
                yield View.StackLayout(padding = Thickness 10., verticalOptions = LayoutOptions.Start,
                    children = [ 
                        
                        // show refresh button only on category selection
                        if (model.SelectedGroups = []) then
                            yield View.Button(text=Translations.current.LoadYourAudioBooks, command = (fun () -> dispatch LoadOnlineAudiobooks))
                            
                        
                        match model.SelectedGroupItems with
                        | GroupList gp ->
                            let groups = gp |> Array.map (fun (key,_) -> key)

                                        
                            yield View.ScrollView(horizontalOptions = LayoutOptions.Fill,
                                verticalOptions = LayoutOptions.Fill,
                                content = 
                                    View.StackLayout(orientation=StackOrientation.Vertical,
                                        verticalOptions = LayoutOptions.Fill,
                                            
                                        children= [
                                            match groups with
                                            | [||] -> yield Controls.secondaryTextColorLabel 20. Translations.current.LoadYourAudioBooksHint
                                            | _ ->
                                                for (idx,item) in groups |> Array.indexed do
                                                    yield 
                                                        View.Label(text=item
                                                            , margin = Thickness 2.
                                                            , fontSize = FontSize.fromValue 20.
                                                            , textColor = Consts.secondaryTextColor
                                                            , horizontalOptions = LayoutOptions.Fill
                                                            , horizontalTextAlignment= if (model.SelectedGroups = []) then TextAlignment.Start else TextAlignment.Center
                                                            , verticalOptions = LayoutOptions.Fill
                                                            , verticalTextAlignment = TextAlignment.Center                                                                        
                                                            , gestureRecognizers = [View.TapGestureRecognizer(command = (fun () -> dispatch (AddSelectGroup item)))]
                                                                
                                                            )
                                                                    
                                            ]
                                        ))
                                        
                        | AudioBookList (key, items) ->
                                
                            yield View.ScrollView(horizontalOptions = LayoutOptions.Fill,
                                verticalOptions = LayoutOptions.Fill,
                                content = 
                                    View.StackLayout(orientation=StackOrientation.Vertical,
                                        children= [
                                            //let abItems = AudioBookItemProcessor.getAudioBookItems model.DisplayedAudioBooks
                                            for item in model.AudioBookItems do
                                                //let audioBookItemDispatch =
                                                //    let d msg = AudioBooksItemMsg (item,msg)
                                                //    d >> dispatch
                                                AudioBookItemNew.view item.Model item.Dispatch
                                        ]
                                    )
                                    )
                                
                                            
                        
                ]).Row(1)


                //if (model.SelectedGroups.Length > 0) then
                //    yield View.Button(text = Translations.current.Back,command = (fun ()-> dispatch RemoveLastSelectGroup)).Row(2)
                
                //let downloadQueueDispatch =
                //    DownloadQueueMsg >> dispatch

                //yield (DownloadQueue.view model.DownloadQueueModel downloadQueueDispatch).Row(3)


                

                if model.IsLoading then 
                    yield Common.createBusyLayer().RowSpan(2)

                    
            ]
        )


    let view (model:Model) dispatch =
        
        //// new nav page
        //dependsOn model.ListStates (fun _ listStates ->

            

        //    listStates            
        //    |> List.iteri (fun idx i ->
        //        let subRef = ViewRef<ContentPage>()
        //        // for a nav page remove all selected groups, every nav page stand alone
        //        let newModel = { 
        //            model with 
        //                ListStates= []
        //                LastSelectedGroup = Some i.GroupName
        //                SelectedGroupItems = i.ListType
        //                DisplayedAudioBooks = i.DisplayedAudioBooks
        //        }
        //        let cp = Controls.contentPageWithBottomOverlay subRef None (browseView newModel dispatch) false i.GroupName

        //        let suppressEntry = listStates.Length = (idx + 1) |> not
        //        PushModalHelper.pushOrUpdatePage dispatch RemoveLastSelectGroup i.GroupName suppressEntry pageRef cp
        //        ()
        //    )
        //)
        

        dependsOn model (fun _ m ->
            browseView m dispatch
        )
                
            
