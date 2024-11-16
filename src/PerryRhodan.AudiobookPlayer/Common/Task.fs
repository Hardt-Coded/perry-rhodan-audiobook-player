﻿module PerryRhodan.AudiobookPlayer.Common

//
// // FShharpPlus Task Extensions
//
//
// /// Additional operations on Task<'T>
// [<RequireQualifiedAccess>]
// module Task =
//
//     open System
//     open System.Threading.Tasks
//     
//     let private (|Canceled|Faulted|Completed|) (t: Task<'a>) =
//         if t.IsCanceled then Canceled
//         else if t.IsFaulted then Faulted t.Exception
//         else Completed t.Result
//         
//     let private (|TCanceled|TFaulted|TCompleted|) (t: Task) =
//         if t.IsCanceled then TCanceled
//         else if t.IsFaulted then TFaulted t.Exception
//         else TCompleted
//
//     
//     let tmap (f: unit -> 'U) (source: Task) : Task<'U> =
//         if source.Status = TaskStatus.RanToCompletion then
//             try Task.FromResult (f ())
//             with e ->
//                 let tcs = TaskCompletionSource<'U> ()
//                 tcs.SetException e
//                 tcs.Task
//         else
//             let tcs = TaskCompletionSource<'U> ()
//             if source.Status = TaskStatus.Faulted then
//                 tcs.SetException source.Exception.InnerExceptions
//                 tcs.Task
//             elif source.Status = TaskStatus.Canceled then
//                 tcs.SetCanceled ()
//                 tcs.Task
//             else
//                 let k = function
//                     | TCanceled    -> tcs.SetCanceled ()
//                     | TFaulted e   -> tcs.SetException e.InnerExceptions
//                     | TCompleted   ->
//                         try tcs.SetResult (f ())
//                         with e -> tcs.SetException e
//                 source.ContinueWith k |> ignore
//                 tcs.Task
//                 
//     /// <summary>Creates a task workflow from 'source' another, mapping its result with 'f'.</summary>
//     let map (f: 'T -> 'U) (source: Task<'T>) : Task<'U> =
//         if source.Status = TaskStatus.RanToCompletion then
//             try Task.FromResult (f source.Result)
//             with e ->
//                 let tcs = TaskCompletionSource<'U> ()
//                 tcs.SetException e
//                 tcs.Task
//         else
//             let tcs = TaskCompletionSource<'U> ()
//             if source.Status = TaskStatus.Faulted then
//                 tcs.SetException source.Exception.InnerExceptions
//                 tcs.Task
//             elif source.Status = TaskStatus.Canceled then
//                 tcs.SetCanceled ()
//                 tcs.Task
//             else
//                 let k = function
//                     | Canceled    -> tcs.SetCanceled ()
//                     | Faulted e   -> tcs.SetException e.InnerExceptions
//                     | Completed r ->
//                         try tcs.SetResult (f r)
//                         with e -> tcs.SetException e
//                 source.ContinueWith k |> ignore
//                 tcs.Task
//
//     /// <summary>Creates a task workflow from two workflows 'x' and 'y', mapping its results with 'f'.</summary>
//     /// <remarks>Workflows are run in sequence.</remarks>
//     /// <param name="f">The mapping function.</param>
//     /// <param name="x">First task workflow.</param>
//     /// <param name="y">Second task workflow.</param>
//     let map2 (f: 'T -> 'U -> 'V) (x: Task<'T>) (y: Task<'U>) : Task<'V> =
//         if x.Status = TaskStatus.RanToCompletion && y.Status = TaskStatus.RanToCompletion then
//             try Task.FromResult (f x.Result y.Result)
//             with e ->
//                 let tcs = TaskCompletionSource<'V> ()
//                 tcs.SetException e
//                 tcs.Task
//         else
//             let tcs = TaskCompletionSource<'V> ()
//             match x.Status, y.Status with
//             | TaskStatus.Canceled, _ -> tcs.SetCanceled ()
//             | TaskStatus.Faulted, _  -> tcs.SetException x.Exception.InnerExceptions
//             | _, TaskStatus.Canceled -> tcs.SetCanceled ()
//             | _, TaskStatus.Faulted  -> tcs.SetException y.Exception.InnerExceptions
//             | TaskStatus.RanToCompletion, _ ->
//                 let k = function
//                     | Canceled    -> tcs.SetCanceled ()
//                     | Faulted e   -> tcs.SetException e.InnerExceptions
//                     | Completed r ->
//                         try tcs.SetResult (f x.Result r)
//                         with e -> tcs.SetException e
//                 y.ContinueWith k |> ignore
//             | _, TaskStatus.RanToCompletion ->
//                 let k = function
//                     | Canceled    -> tcs.SetCanceled ()
//                     | Faulted e   -> tcs.SetException e.InnerExceptions
//                     | Completed r ->
//                         try tcs.SetResult (f r y.Result)
//                         with e -> tcs.SetException e
//                 x.ContinueWith k |> ignore
//             | _, _ ->
//                 x.ContinueWith (
//                     function
//                     | Canceled    -> tcs.SetCanceled ()
//                     | Faulted e   -> tcs.SetException e.InnerExceptions
//                     | Completed r ->
//                         y.ContinueWith (
//                                 function
//                                 | Canceled     -> tcs.SetCanceled ()
//                                 | Faulted e    -> tcs.SetException e.InnerExceptions
//                                 | Completed r' ->
//                                     try tcs.SetResult (f r r')
//                                     with e -> tcs.SetException e
//                         ) |> ignore) |> ignore
//             tcs.Task
//
//     /// <summary>Creates a task workflow that is the result of applying the resulting function of a task workflow
//     /// to the resulting value of another task workflow</summary>
//     /// <param name="f">Task workflow returning a function</param>
//     /// <param name="x">Task workflow returning a value</param>
//     let apply (f: Task<'T->'U>) (x: Task<'T>) : Task<'U> =
//         if f.Status = TaskStatus.RanToCompletion && x.Status = TaskStatus.RanToCompletion then
//             try Task.FromResult (f.Result x.Result)
//             with e ->
//                 let tcs = TaskCompletionSource<'U> ()
//                 tcs.SetException e
//                 tcs.Task
//         else
//             let tcs = TaskCompletionSource<'U> ()
//             match f.Status, x.Status with
//             | TaskStatus.Canceled, _ -> tcs.SetCanceled ()
//             | TaskStatus.Faulted, _  -> tcs.SetException f.Exception.InnerExceptions
//             | _, TaskStatus.Canceled -> tcs.SetCanceled ()
//             | _, TaskStatus.Faulted  -> tcs.SetException x.Exception.InnerExceptions
//             | TaskStatus.RanToCompletion, _ ->
//                 let k = function
//                     | Canceled    -> tcs.SetCanceled ()
//                     | Faulted e   -> tcs.SetException e.InnerExceptions
//                     | Completed r ->
//                         try tcs.SetResult (f.Result r)
//                         with e -> tcs.SetException e
//                 x.ContinueWith k |> ignore
//             | _, TaskStatus.RanToCompletion ->
//                 let k = function
//                     | Canceled    -> tcs.SetCanceled ()
//                     | Faulted e   -> tcs.SetException e.InnerExceptions
//                     | Completed r ->
//                         try tcs.SetResult (r x.Result)
//                         with e -> tcs.SetException e
//                 f.ContinueWith k |> ignore
//             | _, _ ->
//                 f.ContinueWith (
//                     function
//                     | Canceled    -> tcs.SetCanceled ()
//                     | Faulted e   -> tcs.SetException e.InnerExceptions
//                     | Completed r ->
//                         x.ContinueWith (
//                                 function
//                                 | Canceled     -> tcs.SetCanceled ()
//                                 | Faulted e    -> tcs.SetException e.InnerExceptions
//                                 | Completed r' ->
//                                     try tcs.SetResult (r r')
//                                     with e -> tcs.SetException e
//                         ) |> ignore) |> ignore
//             tcs.Task
//
//     /// <summary>Creates a task workflow from two workflows 'x' and 'y', tupling its results.</summary>
//     let zip (x: Task<'T>) (y: Task<'U>) : Task<'T * 'U> =
//         if x.Status = TaskStatus.RanToCompletion && y.Status = TaskStatus.RanToCompletion then
//             Task.FromResult (x.Result, y.Result)
//         else
//             let tcs = TaskCompletionSource<'T * 'U> ()
//             match x.Status, y.Status with
//             | TaskStatus.Canceled, _ -> tcs.SetCanceled ()
//             | TaskStatus.Faulted, _  -> tcs.SetException x.Exception.InnerExceptions
//             | _, TaskStatus.Canceled -> tcs.SetCanceled ()
//             | _, TaskStatus.Faulted  -> tcs.SetException y.Exception.InnerExceptions
//             | TaskStatus.RanToCompletion, _ ->
//                 let k = function
//                     | Canceled    -> tcs.SetCanceled ()
//                     | Faulted e   -> tcs.SetException e.InnerExceptions
//                     | Completed r -> tcs.SetResult (x.Result, r)
//                 y.ContinueWith k |> ignore
//             | _, TaskStatus.RanToCompletion ->
//                 let k = function
//                     | Canceled    -> tcs.SetCanceled ()
//                     | Faulted e   -> tcs.SetException e.InnerExceptions
//                     | Completed r -> tcs.SetResult (r, y.Result)
//                 x.ContinueWith k |> ignore
//             | _, _ ->
//                 x.ContinueWith (
//                     function
//                     | Canceled    -> tcs.SetCanceled ()
//                     | Faulted e   -> tcs.SetException e.InnerExceptions
//                     | Completed r ->
//                         y.ContinueWith (function
//                             | Canceled     -> tcs.SetCanceled ()
//                             | Faulted e    -> tcs.SetException e.InnerExceptions
//                             | Completed r' -> tcs.SetResult (r, r')) |> ignore) |> ignore
//             tcs.Task
//
//     /// Flattens two nested tasks into one.
//     let join (source: Task<Task<'T>>) : Task<'T> = source.Unwrap()
//     
//     /// <summary>Creates a task workflow from 'source' workflow, mapping and flattening its result with 'f'.</summary>
//     let bind (f: 'T -> Task<'U>) (source: Task<'T>) : Task<'U> = source |> map f |> join
//     
//     /// <summary>Creates a task that ignores the result of the source task.</summary>
//     /// <remarks>It can be used to convert non-generic Task to unit Task.</remarks>
//     let ignore (task: Task) =
//         if task.Status = TaskStatus.RanToCompletion then Task.FromResult ()
//         else
//             let tcs = TaskCompletionSource<unit> ()
//             if task.Status = TaskStatus.Faulted then
//                 tcs.SetException task.Exception.InnerExceptions |> ignore
//             elif task.Status = TaskStatus.Canceled then
//                 tcs.SetCanceled ()
//             else
//                 let k (t: Task) : unit =
//                     if t.IsCanceled  then tcs.SetCanceled () |> ignore
//                     elif t.IsFaulted then tcs.SetException t.Exception |> ignore
//                     else tcs.SetResult ()
//                 task.ContinueWith k |> ignore
//             tcs.Task
