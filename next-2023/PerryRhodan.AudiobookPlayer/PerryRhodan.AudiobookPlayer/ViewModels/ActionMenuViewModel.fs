namespace PerryRhodan.AudiobookPlayer.ViewModels

open CherylUI.Controls

open Domain
open ReactiveElmish

module ActionMenu =
    
    type State = {
        Bla:string
    }
    
    
    type Msg =
        | Close
        
        
    [<RequireQualifiedAccess>]
    type SideEffect =
        | None
        | CloseDialog
        
        
    let init () =
        { Bla = "Bla" }, SideEffect.None
        
        
        
    let update (msg:Msg) (state:State) =
        match msg with
        | Close -> state, SideEffect.CloseDialog
        
        
        
    module SideEffects =
        
        let runSideEffects (sideEffect:SideEffect) (state:State) (dispatch:Msg -> unit) =
            task {
                match sideEffect with
                | SideEffect.None ->
                    return ()

                | SideEffect.CloseDialog ->
                    InteractiveContainer.CloseDialog()
                    return ()
            }
    
    
open ReactiveElmish
open ReactiveElmish.Avalonia
open Elmish.SideEffect
open ActionMenu
    
type ActionMenuViewModel(audioBook: AudioBook) =
    inherit ReactiveElmishViewModel()
    
    
    let local =
        Program.mkAvaloniaProgrammWithSideEffect init update SideEffects.runSideEffects
        |> Program.mkStore
        
    member this.CloseDialog() = local.Dispatch Close
        
    static member DesignVM = new ActionMenuViewModel(Design.stub)

