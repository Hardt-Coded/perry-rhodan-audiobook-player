module Controls

open Fabulous
open Fabulous.XamarinForms
open Xamarin.Forms
open Xamarin
open Domain
open Common

let faFontFamilyName bold =
    match Device.RuntimePlatform,bold with
    | Device.Android, false -> "fa-regular-400.ttf#Font Awesome 5 Free"
    | Device.Android, true -> "fa-solid-900.ttf#Font Awesome 5 Free"
    | Device.iOS, _ -> "Font Awesome 5 Free"
    | Device.UWP, true -> "Assets/fa-solid-900.ttf#Font Awesome 5 Free"
    | Device.UWP, false -> "Assets/fa-regular-400.ttf#Font Awesome 5 Free"
    | _, true -> "/#Font Awesome 5 Free Solid"
    | _, false -> "/#Font Awesome 5 Free"


let audioBookEntryActionSheet
    cmdDownload
    cmdRemoveFromDownloadQueue
    cmdDelete
    cmdMarkAsListend
    cmdMarkAsUnlistend
    cmdDownloadOnlyPicture
    cmdDescription
    cmdDeleteFromDatabase
    isOnDownloadQueue
    isCurrentlyDownloading
    audiobook =
    async {
        let buttons = [|
            yield (Translations.current.AudioBookDescription,cmdDescription audiobook)

            if audiobook.State.Downloaded then
                yield (Translations.current.RemoveFromDevice,cmdDelete audiobook)
            elif (isOnDownloadQueue && not audiobook.State.Downloaded && not isCurrentlyDownloading) then
                yield (Translations.current.RemoveFromDownloaQueue,cmdRemoveFromDownloadQueue audiobook)
            elif (not isOnDownloadQueue && not audiobook.State.Downloaded) then
                yield (Translations.current.DownloadAudioBook,cmdDownload audiobook)




            if audiobook.State.Completed then
                yield (Translations.current.UnmarkAsListend,cmdMarkAsUnlistend audiobook)
            else
                yield (Translations.current.MarkAsListend,cmdMarkAsListend audiobook)


            let isDevMode = Services.SystemSettings.getDeveloperMode() |> Async.RunSynchronously
            if isDevMode then
                yield ("Remove Item from Database",cmdDeleteFromDatabase audiobook)

        |]
        return! Helpers.displayActionSheet (Some Translations.current.PleaseSelect) (Some Translations.current.Cancel) buttons
    }

let listendCheckLabel =
    View.Label(text="\uf058",
        fontFamily=faFontFamilyName true,
        fontSize=FontSize.fromValue 25.,
        textColor=Color.White,
        verticalOptions = LayoutOptions.Fill,
        horizontalOptions = LayoutOptions.Fill,
        verticalTextAlignment = TextAlignment.Center,
        horizontalTextAlignment = TextAlignment.Center
        )

let arrowDownLabel =
    View.Label(text="\uf358",
        fontFamily=faFontFamilyName false,
        fontSize=FontSize.fromValue 25.,
        textColor=Consts.primaryTextColor,
        verticalOptions = LayoutOptions.Fill,
        horizontalOptions = LayoutOptions.Fill,
        verticalTextAlignment = TextAlignment.Center,
        horizontalTextAlignment = TextAlignment.Center
        )

let inDownloadQueueLabel =
    View.Label(text="\uf0c9",
        fontFamily=faFontFamilyName true,
        fontSize=FontSize.fromValue 25.,
        textColor=Consts.primaryTextColor,
        verticalOptions = LayoutOptions.Fill,
        horizontalOptions = LayoutOptions.Fill,
        verticalTextAlignment = TextAlignment.Center,
        horizontalTextAlignment = TextAlignment.Center
        )

let playerSymbolLabel =
    View.Label(text="\uf144",
        fontFamily=faFontFamilyName false,
        fontSize=FontSize.fromValue 25.,
        textColor=Consts.primaryTextColor,
        verticalOptions = LayoutOptions.Fill,
        horizontalOptions = LayoutOptions.Fill,
        verticalTextAlignment = TextAlignment.Center,
        horizontalTextAlignment = TextAlignment.Center
        )






let tapLabelWithFaIcon faIcon faBold onTab textColor fontSize text =
    View.StackLayout(orientation = StackOrientation.Horizontal,
        children = [
            View.Label(text=faIcon,
                textColor=textColor,
                fontFamily = faFontFamilyName faBold,
                fontSize=FontSize.fromValue fontSize,
                verticalOptions = LayoutOptions.Center,
                verticalTextAlignment = TextAlignment.Center,
                margin = Thickness(0., 0., 4., 0.)
            )
            View.Label(text=text,
                textColor=textColor,
                verticalOptions = LayoutOptions.Center,
                verticalTextAlignment = TextAlignment.Center,
                fontSize=FontSize.fromValue (fontSize + 2.)
            )
        ],
        gestureRecognizers = [
            View.TapGestureRecognizer(command = onTab)
        ])


let primaryTextColorLabel size text =
    View.Label(text=text,
        fontSize=FontSize.fromValue size,
        textColor=Consts.primaryTextColor,
        verticalOptions=LayoutOptions.Fill,
        horizontalOptions=LayoutOptions.Fill,
        horizontalTextAlignment=TextAlignment.Center,
        verticalTextAlignment=TextAlignment.Center
        )


let secondaryTextColorLabel size text =
    View.Label(text=text,
        fontSize=FontSize.fromValue size,
        textColor=Consts.secondaryTextColor,
        verticalOptions=LayoutOptions.Fill,
        horizontalOptions=LayoutOptions.Fill,
        horizontalTextAlignment=TextAlignment.Center,
        verticalTextAlignment=TextAlignment.Center
        )


let primaryColorSymbolLabelWithTapCommand command size solid text =
    View.Label(text=text,
        fontSize=FontSize.fromValue size,
        fontFamily=faFontFamilyName solid,
        textColor=Consts.primaryTextColor,
        verticalOptions=LayoutOptions.Fill,
        horizontalOptions=LayoutOptions.Fill,
        horizontalTextAlignment=TextAlignment.Center,
        verticalTextAlignment=TextAlignment.Center,
        gestureRecognizers = [
            View.TapGestureRecognizer(command = command)
        ]
        )


let primaryColorSymbolLabelWithTapCommandRightAlign command size solid text =
    View.Label(text=text,
        fontSize=FontSize.fromValue size,
        fontFamily=faFontFamilyName solid,
        textColor=Consts.primaryTextColor,
        verticalOptions=LayoutOptions.Fill,
        horizontalOptions=LayoutOptions.End,
        horizontalTextAlignment=TextAlignment.End,
        verticalTextAlignment=TextAlignment.Center,
        gestureRecognizers = [
            View.TapGestureRecognizer(command = command)
        ]
        )


let contentPageWithBottomOverlay
    (pageRef:ViewRef<CustomContentPage>)
    (bottomOverlay:ViewElement option)
    (content:ViewElement)
    isBusy
    title
    uniqueId =
    View.ContentPage(
        title=title,
        backgroundColor = Consts.backgroundColor,
        isBusy = isBusy,
        ref=pageRef,
        content =
            View.Grid(rowdefs = [Star; Auto],
                rowSpacing = 0.,
                children = [
                    yield content.Row(0)
                    if bottomOverlay.IsSome then
                        yield bottomOverlay.Value.Row(1)
                ]
        ),
        automationId = uniqueId
    )


let contentPageWithBottomOverlayW (pageRef:ViewRef<CustomContentPage>) (bottomOverlay:ViewElement option) (content:ViewElement) isBusy title =
    View.ContentPage(
        title=title,
        backgroundColor = Consts.backgroundColor,
        isBusy = isBusy,
        ref=pageRef,
        content =
            View.Grid(rowdefs = [Star; Auto],
                rowSpacing = 0.,
                children = [
                    yield content.Row(0)
                    if bottomOverlay.IsSome then
                        yield bottomOverlay.Value.Row(1)
                ]
        )
    )



let navPageWithBottomOverlay (pageRef:ViewRef<NavigationPage>) (bottomOverlay:ViewElement option) (content:ViewElement) isBusy title =
    View.NavigationPage(
        title=title,
        backgroundColor = Consts.backgroundColor,
        isBusy = isBusy,
        ref=pageRef,
        pages=[
            View.ContentPage(
                title=title,
                backgroundColor = Consts.backgroundColor,
                isBusy = isBusy,
                content =
                    View.Grid(rowdefs = [Star; Auto],
                        rowSpacing = 0.,
                        children = [
                            yield content.Row(0)
                            if bottomOverlay.IsSome then
                                yield bottomOverlay.Value.Row(1)
                        ]
                )
            )
        ]
    )



let tickerBand (fontSize:float) text =
    View.ScrollView(
        orientation=ScrollOrientation.Horizontal,
        horizontalScrollBarVisibility = ScrollBarVisibility.Never,
        horizontalOptions=LayoutOptions.Fill,
        content=View.Label(text=text,
            fontSize=FontSize.fromValue fontSize,
            textColor=Consts.primaryTextColor,
            verticalOptions=LayoutOptions.Fill,
            horizontalOptions=LayoutOptions.Fill,
            horizontalTextAlignment=TextAlignment.Start,
            verticalTextAlignment=TextAlignment.Center,
            lineBreakMode = LineBreakMode.NoWrap,
            maxLines = 1
        ),
        created=(fun e ->

            async {

                while (e.ContentSize.Width <= 0.0) do
                    do! Async.Sleep 200

                let visibleWidth = e.Bounds.Width
                let contentWidth = e.ContentSize.Width
                let scrollWidth = contentWidth - visibleWidth
                if scrollWidth > 0.0 then
                    let waitingTime = 3.5
                    let durationSec = scrollWidth / 32.0
                    let duration = (durationSec + waitingTime) * 1000.0
                    let additionWidthForWaitingRepeat = waitingTime * 32.0
                    do! Device.InvokeOnMainThreadAsync(fun () ->
                            let anim = Animation((fun v -> e.ScrollToAsync(v, 0.0, false) |> ignore), start=(0.0 - additionWidthForWaitingRepeat), ``end``= (scrollWidth + additionWidthForWaitingRepeat))
                            anim.Commit(e,"mini-player-ticker",rate=24u,length=(duration |> uint32),easing=Easing.Linear,repeat=(fun ()-> true))
                        ) |> Async.AwaitTask

            }
            |> Async.Start
        )
    )


type MyContentPage (cp:ContentPage, onBackButton) as self =
    inherit ContentPage()

    do
        self.Title <- cp.Title
        self.BackgroundColor <- cp.BackgroundColor
        self.IsBusy <- cp.IsBusy
        self.Content <- cp.Content

    override this.OnBackButtonPressed()=
        onBackButton()





