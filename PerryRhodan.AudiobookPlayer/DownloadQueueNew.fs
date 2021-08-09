module DownloadQueueNew

open Domain
open Fabulous
open Fabulous.XamarinForms
open Common
open Xamarin.Forms
open Services
open Global

type Model = AudioBookItemNew.AudioBookItem []


let init (allAudioBooks:AudioBookItemNew.AudioBookItem []) =
    allAudioBooks
    |> Array.filter (fun i -> 
        match i.Model.DownloadState with
        | AudioBookItemNew.Queued
        | AudioBookItemNew.Downloading _ -> true    
        | _ -> false
    )


let view (model:Model) =
    View.ContentPage(
        title="Downloads",
        backgroundColor = Consts.backgroundColor,
        content = View.Grid(
            children = [
                View.StackLayout(
                    orientation = StackOrientation.Vertical,
                    children = [
                        View.Label(text="aktuelle Downloads", fontAttributes = FontAttributes.Bold,
                            fontSize=FontSize.fromValue 25.,
                            verticalOptions=LayoutOptions.Fill,
                            horizontalOptions=LayoutOptions.Fill,
                            horizontalTextAlignment=TextAlignment.Center,
                            verticalTextAlignment=TextAlignment.Center,
                            textColor = Consts.primaryTextColor,
                            backgroundColor = Consts.cardColor,
                            margin=Thickness 0.)
                        
                        
                        match model with
                        | [||] ->
                            Controls.secondaryTextColorLabel 24. "aktuell laufen keine Downloads oder sind welche geplant."
                        | _ ->
                            View.ScrollView(
                                horizontalOptions = LayoutOptions.Fill,
                                verticalOptions = LayoutOptions.Fill,
                                content = 
                                    View.StackLayout(orientation=StackOrientation.Vertical,
                                        children= [
                                            for item in model do
                                                AudioBookItemNew.view item.Model item.Dispatch 
                                        ]
                                )
                            )
                    ]
                )
            ]
        )
    )