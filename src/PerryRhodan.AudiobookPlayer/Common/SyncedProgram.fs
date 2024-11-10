module Elmish.SyncedProgram

open System
open System.Threading.Tasks
open Elmish
open Microsoft.Maui.ApplicationModel


type TaskDispatch<'Msg> = 'Msg -> Task<unit>
type TaskEffect<'Msg> = TaskDispatch<'Msg> -> Task<unit>
type TaskCmd<'Msg> = TaskEffect<'Msg> list
type SyncedProgram<'Model, 'Msg> = (unit -> 'Model * TaskCmd<'Msg>) * ('Msg -> 'Model -> 'Model * TaskCmd<'Msg>)
type RelyMsg<'Msg> = 'Msg * AsyncReplyChannel<unit>

type ISyncedStore<'Model, 'Msg> =
    abstract member Dispatch : 'Msg -> Task<unit>
    abstract member Model : 'Model
    abstract member Observable : IObservable<'Model>


let runOnMainThread (fn: unit -> Task<'a>) =
    let func: Func<Task<'a>> = Func<Task<'a>>(fn)
    MainThread.InvokeOnMainThreadAsync<'a>(funcTask=func)


[<RequireQualifiedAccess>]
module Program =
    let mkSyncedStore (program:SyncedProgram<'Model, 'Msg>) =
        let init, update = program
        let modelChanged = new Event<'Model>()
        let mutable currentModel = Unchecked.defaultof<_>

        let agent = MailboxProcessor<RelyMsg<'Msg>>.Start(fun inbox ->

            let rec runMsg msg =
                task {
                    try
                        // Update the model and get the next command.
                        let (newModel, cmds) = update msg currentModel
                        currentModel <- newModel
                        modelChanged.Trigger newModel
                        for cmd in cmds do
                            //let dispatch msg = (fun () -> runMsg msg newModel) |> runOnMainThread
                            do! cmd runMsg |> Async.AwaitTask
                    with
                    | ex ->
                        // log
                        Global.telemetryClient.TrackException ex
                        #if DEBUG
                        System.Diagnostics.Trace.WriteLine($"Exception: {ex.Message}")
                        #endif

                }
            // Recursive loop to process incoming messages.
            let rec loop () = async {
                let! (msg, reply) = inbox.Receive()
                do! (fun () -> runMsg msg) |> runOnMainThread |> Async.AwaitTask
                modelChanged.Trigger currentModel
                reply.Reply ()
                return! loop ()
            }

            async {
                // Start the loop with the initial model.
                let (initModel, initCmds) = init()
                currentModel <- initModel
                modelChanged.Trigger initModel
                for cmd in initCmds do
                    let dispatch msg = (fun () -> runMsg msg) |> runOnMainThread
                    do! cmd dispatch |> Async.AwaitTask
                return! loop ()
            })

        {
            new ISyncedStore<'Model, 'Msg> with
                member this.Dispatch (msg:'Msg) =
                    async {
                        let! _ = agent.PostAndAsyncReply (fun reply -> msg, reply)
                        return ()
                    } |> Async.StartAsTask


                member this.Model with get() = currentModel
                member this.Observable = modelChanged.Publish :> IObservable<'Model>
        }

    let syncedProgrammWithSideEffect init update runSideEffect : SyncedProgram<'Model, 'Msg> =

        let runSideEffect sideEffect state : TaskCmd<'Msg> =
            [
                fun dispatch ->
                    task {
                        try
                            do! runSideEffect sideEffect state dispatch
                        with
                        | ex ->
                            // log
                            Global.telemetryClient.TrackException ex
                            #if DEBUG
                            System.Diagnostics.Trace.WriteLine($"Exception: {ex.Message}")
                            #endif
                            raise ex
                            return ()
                    }
            ]

        let init () =
            let state, sideEffect = init ()
            state, runSideEffect sideEffect state

        let update msg state =
            let state, sideEffect = update msg state
            state, runSideEffect sideEffect state

        init, update


    let withSyncedTrace fn (program:SyncedProgram<'Model, 'Msg>) =
        let init, update = program
        let traceInit () =
            let initModel,cmd = init ()
            fn Unchecked.defaultof<'Msg> initModel
            initModel,cmd

        let traceUpdate msg model =
            let newModel,cmd = update msg model
            fn msg newModel
            newModel,cmd

        traceInit, traceUpdate



