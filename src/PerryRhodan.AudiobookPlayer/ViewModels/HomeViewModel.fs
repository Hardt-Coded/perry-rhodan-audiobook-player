﻿namespace PerryRhodan.AudiobookPlayer.ViewModels

open System
open System.Collections.ObjectModel
open Domain
open PerryRhodan.AudiobookPlayer.ViewModel


module HomePage =

    type State = {
        Audiobooks: AudioBookItemViewModel array
        LastTimeListenedAudioBook: AudioBookItemViewModel option
        IsLoading:bool
    }

    type Msg =
        | RunOnlySideEffect of SideEffect
        | AudioBooksLoaded of AudioBookItemViewModel array
        | SetBusy of bool
        | OnError of string


    and [<RequireQualifiedAccess>]
        SideEffect =
        | None
        | Init
        | LoadAudioBooks


    let disposables = new System.Collections.Generic.List<IDisposable>()

    let emptyModel = { Audiobooks = [||] ; LastTimeListenedAudioBook = None; IsLoading = true }

    let init () =
        { emptyModel with IsLoading = true }, SideEffect.Init


    let update (msg:Msg) (state:State) =
        match msg with
        | RunOnlySideEffect sideEffect ->
            state, sideEffect

        | AudioBooksLoaded audiobooks ->
            let lastListenAudioBook =
                audiobooks
                |> Array.filter (fun i -> i.DownloadState = AudioBookItem.Downloaded)
                |> Array.sortByDescending (_.AudioBook.State.LastTimeListend)
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

        | SetBusy isBusy ->
            { state with IsLoading = isBusy }, SideEffect.None

        | OnError error ->
            { state with IsLoading = false }, SideEffect.None




    module SideEffects =


        let runSideEffects (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) =
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
                                        dispatch <| AudioBooksLoaded m.Audiobooks
                                    )

                            | SideEffect.LoadAudioBooks ->
                                try
                                    let audioBooks =
                                        AudioBookStore.globalAudiobookStore.Model.Audiobooks

                                    dispatch <| AudioBooksLoaded audioBooks
                                    return ()
                                with
                                | ex ->
                                    dispatch <| OnError ex.Message
                                    return ()


                        }

                    dispatch <| SetBusy false
            }



open HomePage
open ReactiveElmish.Avalonia
open ReactiveElmish
open Elmish.SideEffect

type HomeViewModel() =
    inherit ReactiveElmishViewModel()

    let local =
        Program.mkAvaloniaProgrammWithSideEffect init update SideEffects.runSideEffects
        |> Program.mkStore

    do
        let a = 1
        ()

    member this.AudioBooks =
        this.Bind(local, fun s -> ObservableCollection(s.Audiobooks))

    member this.LastListendAudiobook =
        this.Bind(local, fun i -> i.LastTimeListenedAudioBook |> Option.defaultValue AudioBookItemViewModel.DesignVM)

    member this.LastTimeListenedAudioBook =
        this.Bind(local, _.LastTimeListenedAudioBook)

    member this.HasLastListenedAudioBook =
        this.Bind(local, _.LastTimeListenedAudioBook.IsSome)

    member this.IsLoading =
        this.Bind(local, _.IsLoading)

    member this.SelectorValues =
        [|
            (1,"eins")
            (2,"zwei")
            (3,"drei")
            (4,"vier")
            (5,"fünf")
            (6,"sechs")
            (7,"sieben")
            (8,"acht")
            (9,"neun")
            (1,"zehn")
        |]

    member this.SelectorValue
        with get():int = 0
        and set(value:int) =
            ()
            
    member this.IsEmpty = this.Bind(local, fun i -> i.Audiobooks.Length = 0 && i.LastTimeListenedAudioBook.IsNone)



    static member DesignVM = new HomeViewModel()

