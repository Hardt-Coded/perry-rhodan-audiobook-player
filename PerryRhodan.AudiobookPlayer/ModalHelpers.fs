module ModalHelpers

open Fabulous
open Xamarin.Forms


let private pushModalPage dispatch (sr:Shell) closeEventMsg (page:ViewElement) =
    let p = page.Create() :?> Page    
    p.Disappearing.Add(fun e-> dispatch closeEventMsg)
    sr.Navigation.PushModalAsync(p) |> Async.AwaitTask |> Async.StartImmediate

let private tryFindModal (sr:Shell) title =
    sr.Navigation.ModalStack |> Seq.tryFind (fun i -> i.Title = title)

let private pushOrUpdateModal dispatch closeMessage pageTitle (shellRef:ViewRef<Shell>) page =
    shellRef.TryValue
    |> Option.map (fun sr ->
        let hasLoginPageInStack =
            tryFindModal sr pageTitle //
        match hasLoginPageInStack with
        | None ->
            // creates a new page and push it to the modal stack
            page |> pushModalPage dispatch sr closeMessage 
            ()
        | Some pushedPage -> 
            // this uses the new view Element and through model updated Page 
            // and updates the current viewed from the shel modal stack :) nice!
            page.Update(pushedPage)
    )
    |> ignore


let showLoginModal dispatch loginPageMsg loginClosed (shellRef:ViewRef<Shell>) loginModel =
    (LoginPage.view loginModel (loginPageMsg >> dispatch))
    |> pushOrUpdateModal dispatch loginClosed Translations.current.LoginPage shellRef


let showDetailModal dispatch detailPageMsg detailPageClosed (shellRef:ViewRef<Shell>) detailModel =
    (AudioBookDetailPage.view detailModel (detailPageMsg >> dispatch))
    |> pushOrUpdateModal dispatch detailPageClosed Translations.current.AudioBookDetailPage shellRef
    
        

