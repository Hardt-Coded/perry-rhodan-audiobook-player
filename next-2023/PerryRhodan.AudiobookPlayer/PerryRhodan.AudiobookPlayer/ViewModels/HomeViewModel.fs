namespace PerryRhodan.AudiobookPlayer.ViewModels

open System
open System.Collections.ObjectModel
open Domain
open Microsoft.FSharp.Linq




module HomePage =

    type State = {
        Audiobooks: AudioBookItemViewModel array
        LastTimeListenedAudioBook: AudioBookItemViewModel option
        IsLoading:bool
    }

    type Msg = 
        //| LoadAudioBooks
        | AudioBooksLoaded of AudioBookItemViewModel array
        | ChangeBusyState of bool
        | OnError of string
        
        
    [<RequireQualifiedAccess>]
    type SideEffect =
        | None
        | LoadAudioBooks
        
        
    let emptyModel = { Audiobooks = [||] ; LastTimeListenedAudioBook = None; IsLoading = true }
    
    let init () =
        { emptyModel with IsLoading = true }, SideEffect.LoadAudioBooks
        
        
    let update (msg:Msg) (state:State) =
        match msg with
        //| LoadAudioBooks -> { state with IsLoading = true }, SideEffect.LoadAudioBooks
        | AudioBooksLoaded audiobooks ->
            let lastListenAudioBook =
                audiobooks 
                |> Array.filter (fun i -> i.DownloadState = AudioBookItem.Downloaded)
                |> Array.sortByDescending (fun i -> i.AudioBook.State.LastTimeListend) 
                |> Array.tryHead
                |> Option.bind (fun i -> 
                    match i.AudioBook.State.LastTimeListend with
                    | None -> None
                    | Some _ -> Some i
                )
                
            let newState = {
                Audiobooks = 
                    audiobooks
                    |> Array.filter (fun x -> match x.DownloadState with | AudioBookItem.Downloaded | AudioBookItem.Downloading _ -> true | _ -> false)
                    |> Array.filter (fun i -> 
                        lastListenAudioBook 
                        |> Option.map (fun o -> o.AudioBook.Id <> i.AudioBook.Id)
                        |> Option.defaultValue true
                    )
                LastTimeListenedAudioBook = lastListenAudioBook
                IsLoading = false
            }
            newState, SideEffect.None
            
        | ChangeBusyState isBusy ->
            { state with IsLoading = isBusy }, SideEffect.None
            
        | OnError error ->
            { state with IsLoading = false }, SideEffect.None
        
    
    
    
    module SideEffects =
        
        open System.Threading.Tasks
        
        let runSideEffects (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) =
            task {
                match sideEffect with
                | SideEffect.None ->
                    return ()
                    
                | SideEffect.LoadAudioBooks ->
                    try
                        let! ab = Services.DataBase.loadAudioBooksStateFile ()
                        let getRandomAudioBook() =
                            {
                                AudioBook.Empty with
                                    Id = 1000
                                    FullName = $"""Random AudioBook {Guid.NewGuid().ToString("N")}"""
                                    EpisodenTitel = $"""Episode {Guid.NewGuid().ToString("N")}""" 
                                    State = {
                                        AudioBook.Empty.State with
                                            Downloaded = true
                                            CurrentPosition = Some {AudioBookPosition.Filename = ""; Position = TimeSpan.FromMinutes 1 }
                                    }
                            }
                            
                        let randomAudioBooks =
                            Seq.init 100 (fun i -> getRandomAudioBook())
                            |> Seq.toArray
                            |> Array.map (fun i -> new AudioBookItemViewModel(Some i))
                            
                        let abVm =
                            ab
                            |> Array.map (fun i -> new AudioBookItemViewModel(Some i))
                        
                        dispatch <| AudioBooksLoaded randomAudioBooks
                        return ()
                    with
                    | ex ->
                        dispatch <| OnError ex.Message
                        return ()
                    
            }
            
            

open HomePage
open ReactiveElmish.Avalonia
open ReactiveElmish
open Elmish.SideEffect
open System

type HomeViewModel() =
    inherit ReactiveElmishViewModel()
    
    let local =
        Program.mkAvaloniaProgrammWithSideEffect init update SideEffects.runSideEffects
        |> Program.mkStore
        
        
    member this.AudioBooks =
        this.Bind(local, fun s -> ObservableCollection(s.Audiobooks))
    
    member this.LastTimeListenedAudioBook =
        this.Bind(local, _.LastTimeListenedAudioBook)
    
    member this.HasLastListenedAudioBook =
        this.Bind(local, fun s -> s.LastTimeListenedAudioBook.IsSome)
        
    member this.IsLoading =
        this.Bind(local, fun s -> s.IsLoading)
        
        
    static member DesignVM = new HomeViewModel()
    

