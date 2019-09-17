module ModalHelpers

open Fabulous
open Xamarin.Forms
open Common.ModalBaseHelpers



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
    
        

