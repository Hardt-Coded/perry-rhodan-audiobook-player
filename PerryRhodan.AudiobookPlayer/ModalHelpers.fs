module ModalHelpers

open Fabulous
open Xamarin.Forms
open Common.ModalBaseHelpers
open Fabulous.XamarinForms


module ModalManager =
    
    let shellRef: ViewRef<Shell> = ViewRef<Shell>()

    let browserRef: ViewRef<CustomContentPage> = ViewRef<CustomContentPage>()


    type Appearence =
        | Shell
        | BrowserPage

    type PushModelInput = {
        Appearence:Appearence
        UniqueId:string
        CloseEvent: unit -> unit
        Page:ViewElement
    }

    type private PushedPage = {
        UniqueId:string
        Page:ViewElement
        ContentPage: ContentPage
        Appearence:Appearence
    }

    type private PushModalState = {
        PushedPages: PushedPage list
    }


    type private Msg =
        | PushOrUpdateModal of PushModelInput
        | RemovePage of string
        | RemoveLastModal
        | GetLastPageId of AsyncReplyChannel<string>
        | CleanupPageStack


    let private modalManager = lazy( 
        MailboxProcessor<Msg>.Start(
            fun inbox ->
                let rec loop (state:PushModalState) =
                    async {
                        let! msg = inbox.Receive()
                        match msg with

                        | PushOrUpdateModal input ->

                            let pageToPush =
                                state.PushedPages
                                |> List.tryFind (fun i -> i.UniqueId = input.UniqueId)

                            match pageToPush with
                            | Some pageToPush ->
                                Device.BeginInvokeOnMainThread(
                                    fun () ->
                                        input.Page.UpdateIncremental(pageToPush.Page, pageToPush.ContentPage)
                                )
                                
                                let newPageToPush = { pageToPush with Page = input.Page }
                                let newPushedPages = 
                                    state.PushedPages 
                                    |> List.map (fun i -> if i.UniqueId = pageToPush.UniqueId then newPageToPush else i)

                                let newState = { state with PushedPages = newPushedPages }
                                return! loop newState

                            | None ->
                                let pageToPush = input.Page.Create() :?> ContentPage
                                pageToPush.Disappearing.Add (fun e -> 

                                    let shellParent = Shell.Current.CurrentItem.CurrentItem :> Element
                                    let pageParent = pageToPush.Parent

                                    if shellParent = pageParent ||
                                        // in case of a gloabl modal like the login
                                       pageToPush.Parent.GetType().BaseType = typeof<Xamarin.Forms.Application> then

                                        let lastPageId = inbox.PostAndReply (fun rply -> GetLastPageId rply)
                                        if pageToPush.AutomationId = lastPageId then 
                                            input.CloseEvent ()
                                            inbox.Post <| RemovePage pageToPush.AutomationId
                                            ()
                                )
                                match input.Appearence with
                                | Shell -> 
                                    match shellRef.TryValue with
                                    | Some shellRef ->
                                        Device.BeginInvokeOnMainThread(
                                            fun () ->
                                                shellRef.Navigation.PushModalAsync (pageToPush,true) |> ignore
                                        )
                                    | None ->
                                        ()

                                | BrowserPage ->
                                    match browserRef.TryValue with
                                    | Some browserRef ->
                                        Device.BeginInvokeOnMainThread(
                                            fun () ->
                                                browserRef.Navigation.PushAsync (pageToPush,true) |> ignore
                                        )
                                        
                                    | None ->
                                        ()


                                let pushPage = {
                                    UniqueId=input.UniqueId
                                    Page=input.Page
                                    ContentPage=pageToPush
                                    Appearence=input.Appearence
                                }

                                return! loop {state with PushedPages = pushPage::state.PushedPages }

                        | RemovePage id ->
                            let newState =
                                { state with PushedPages = state.PushedPages |> List.filter (fun i -> i.UniqueId <> id) }

                            return! loop newState

                        | RemoveLastModal ->
                            match state.PushedPages |> List.tryHead with
                            | None ->
                                return! loop state
                            | Some last ->
                                match last.Appearence with
                                | Shell -> 
                                    match shellRef.TryValue with
                                    | Some shellRef ->
                                        Device.BeginInvokeOnMainThread(
                                            fun () ->
                                                shellRef.Navigation.PopModalAsync () |> ignore
                                        )

                                        //let! _ = shellRef.Navigation.PopModalAsync (true) |> Async.AwaitTask
                                        ()
                                    | None ->
                                        ()

                                | BrowserPage ->
                                    match browserRef.TryValue with
                                    | Some browserRef ->
                                        Device.BeginInvokeOnMainThread(
                                            fun () ->
                                                browserRef.Navigation.RemovePage (last.ContentPage) |> ignore
                                        )

                                        //let! _ = shellRef.Navigation.PopModalAsync (true) |> Async.AwaitTask
                                        ()
                                    | None ->
                                        ()


                                return! loop { state with PushedPages = state.PushedPages.Tail }

                        | GetLastPageId reply ->
                            match state.PushedPages |> List.tryHead with
                            | None ->
                                reply.Reply ("")
                            | Some last ->
                                reply.Reply (last.UniqueId)
                                return! loop state

                            return! loop state

                        | CleanupPageStack ->
                            return! loop { PushedPages = [] }
                    }

                loop { PushedPages = [] }
        )
    )
                    

    let pushOrUpdateModal input =
        modalManager.Force().Post <| PushOrUpdateModal input


    let removeLastModal () =
        modalManager.Force().Post <| RemoveLastModal


    let getLastPageId () =
        modalManager.Force().PostAndReply <| GetLastPageId


    let cleanUpModalPageStack () =
        modalManager.Force().Post <| CleanupPageStack



//module BrowserPageModal =

//    let private lazyPageMap = lazy (new System.Collections.Generic.Dictionary<string, ViewElement>())
        
//    let private prevPageMap = lazyPageMap.Force ()

//    let private pushPageInternal dispatch (sr:ContentPage) closeEventMsg (page:ViewElement) =
        
//        let p = page.Create() :?> ContentPage
//        // a littlebit hacky, but trigger change of the model
//        // which knows about every site, only when the shell is currently on the browser page
//        // the disappearing event will also triggert, when you hcange the side with the tab button in the bottom
//        p.Disappearing.Add(fun e -> 
//            let shell = Shell.Current
//            let item = shell.CurrentItem
//            if item.CurrentItem.Title = Translations.current.TabBarBrowserLabel then
//                // disapear also is triggered when a new popup overlayes the old one.
//                // if the current page is not the last page in the page map, than do not call the
//                // close event msg
//                let lastPageMapTitle =  prevPageMap.Keys |> Seq.last
//                if lastPageMapTitle = p.Title then
//                    dispatch closeEventMsg
//                    if prevPageMap.ContainsKey(p.Title) then
//                        prevPageMap.Remove(p.Title) |> ignore
//        )

//        sr.Navigation.PushAsync(p) |> Async.AwaitTask |> Async.StartImmediate
    
//    let private tryFindPage (sr:ContentPage) title =
//        sr.Navigation.NavigationStack |> Seq.filter (fun e -> e<>null) |> Seq.tryFind (fun i -> i.Title = title)


//    let pushPage dispatch closeMessage pageTitle suppressUpdate (pageRef:ViewRef<CustomContentPage>) page =
//        pageRef.TryValue
//        |> Option.map (fun sr ->
//            let hasPageInStack =
//                tryFindPage sr pageTitle
//            match hasPageInStack with
//            | None ->
//                // creates a new page and push it to the modal stack
//                // update page map first, because the disappeare event will rely on that, that the new page is already in the dict
//                prevPageMap.Add(pageTitle,page)
//                page |> pushPageInternal dispatch sr (closeMessage pageTitle)
                    
//                ()
//            | _ ->
//                ()
//        )
//        |> ignore

//    let updatePage dispatch closeMessage pageTitle suppressUpdate (pageRef:ViewRef<CustomContentPage>) (page:ViewElement) =
//        pageRef.TryValue
//        |> Option.map (fun sr ->
//            let hasPageInStack =
//                tryFindPage sr pageTitle //
//            match hasPageInStack with
//            | None ->
//                // in case no page on stack found, remove it from the dictionary
//                if (prevPageMap.ContainsKey pageTitle) then
//                    prevPageMap.Remove pageTitle |> ignore
//                else 
//                    ()
//                pushPage dispatch closeMessage pageTitle suppressUpdate pageRef page
//            | Some pushedPage -> 
//                if suppressUpdate then
//                    ()
//                else
//                    // this uses the new view Element and through model updated Page 
//                    // and updates the current viewed from the shel modal stack :) nice!
//                    let (hasPrev,prevPage) = prevPageMap.TryGetValue(pageTitle)
//                    match hasPrev with
//                    | false ->
//                        page.Update(pushedPage)
//                        if (prevPageMap.ContainsKey(pageTitle)) then
//                            prevPageMap.[pageTitle] <- page
//                        else
//                            prevPageMap.Add(pageTitle,page)
//                    | true ->
//                        //prevPage.UpdateIncremental(page,pushedPage)
//                        page.UpdateIncremental(prevPage,pushedPage)
//                        if (prevPageMap.ContainsKey(pageTitle)) then
//                            prevPageMap.[pageTitle] <- page
//                        else
//                            prevPageMap.Add(pageTitle,page)

//                    ()
//        )
//        |> ignore

//    let clearPushPages () =
//        prevPageMap.Clear ()
//        let a = 1
//        ()



    
    
        

