namespace PerryRhodan.AudiobookPlayer.ViewModels

open Avalonia
open Avalonia.Controls
open CherylUI.Controls
open Domain
open Elmish
open Material.Dialog


module AudioBookItem =
    // State
    type State = { 
        AudioBook: AudioBook
        DownloadState: DownloadState
        ListenState: ListenState
        AudioFileInfos:AudioBookAudioFilesInfo option
    }
    
    and DownloadState =
        | NotDownloaded
        | Queued
        | Downloading of (int * int)
        | Downloaded


    and ListenState =
        | Unlistend
        | InProgress of Domain.AudioBookPosition
        | Listend
        
        
    type Msg =
        | OpenAudioBookActionMenu
        | AddToDownloadQueue
        | RemoveFromDownloadQueue
        
        | DeleteAudiobook
        | AudioBookDeleted
        | MarkAudioBookAsListend
        | UnmarkAudioBookAsListend
        | UpdateDownloadProgress of (int * int)
        | OpenAudioBookPlayer
        | OpenAudioBookDetail
        | DeleteItemFromDb
        | DeletedFromDb

        | ShowMetaData

        | DownloadCompleted of Services.WebAccess.Downloader.DownloadResult
        
        
    [<RequireQualifiedAccess>]
    type SideEffect =
        | OpenAudioBookActionMenu
        | None
        


    
    let init audiobook = 
        { 
            AudioBook = audiobook
            DownloadState = if audiobook.State.Downloaded then Downloaded else NotDownloaded
            ListenState = 
                match audiobook.State.Completed, audiobook.State.CurrentPosition with
                | true, _           -> Listend
                | false, Some pos   -> InProgress pos
                | false, None       -> Unlistend
            AudioFileInfos = Services.DataBase.getAudioBookFileInfoTimeout 100 audiobook.Id // Todo: unbedingt ändern!
        }
        
        
        
    let update msg state =
        match msg with
        | OpenAudioBookActionMenu ->
            state, SideEffect.OpenAudioBookActionMenu
        | _ -> state, SideEffect.None
        
        
        
    module SideEffects =
        
        let runSideEffects (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) =
            task {
                match sideEffect with
                | SideEffect.None ->
                    return ()

                | SideEffect.OpenAudioBookActionMenu ->
                    let control = PerryRhodan.AudiobookPlayer.Views.ActionMenuView()
                    let vm = new ActionMenuViewModel(state.AudioBook)
                    control.DataContext <- vm
                    InteractiveContainer.ShowDialog (control, true)
                    
                    return ()
            }
        
        

open AudioBookItem
open ReactiveElmish
open ReactiveElmish.Avalonia
open Elmish.SideEffect
open System

     
    
type AudioBookItemViewModel(audiobook: Domain.AudioBook option) =
    inherit ReactiveElmishViewModel()
    
    let init () =
        init audiobook.Value, SideEffect.None
    
    let local =
        Program.mkAvaloniaProgrammWithSideEffect init update SideEffects.runSideEffects
        |> Program.mkStore
    
    member this.AudioBook = this.Bind(local, _.AudioBook)
    member this.DownloadState = this.Bind(local, _.DownloadState)
    member this.ListenState = this.Bind(local, _.ListenState)
    member this.AudioFileInfos = this.Bind(local, _.AudioFileInfos)
    member this.OpenDialog() = local.Dispatch (OpenAudioBookActionMenu)
    
    member this.Title =
        match audiobook with
        | None -> ""
        | Some _ -> this.Bind(local, _.AudioBook.FullName)
        
        
    static member DesignVM = new AudioBookItemViewModel(Design.stub)
        
        
