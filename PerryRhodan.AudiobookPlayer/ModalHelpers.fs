module ModalHelpers

open Fabulous
open Xamarin.Forms
open Common.ModalBaseHelpers



let showLoginModal dispatch loginPageMsg loginClosed (shellRef:ViewRef<Shell>) loginModel =
    (LoginPage.view loginModel (loginPageMsg >> dispatch))
    |> pushOrUpdateModal dispatch loginClosed Translations.current.LoginPage shellRef


let showDetailModal dispatch detailPageMsg detailPageClosed (shellRef:ViewRef<Shell>) detailModel =
    (AudioBookDetailPage.view detailModel (detailPageMsg >> dispatch))
    |> pushOrUpdateModal dispatch detailPageClosed Translations.current.AudioBookDetailPage shellRef
    
        

