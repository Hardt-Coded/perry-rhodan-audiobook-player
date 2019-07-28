module AudioBookItemProcessor

open Domain


type private Msg =
    | GetAudioBookItem of (string * AsyncReplyChannel<AudioBookItem.Model>)
    | GetAudioBookItems of (string[] * AsyncReplyChannel<AudioBookItem.Model[]>)
    | GetDownloadingAndDownloadedAudioBookItems of AsyncReplyChannel<AudioBookItem.Model[]>
    | InsertAudioBooks of AudioBookItem.Model []
    | UpdateAudioBookItem of AudioBookItem.Model
    | UpdateAudioBook of AudioBook
    

let private abItemErrorEvent = Event<exn>()

let abItemOnError = abItemErrorEvent.Publish

let private abItemUpdatedEvent = Event<AudioBookItem.Model>()

let onAbItemUpdated = abItemUpdatedEvent.Publish

let private abItemProcessor = 
    MailboxProcessor<Msg>.Start(
        fun inbox ->
            try
                // read audiobooks on start from db
                let audioBooks =
                    Services.DataBase.loadAudioBooksStateFile ()
                    |> Async.RunSynchronously
                    |> Array.Parallel.map (fun i -> 
                        let newItemModel,_,_ = AudioBookItem.init i
                        newItemModel
                    )
            
                let rec loop (state:AudioBookItem.Model []) =
                    async {
                        let! msg = inbox.Receive()
                        match msg with
                        | GetAudioBookItem (fullName,replyChannel) ->
                            let item =
                                state 
                                |> Array.find (fun i -> i.AudioBook.FullName = fullName)
                            replyChannel.Reply(item)
                            return! (loop state)
                        | GetAudioBookItems (fullNames,replyChannel) ->
                            let items =
                                state 
                                |> Array.filter (fun i -> 
                                    fullNames |> Array.exists (fun fn -> i.AudioBook.FullName = fn)
                                )
                            replyChannel.Reply(items)
                            return! (loop state)

                        | GetDownloadingAndDownloadedAudioBookItems replyChannel ->
                            let items =
                                state 
                                |> Array.filter (fun i -> i.IsDownloading || i.AudioBook.State.Downloaded || i.QueuedToDownload)
                            replyChannel.Reply(items)
                            return! (loop state)

                        | UpdateAudioBookItem item ->
                            let newState =
                                state 
                                |> Array.Parallel.map (fun i -> 
                                    if i.AudioBook.FullName = item.AudioBook.FullName then
                                        item
                                    else
                                        i
                                )

                            abItemUpdatedEvent.Trigger(item)        
                            return! (loop newState)

                        | UpdateAudioBook ab ->
                            let abItem =
                                state |> Array.find (fun i -> i.AudioBook.FullName = ab.FullName)
                            let newAbItem = {abItem with AudioBook = ab }
                            let newState =
                                state 
                                |> Array.Parallel.map (fun i -> 
                                    if i.AudioBook.FullName = newAbItem.AudioBook.FullName then
                                        newAbItem
                                    else
                                        i
                                )

                            abItemUpdatedEvent.Trigger(newAbItem)        
                            return! (loop newState)

                        | InsertAudioBooks audioBooks ->
                            let newState =
                                state 
                                |> Array.append audioBooks
                                |> Array.sortBy (fun i-> i.AudioBook.FullName)
                                
                            return! (loop newState)
                    }
                
                loop audioBooks
            with
            | _ as ex ->
                abItemErrorEvent.Trigger(ex)
                
                failwith "machine down!"
                
    )


let getAudioBookItem fullname =
    let msg replyChannel =
        GetAudioBookItem (fullname,replyChannel)
    abItemProcessor.PostAndAsyncReply(msg)


let getAudioBookItems fullname =
    let msg replyChannel =
        GetAudioBookItems (fullname,replyChannel)
    abItemProcessor.PostAndAsyncReply(msg)

let getDownloadingAndDownloadedAudioBookItems () =
    abItemProcessor.PostAndAsyncReply(GetDownloadingAndDownloadedAudioBookItems)

let updateAudiobookItem item =
    abItemProcessor.Post(UpdateAudioBookItem item)


let insertAudiobookItems items =
    abItemProcessor.Post(InsertAudioBooks items)

let insertAudiobooks items =
    let items = 
        items
        |> Array.map (fun i ->
            let mdl,_,_ = AudioBookItem.init i
            mdl
        )
    abItemProcessor.Post(InsertAudioBooks items)


let updateUnderlyingAudioBookInItem audiobook =
    abItemProcessor.Post(UpdateAudioBook audiobook)

