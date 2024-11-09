namespace PerryRhodan.AudiobookPlayer.ViewModels

open System
open System.Collections.ObjectModel
open System.Threading.Tasks
open CherylUI.Controls
open Common
open Dependencies
open Domain
open PerryRhodan.AudiobookPlayer.Services.Interfaces
open PerryRhodan.AudiobookPlayer.ViewModel
open PerryRhodan.AudiobookPlayer.ViewModel.AudioBookItem
open Services
open Services.DependencyServices


module BrowserPageOld =

    type State = {
        AudioBookItems: AudioBookItemViewModel []
        PreviousGroups: string list
        AvailableGroups: string list
        SelectedGroups:string list
        SelectedGroupItems: AudioBookListType
        IsLoading:bool
        SearchText:string
        BusyMessage:string
    }


    type Msg =
        | RunOnlySideEffect of SideEffect
        | LoadOnlineAudiobooks
        | AudioBookItemsChanged of AudioBookItemViewModel []
        | SelectPreviousGroup of string
        | AddSelectGroup of string
        | RemoveLastSelectGroup of string
        | ShowErrorMessage of string
        | SetBusy of bool
        | OpenLoginPage
        | SetSearchText of string
        | SetSearchResult of AudioBookItemViewModel []
        | AppendBusyMessage of string




    and [<RequireQualifiedAccess>]
        SideEffect =
        | None
        | Init
        | LoadCurrentAudioBooks
        | LoadOnlineAudioBooks
        | OpenLoginPage
        | ShowErrorMessage of string
        | OnSelectedGroupChanged
        | StartSearch

    let disposables = new System.Collections.Generic.List<IDisposable>()

    let filter selectedGroups (audioBookItems:AudioBookItemViewModel []) =

        //let selectedGroups = [ "Perry Rhodan"; "Perry Rhodan 3000 - 3000" ]

        let audioBooks =
            audioBookItems
            |> Array.map (_.AudioBook)

        let groupedAudioBooks = audioBooks |> Filters.nameFilter

        // filter audio books
        let filteredAb =
            match selectedGroups with
            | [] ->
                (GroupList groupedAudioBooks)
            | _ ->
                ((GroupList groupedAudioBooks) |> Filters.groupsFilter selectedGroups)

        let audioBooks =
            match filteredAb with
            | GroupList _ -> [||]
            | AudioBookList (_, ab) ->
                audioBookItems
                |> Array.filter (fun i -> ab |> Array.exists (fun a -> a.Id = i.AudioBook.Id))

        {|
            FilteredAudioBooks = filteredAb
            CurrentVisibleAudioBooks = audioBooks
            PreviousAvailableGroups = groupedAudioBooks |> Array.map fst |> Array.toList
            AvailableGroups =
                match filteredAb with
                | GroupList grp -> grp |> Array.map fst |> Array.toList
                | _ -> []
        |}


    let init initAudioBooks =
        let filterResult = filter [] initAudioBooks

        let newModel = {
            AudioBookItems = filterResult.CurrentVisibleAudioBooks
            IsLoading = false
            SelectedGroups = []
            SelectedGroupItems = filterResult.FilteredAudioBooks
            PreviousGroups = filterResult.PreviousAvailableGroups
            AvailableGroups = filterResult.AvailableGroups
            SearchText = ""
            BusyMessage = ""
        }

        newModel, if initAudioBooks = [||] then SideEffect.Init else SideEffect.None


    let update msg (state:State) =
        match msg with
        | RunOnlySideEffect sideEffect ->
            state, sideEffect

        | LoadOnlineAudiobooks ->
            state, SideEffect.LoadOnlineAudioBooks

        | AudioBookItemsChanged audioBooks ->
            let filterResult = filter state.SelectedGroups audioBooks
            { state with
                AudioBookItems = filterResult.CurrentVisibleAudioBooks
                IsLoading = false
                SelectedGroupItems = filterResult.FilteredAudioBooks
                PreviousGroups = filterResult.PreviousAvailableGroups
                AvailableGroups = filterResult.AvailableGroups }, SideEffect.None

        | SelectPreviousGroup group ->
            { state with SelectedGroups = [group] }, SideEffect.LoadCurrentAudioBooks
        | AddSelectGroup group ->
            { state with SelectedGroups = state.SelectedGroups @ [group] }, SideEffect.OnSelectedGroupChanged

        | RemoveLastSelectGroup groupName ->
            { state with SelectedGroups = state.SelectedGroups |> List.filter (fun i -> i <> groupName) }, SideEffect.OnSelectedGroupChanged

        | ShowErrorMessage e ->
            state, SideEffect.ShowErrorMessage e
        | SetBusy bstate ->
            { state with IsLoading = bstate; BusyMessage = "" }, SideEffect.None
        | AppendBusyMessage msg ->
            { state with BusyMessage = $"{state.BusyMessage}\r\n{msg}" }, SideEffect.None

        | OpenLoginPage ->
            state, SideEffect.OpenLoginPage
        | SetSearchText s ->
            { state with SearchText = s }, SideEffect.StartSearch
        | SetSearchResult audioBookItemViewModels ->
            { state with AudioBookItems = audioBookItemViewModels }, SideEffect.None


    module SideEffects =
        open PerryRhodan.AudiobookPlayer.Common

        

        


        let runSideEffects (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) =
            let loadCurrentAudiobooks () =
                let audioBooks =
                        AudioBookStore.globalAudiobookStore.Model.Audiobooks
                dispatch <| AudioBookItemsChanged audioBooks



            task {
                if sideEffect = SideEffect.None then
                    return ()
                else
                    dispatch <| SetBusy true
                    do!
                        task {
                            match sideEffect with
                            | SideEffect.None ->
                                return ()

                            | SideEffect.Init ->
                                // listen to global audiobook store
                                disposables.Add
                                    <| AudioBookStore.globalAudiobookStore.Observable.Subscribe(fun m ->
                                        dispatch <| AudioBookItemsChanged m.Audiobooks
                                    )

                            | SideEffect.LoadCurrentAudioBooks ->
                                loadCurrentAudiobooks ()
                                return ()

                            | SideEffect.LoadOnlineAudioBooks ->
                                raise <| NotImplementedException ("View fliegt raus")

                                return ()

                            | SideEffect.OpenLoginPage ->
                                let control = PerryRhodan.AudiobookPlayer.Views.LoginView()
                                let vm = new LoginViewModel()
                                control.DataContext <- vm
                                InteractiveContainer.ShowDialog (control, true)
                                return ()

                            | SideEffect.ShowErrorMessage e ->
                                do! Notifications.showErrorMessage e
                                return ()

                            | SideEffect.OnSelectedGroupChanged ->
                                loadCurrentAudiobooks ()
                                let navService = DependencyService.Get<INavigationService>()
                                let selectedGroups = state.SelectedGroups
                                match selectedGroups with
                                | [] ->
                                    navService.ResetBackbuttonPressed()
                                | _ ->
                                    navService.RegisterBackbuttonPressed (fun () -> dispatch (RemoveLastSelectGroup (List.last selectedGroups)))

                                return ()

                            | SideEffect.StartSearch ->
                                if state.SearchText = "" then
                                    loadCurrentAudiobooks ()
                                else
                                    // search for audio books with the given search text
                                    let filteredAudioBooks =
                                        AudioBookStore.globalAudiobookStore.Model.Audiobooks
                                        |> Array.filter (_.AudioBook.FullName.ToLower().Contains(state.SearchText.ToLower()))

                                    dispatch <| Msg.SetSearchResult filteredAudioBooks
                        }

                    dispatch <| SetBusy false
            }

open BrowserPageOld
open ReactiveElmish.Avalonia
open ReactiveElmish
open Elmish.SideEffect

module private DemoData =
    let designAudioBook = {
            Id = 1
            FullName = "Perry Rhodan 3000 - Mythos Erde"
            EpisodeNo = Some 3000
            EpisodenTitel = "Mythos Erde"
            Group = "Perry Rhodan"
            Picture = Some "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
            Thumbnail = Some "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
            DownloadUrl = None
            ProductSiteUrl = None
            State = {
                Completed = true
                CurrentPosition = None
                Downloaded = true
                DownloadedFolder = None
                LastTimeListend = None
            }
            AmbientColor = Some "#FF0000"
        }

    let designAudioBook2 = {
            Id = 2
            FullName = "Perry Rhodan 3001 - Mythos Erde2"
            EpisodeNo = Some 3000
            EpisodenTitel = "Mythos Erde2"
            Group = "Perry Rhodan"
            Picture = Some "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
            Thumbnail = Some "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
            DownloadUrl = None
            ProductSiteUrl = None
            State = {
                Completed = true
                CurrentPosition = None
                Downloaded = true
                DownloadedFolder = None
                LastTimeListend = None
            }
            AmbientColor = Some "#00FF00"
        }

    let designAudioBook3 = {
            Id = 3
            FullName = "Perry Rhodan 3003 - Mythos Erde2"
            EpisodeNo = Some 3000
            EpisodenTitel = "Mythos Erde3"
            Group = "Perry Rhodan - Dies ist ein ganz langer Titel, der nicht in das Fenster passt"
            Picture = Some "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
            Thumbnail = Some "avares://PerryRhodan.AudiobookPlayer/Assets/AudioBookPlaceholder_Dark.png"
            DownloadUrl = None
            ProductSiteUrl = None
            State = {
                Completed = true
                CurrentPosition = None
                Downloaded = true
                DownloadedFolder = None
                LastTimeListend = None
            }
            AmbientColor = Some "#0000FF"
        }


type BrowserOldViewModel(?audiobookItems) as self =
    inherit ReactiveElmishViewModel()

    let audiobookItems = [||] |> defaultArg audiobookItems
    let init() = init audiobookItems
    let local =
        Program.mkAvaloniaProgrammWithSideEffect init update SideEffects.runSideEffects
        |> Program.mkStore

    let searchStringDebouncer = Extensions.debounce<string>

    do
        do
            self.AddDisposable
                <| AudioBookStore.globalAudiobookStore.Observable.Subscribe(fun s ->
                    local.Dispatch (s.IsLoading |> SetBusy)
                )
            ()

    new () =
        new BrowserOldViewModel([||])

    member this.AudioBooks =
        this.Bind(local, fun s -> ObservableCollection(s.AudioBookItems))

    member this.PreviousGroups:string list  = this.Bind(local, _.PreviousGroups)
    member this.AvailableGroups:string list = this.Bind(local, _.AvailableGroups)
    member this.SelectedGroups:string list  = this.Bind(local, _.SelectedGroups)
    member this.GroupItems                  = this.BindList(local, fun s -> ObservableCollection(match s.SelectedGroupItems with | GroupList grp -> grp | _ -> [||]))
    member this.IsBusy                      = this.BindOnChanged(local, _.IsLoading, _.IsLoading)
    member this.BusyMessage                 = this.BindOnChanged(local, _.BusyMessage, _.BusyMessage)

    member this.BackButtonVisible           = this.Bind(local, fun s -> s.SelectedGroups.Length > 0)
    member this.AudioBookItemsVisible       = this.Bind(local, fun s -> s.AudioBookItems.Length > 0)
    member this.CategoryItemsVisible =
        this.Bind(local, fun s ->
            match s.SelectedGroupItems with
            | GroupList _ -> true
            | _ -> false

        )

    member this.IsEmpty = this.Bind(local, fun s -> s.AudioBookItems.Length = 0 && s.AvailableGroups.Length = 0)

    member this.SearchText
        with get() = "nix"
        and set(value) =
            searchStringDebouncer 1000 (fun s ->
                // protect against null
                let s = if s = null then "" else s
                local.Dispatch <| SetSearchText s
            ) value




    member this.OnInitialized() = local.Dispatch <| RunOnlySideEffect SideEffect.LoadCurrentAudioBooks
    member this.LoadOnlineAudiobooks() = local.Dispatch LoadOnlineAudiobooks
    member this.SelectPreviousGroup(group:string) = local.Dispatch (SelectPreviousGroup group)
    member this.SelectAdditionalGroup(group:string) = local.Dispatch (AddSelectGroup group)
    member this.RemoveLastSelectedGroup () =
        match this.SelectedGroups with
        | [] ->
            ()
        | _ ->
            local.Dispatch <| RemoveLastSelectGroup (local.Model.SelectedGroups |> List.last)

    member this.OnLoaded() =
        // always set backbutton, when enter this page
        let navService = DependencyService.Get<INavigationService>()
        if navService <> Unchecked.defaultof<_> then // because of design time
            let selectedGroups = local.Model.SelectedGroups
            match selectedGroups with
            | [] ->
                navService.ResetBackbuttonPressed()
            | _ ->
                navService.RegisterBackbuttonPressed (fun () -> local.Dispatch (RemoveLastSelectGroup (List.last selectedGroups)))

    member this.GoBackHome() =
        DependencyService.Get<IMainViewModel>().GotoHomePage()


    static member DesignVM =
        new BrowserOldViewModel(
            [|
                AudioBookItemViewModel(DemoData.designAudioBook)
                AudioBookItemViewModel(DemoData.designAudioBook2)
                AudioBookItemViewModel(DemoData.designAudioBook3)
            |])
