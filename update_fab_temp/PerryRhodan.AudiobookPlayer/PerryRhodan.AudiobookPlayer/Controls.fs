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
        fontSize=FontSize 25.,
        textColor=Color.White,
        verticalOptions = LayoutOptions.Fill, 
        horizontalOptions = LayoutOptions.Fill, 
        verticalTextAlignment = TextAlignment.Center,
        horizontalTextAlignment = TextAlignment.Center
        )

let arrowDownLabel = 
    View.Label(text="\uf358",
        fontFamily=faFontFamilyName false,
        fontSize=FontSize 25.,
        textColor=Consts.primaryTextColor,
        verticalOptions = LayoutOptions.Fill, 
        horizontalOptions = LayoutOptions.Fill, 
        verticalTextAlignment = TextAlignment.Center,
        horizontalTextAlignment = TextAlignment.Center
        )

let inDownloadQueueLabel =
    View.Label(text="\uf0c9",
        fontFamily=faFontFamilyName true,
        fontSize=FontSize 25.,
        textColor=Consts.primaryTextColor,
        verticalOptions = LayoutOptions.Fill, 
        horizontalOptions = LayoutOptions.Fill, 
        verticalTextAlignment = TextAlignment.Center,
        horizontalTextAlignment = TextAlignment.Center
        )

let playerSymbolLabel =
    View.Label(text="\uf144",
        fontFamily=faFontFamilyName false,
        fontSize=FontSize 25.,
        textColor=Consts.primaryTextColor,
        verticalOptions = LayoutOptions.Fill, 
        horizontalOptions = LayoutOptions.Fill, 
        verticalTextAlignment = TextAlignment.Center,
        horizontalTextAlignment = TextAlignment.Center
        )

let showDownloadProgress (f:int,t:int) =
    let factor = (f |> float) / (t |> float)
    View.Grid(
        children = [
            View.ProgressBar(
                progress = (factor),
                verticalOptions = LayoutOptions.End, 
                horizontalOptions = LayoutOptions.Fill,
                height = 12.,
                created = (
                    fun e ->
                        e.ProgressColor <- Color.Green
                )
            )
            View.Label(text=(sprintf "%i %%" ((factor * 100.0) |> int)),
                //fontFamily=faFontFamilyName true,
                fontSize=FontSize 11.,
                margin=Thickness 3.,
                textColor=Consts.primaryTextColor,
                verticalOptions = LayoutOptions.Fill, 
                horizontalOptions = LayoutOptions.Fill, 
                verticalTextAlignment = TextAlignment.End,
                horizontalTextAlignment = TextAlignment.Center
                )    
        ]
    )
    



let audioBookStateOverlay 
    isDownloaded 
    isLoading 
    isComplete 
    isInDownloadQueue 
    (progress: (int * int) option) 
    openAudioBookPlayerCmd =
    View.Grid(
        backgroundColor = Color.Transparent,
        margin=Thickness 10.,
        coldefs = [Star; Star; Star],
        rowdefs = [Star; Star; Star],
        children = [

            match isComplete,isInDownloadQueue, isLoading, isDownloaded with
            | true,_,_,_ ->
                yield listendCheckLabel.Column(2).Row(2)
            | false,true,false,false ->
                yield inDownloadQueueLabel.Column(2).Row(2)
            | false,false,false,false ->
                yield arrowDownLabel.Column(2).Row(2)
            | false,false,false,true ->
                yield playerSymbolLabel.Column(1).Row(1)
            | false,_,true,false ->
                match progress with
                | None -> ()
                | Some progress ->
                    let f,t = progress
                    yield ((f,t) |> showDownloadProgress).ColumnSpan(3).Row(2).Column(0)
            | _ ->
                ()
           
            
        ]
        , gestureRecognizers = 
            [
                View.TapGestureRecognizer(
                    command = (fun () -> 
                        if isDownloaded then 
                            openAudioBookPlayerCmd ()
                        else
                        ()
                    )
                )
        ]
    )
    
let renderAudiobookEntry
    openActionMenuCmd
    openAudioBookPlayerCmd
    isLoading
    isInDownloadQueue
    (progress: (int * int) option)
    audiobook =
        View.Grid(
            backgroundColor = Consts.cardColor,
            margin=Thickness 5.,
            height = 120.,
            coldefs = [Auto; Star; Auto],
            rowdefs = [Auto],
            children = [
                match audiobook.Thumbnail with
                | None ->
                    yield View.Image(source=ImagePath "AudioBookPlaceholder_Dark.png"
                        , aspect = Aspect.AspectFit
                        , height=100.
                        , width=100.
                        , margin=Thickness 10.).Column(0).Row(0)
                | Some thumb ->
                    yield View.Image(source=ImagePath thumb
                        , aspect = Aspect.AspectFit
                        , height=100.
                        , width=100.
                        , margin=Thickness 10.
                        ).Column(0).Row(0)
                
                // audioBook state
                yield (audioBookStateOverlay 
                    audiobook.State.Downloaded 
                    isLoading 
                    audiobook.State.Completed 
                    isInDownloadQueue 
                    progress
                    openAudioBookPlayerCmd
                    ).Column(0).Row(0)

                yield View.Label(text=audiobook.FullName, 
                    fontSize = FontSize 15., 
                    verticalOptions = LayoutOptions.Fill, 
                    horizontalOptions = LayoutOptions.Fill, 
                    verticalTextAlignment = TextAlignment.Center,
                    horizontalTextAlignment = TextAlignment.Center,
                    textColor = Consts.secondaryTextColor,
                    lineBreakMode = LineBreakMode.WordWrap
                    ).Column(1).Row(0)
                yield View.Grid(
                    verticalOptions = LayoutOptions.Fill, 
                    horizontalOptions = LayoutOptions.Fill,
                    gestureRecognizers = [
                        View.TapGestureRecognizer(command = openActionMenuCmd)
                    ],
                    children = [
                        View.Label(text="\uf142",fontFamily = faFontFamilyName true,
                            fontSize=FontSize 35., 
                            margin = Thickness(10., 0. ,10. ,0.),                    
                            verticalOptions = LayoutOptions.Fill, 
                            horizontalOptions = LayoutOptions.Fill, 
                            verticalTextAlignment = TextAlignment.Center,
                            horizontalTextAlignment = TextAlignment.Center,
                            textColor = Consts.secondaryTextColor
                            )
                    ]
                ).Column(2).Row(0)

        ]
        )



let tapLabelWithFaIcon faIcon faBold onTab textColor fontSize text =
    View.StackLayout(orientation = StackOrientation.Horizontal,
        children = [
            View.Label(text=faIcon,
                textColor=textColor,
                fontFamily = faFontFamilyName faBold,
                fontSize=FontSize fontSize,
                verticalOptions = LayoutOptions.Center,
                verticalTextAlignment = TextAlignment.Center,
                margin = Thickness(0., 0., 4., 0.)
            )
            View.Label(text=text,
                textColor=textColor,                
                verticalOptions = LayoutOptions.Center,
                verticalTextAlignment = TextAlignment.Center,
                fontSize=FontSize (fontSize + 2.)
            )
        ],
        gestureRecognizers = [
            View.TapGestureRecognizer(command = onTab)
        ])

let primaryTextColorLabel size text = 
    View.Label(text=text,
        fontSize=FontSize size,
        textColor=Consts.primaryTextColor,
        verticalOptions=LayoutOptions.Fill,
        horizontalOptions=LayoutOptions.Fill,
        horizontalTextAlignment=TextAlignment.Center,
        verticalTextAlignment=TextAlignment.Center
        )

let secondaryTextColorLabel size text = 
    View.Label(text=text,
        fontSize=FontSize size,
        textColor=Consts.secondaryTextColor,
        verticalOptions=LayoutOptions.Fill,
        horizontalOptions=LayoutOptions.Fill,
        horizontalTextAlignment=TextAlignment.Center,
        verticalTextAlignment=TextAlignment.Center
        )

let primaryColorSymbolLabelWithTapCommand command size solid text = 
    View.Label(text=text,
        fontSize=FontSize size,
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
        fontSize=FontSize size,
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


let contentPageWithBottomOverlay (pageRef:ViewRef<CustomContentPage>) (bottomOverlay:ViewElement option) (content:ViewElement) isBusy title =
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




type MyContentPage (cp:ContentPage, onBackButton) as self =
    inherit ContentPage() 

    do  
        self.Title <- cp.Title
        self.BackgroundColor <- cp.BackgroundColor
        self.IsBusy <- cp.IsBusy
        self.Content <- cp.Content

    override this.OnBackButtonPressed()=
        onBackButton()

   

    

