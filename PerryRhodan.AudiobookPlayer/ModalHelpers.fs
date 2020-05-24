module ModalHelpers

open Fabulous
open Xamarin.Forms
open Common.ModalBaseHelpers
open Fabulous.XamarinForms



let pushLoginModal dispatch loginPageMsg loginClosed (shellRef:ViewRef<Shell>) loginModel =
    (LoginPage.view loginModel (loginPageMsg >> dispatch))
    |> pushModal dispatch loginClosed Translations.current.LoginPage shellRef

let updateLoginModal dispatch loginPageMsg loginClosed (shellRef:ViewRef<Shell>) loginModel =
    (LoginPage.view loginModel (loginPageMsg >> dispatch))
    |> updateModal dispatch loginClosed Translations.current.LoginPage shellRef


let pushDetailModal dispatch detailPageMsg detailPageClosed (shellRef:ViewRef<Shell>) detailModel =
    (AudioBookDetailPage.view detailModel (detailPageMsg >> dispatch))
    |> pushModal dispatch detailPageClosed Translations.current.AudioBookDetailPage shellRef

let updateDetailModal dispatch detailPageMsg detailPageClosed (shellRef:ViewRef<Shell>) detailModel =
    (AudioBookDetailPage.view detailModel (detailPageMsg >> dispatch))
    |> updateModal dispatch detailPageClosed Translations.current.AudioBookDetailPage shellRef


let pushFeedbackModal dispatch feedbackPageMsg feedbackPageClosed (shellRef:ViewRef<Shell>) feedbackModel =
    (SupportFeedback.view feedbackModel (feedbackPageMsg >> dispatch))
    |> pushModal dispatch feedbackPageClosed Translations.current.FeedbackPage shellRef

let updateFeedbackModal dispatch feedbackPageMsg feedbackPageClosed (shellRef:ViewRef<Shell>) feedbackModel =
    (SupportFeedback.view feedbackModel (feedbackPageMsg >> dispatch))
    |> updateModal dispatch feedbackPageClosed Translations.current.FeedbackPage shellRef



module BrowserPageModal =

    let private lazyPageMap = lazy (new System.Collections.Generic.Dictionary<string, ViewElement>())
        
    let private prevPageMap = lazyPageMap.Force ()

    let private pushPageInternal dispatch (sr:ContentPage) closeEventMsg (page:ViewElement) =
        
        let p = page.Create() :?> ContentPage
        // a littlebit hacky, but trigger change of the model
        // which knows about every site, only when the shell is currently on the browser page
        // the disappearing event will also triggert, when you hcange the side with the tab button in the bottom
        p.Disappearing.Add(fun e -> 
            let shell = Shell.Current
            let item = shell.CurrentItem
            if item.CurrentItem.Title = Translations.current.TabBarBrowserLabel then
                // disapear also is triggered when a new popup overlayes the old one.
                // if the current page is not the last page in the page map, than do not call the
                // close event msg
                let lastPageMapTitle =  prevPageMap.Keys |> Seq.last
                if lastPageMapTitle = p.Title then
                    dispatch closeEventMsg
                    if prevPageMap.ContainsKey(p.Title) then
                        prevPageMap.Remove(p.Title) |> ignore
        )

        sr.Navigation.PushAsync(p) |> Async.AwaitTask |> Async.StartImmediate
    
    let private tryFindPage (sr:ContentPage) title =
        sr.Navigation.NavigationStack |> Seq.filter (fun e -> e<>null) |> Seq.tryFind (fun i -> i.Title = title)


    let pushPage dispatch closeMessage pageTitle suppressUpdate (pageRef:ViewRef<CustomContentPage>) page =
        pageRef.TryValue
        |> Option.map (fun sr ->
            let hasPageInStack =
                tryFindPage sr pageTitle
            match hasPageInStack with
            | None ->
                // creates a new page and push it to the modal stack
                // update page map first, because the disappeare event will rely on that, that the new page is already in the dict
                prevPageMap.Add(pageTitle,page)
                page |> pushPageInternal dispatch sr (closeMessage pageTitle)
                    
                ()
            | _ ->
                ()
        )
        |> ignore

    let updatePage dispatch closeMessage pageTitle suppressUpdate (pageRef:ViewRef<CustomContentPage>) (page:ViewElement) =
        pageRef.TryValue
        |> Option.map (fun sr ->
            let hasPageInStack =
                tryFindPage sr pageTitle //
            match hasPageInStack with
            | None ->
                // in case no page on stack found, remove it from the dictionary
                if (prevPageMap.ContainsKey pageTitle) then
                    prevPageMap.Remove pageTitle |> ignore
                else 
                    ()
                pushPage dispatch closeMessage pageTitle suppressUpdate pageRef page
            | Some pushedPage -> 
                if suppressUpdate then
                    ()
                else
                    // this uses the new view Element and through model updated Page 
                    // and updates the current viewed from the shel modal stack :) nice!
                    let (hasPrev,prevPage) = prevPageMap.TryGetValue(pageTitle)
                    match hasPrev with
                    | false ->
                        page.Update(pushedPage)
                        if (prevPageMap.ContainsKey(pageTitle)) then
                            prevPageMap.[pageTitle] <- page
                        else
                            prevPageMap.Add(pageTitle,page)
                    | true ->
                        //prevPage.UpdateIncremental(page,pushedPage)
                        page.UpdateIncremental(prevPage,pushedPage)
                        if (prevPageMap.ContainsKey(pageTitle)) then
                            prevPageMap.[pageTitle] <- page
                        else
                            prevPageMap.Add(pageTitle,page)

                    ()
        )
        |> ignore

    let clearPushPages () =
        prevPageMap.Clear ()
        let a = 1
        ()



    
    
        

