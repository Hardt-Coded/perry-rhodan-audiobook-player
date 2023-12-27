module PerryRhodan.AudiobookPlayer.ViewModels.App

open Elmish
open ReactiveElmish.Avalonia


type Model = {
    View:View
}

    
and [<RequireQualifiedAccess>] View =
    | HomeView
    | PlayerView


type Msg =
    | SetView of View
    | GoHome
    
  
[<RequireQualifiedAccess>]  
type SideEffect =
    | None
  
    
    
let init () =
    { View = View.HomeView }, SideEffect.None
    
    
let update msg state =
    match msg with
    | SetView view ->
        { state with View = view }, SideEffect.None
    | GoHome ->
        { state with View = View.HomeView }, SideEffect.None
    
    
let runSideEffect sideEffect state dispatch =
    task {
        match sideEffect with
        | SideEffect.None ->
            return ()    
    }
    
    
    
open Elmish.SideEffect  
    
let app =
    Program.mkAvaloniaProgrammWithSideEffect init update runSideEffect
    |> Program.withErrorHandler (fun (_, ex) -> printfn $"Error: {ex.Message}")
    //|> Program.withConsoleTrace
    |> Program.mkStore



