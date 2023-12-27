namespace Elmish.SideEffect

open Elmish
open ReactiveElmish.Avalonia


[<RequireQualifiedAccess>]
module Program =
    
    let mkAvaloniaProgrammWithSideEffect init update runSideEffect =
        
        let runSideEffect sideEffect state =
            [
                fun dispatch ->
                    async {
                        do! runSideEffect sideEffect state dispatch |> Async.AwaitTask
                    } |> Async.StartImmediate
            ]
        
        let init () =
            let (state, sideEffect) = init ()
            state, runSideEffect sideEffect state
            
        let update msg state =
            let (state, sideEffect) = update msg state
            state, runSideEffect sideEffect state
        
        Program.mkAvaloniaProgram init update
        
        



