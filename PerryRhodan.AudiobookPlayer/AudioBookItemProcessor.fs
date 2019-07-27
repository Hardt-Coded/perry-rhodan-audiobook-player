module AudioBookItemProcessor


type Msg =
    | GetAudioBookItem of (string * AsyncReplyChannel<AudioBookItem.Model>)
    | GetAudioBookItems of (string[] * AsyncReplyChannel<AudioBookItem.Model[]>)
    | InsertAudioBooks of AudioBookItem.Model []
    | UpdateAudioBookItem of AudioBookItem.Model
    

let abItemErrorEvent = Event<exn>()

let abItemOnError = abItemErrorEvent.Publish

let abItemUpdatedEvent = Event<AudioBookItem.Model>()

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

                        | InsertAudioBooks audioBooks ->
                            let newState =
                                state |> Array.append audioBooks
                                
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


let updateAudiobookItem item =
    abItemProcessor.Post(UpdateAudioBookItem item)


let insertAudiobookItems items =
    abItemProcessor.Post(InsertAudioBooks items)

