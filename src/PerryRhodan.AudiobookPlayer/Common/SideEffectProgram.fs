namespace Elmish.SideEffect

open ReactiveElmish.Avalonia
open Elmish


[<RequireQualifiedAccess>]
module Program =

    let mkAvaloniaProgrammWithSideEffect init update runSideEffect =



        let runSideEffect sideEffect state =
            [
                fun dispatch ->
                    async {
                        try
                            do! runSideEffect sideEffect state dispatch |> Async.AwaitTask
                        with
                        | ex ->
                            // log
                            let props =
                                [
                                    "sideEffect", sideEffect.ToString()
                                    "state", state.ToString()
                                ] |> Map.ofList
                            Global.telemetryClient.TrackException(ex, props)
                            return ()

                    } |> Async.StartImmediate
            ]

        let init () =
            let state, sideEffect = init ()
            state, runSideEffect sideEffect state

        let update msg state =
            let state, sideEffect = update msg state
            state, runSideEffect sideEffect state

        Program.mkAvaloniaProgram init update







