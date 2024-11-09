namespace rec PerryRhodan.AudiobookPlayer.ValueSelector

open System
open System.Linq
open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Markup.Xaml
open Avalonia.Data
open Avalonia.Interactivity
open CherylUI.Controls
open Microsoft.FSharp.Reflection
open System.Collections


[<AutoOpen>]
module Helpers =

    let convertCollectionToTupleArray (list:ICollection) =
        list.Cast<obj>().ToArray()
        |> Array.map (fun x -> (FSharpValue.GetTupleField(x, 0), FSharpValue.GetTupleField(x, 1) :?> string))



type ValueSelectorPopup() as this =
    inherit UserControl()

    //let mutable _mobileNumberPicker: ValueSelector = Unchecked.defaultof<ValueSelector>
    let mutable isScrolling = false
    let mutable StartingPosition = Point()
    let mutable _speedSelector: ValueSelector = Unchecked.defaultof<ValueSelector>
    let mutable _currentIndex = -1
    do
        this.InitializeComponent()

    new (_mobile: ValueSelector) as this =
        ValueSelectorPopup()
        then
            this.ValueSelector <- _mobile
            this.InitializeComponent()
            this.SetTextValues(_mobile.SelectedIndex)
            this.SetFontSize(_mobile)
            this.CurrentValue <- _mobile.Value


    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)

    member this.CurrentValue
        with get():obj =
            let currentValues = _speedSelector.ItemList |> convertCollectionToTupleArray
            if _currentIndex = -1 || currentValues.Length = 0 then
                Unchecked.defaultof<obj>
            else
                currentValues.[_currentIndex] |> fst


        and set(value:obj) =
            let currentValues = _speedSelector.ItemList |> convertCollectionToTupleArray
            match currentValues |> Array.tryFindIndex (fun (key,_) -> key = value) with
            | Some index -> _currentIndex <- index
            | None -> _currentIndex <- -1


    member this.ValueSelector
        with get() =_speedSelector
        and set(value) = _speedSelector <- value

    member this.PointerPressed(sender: obj, e: PointerPressedEventArgs) =
        isScrolling <- true
        StartingPosition <- e.GetPosition(this.FindControl<TextBlock>("CurrentValueText"))

    member this.PointerReleased(sender: obj, e: PointerReleasedEventArgs) =
        let currentValues = _speedSelector.ItemList |> convertCollectionToTupleArray
        isScrolling <- false
        let difference = ((StartingPosition.Y - e.GetPosition(this.FindControl<TextBlock>("CurrentValueText")).Y) |> decimal) / 30.0m
        // floor to next or previous 0.05
        let difference = Math.Round(difference,0) |> int
        // set key
        let key, _ = currentValues.[_currentIndex + difference]
        _speedSelector.Value <- key
        _currentIndex <- _currentIndex + difference

    member this.PointerMoved(sender: obj, e: PointerEventArgs) =
        if isScrolling then
            let difference = ((StartingPosition.Y - e.GetPosition(this.FindControl<TextBlock>("CurrentValueText")).Y) |> decimal) / 30.0m
            let difference = Math.Round(difference,0) |> int
            let mutable temporaryIndex = _currentIndex + difference

            if temporaryIndex > _speedSelector.ItemList.Count - 1 then
                StartingPosition <- e.GetPosition(this.FindControl<TextBlock>("CurrentValueText"))
                temporaryIndex <- _speedSelector.ItemList.Count - 1
                _currentIndex <- temporaryIndex

            if temporaryIndex < 0 then
                temporaryIndex <- 0
                StartingPosition <- e.GetPosition(this.FindControl<TextBlock>("CurrentValueText"))
                _currentIndex <- temporaryIndex

            this.SetTextValues(temporaryIndex)

    member private this.SetFontSize(_mobile: ValueSelector) =
        
        this.FindControl<TextBlock>("CurrentValueTextMinus2").FontSize <- _mobile.BaseFontSize / 5.0
        this.FindControl<TextBlock>("CurrentValueTextMinus1").FontSize <- _mobile.BaseFontSize / 2.5
        this.FindControl<TextBlock>("CurrentValueText").FontSize <- _mobile.BaseFontSize
        this.FindControl<TextBlock>("CurrentValueTextPlus1").FontSize <- _mobile.BaseFontSize / 2.5
        this.FindControl<TextBlock>("CurrentValueTextPlus2").FontSize <- _mobile.BaseFontSize / 5.0
    
    member private this.SetTextValues(index: int) =
        let currentValues = _speedSelector.ItemList |> convertCollectionToTupleArray

        let _, value =
            if index = -1 then
                (Unchecked.defaultof<_>, "")
            else
                let idx =
                    if index < 0 then 0
                    elif index > currentValues.Length - 1 then currentValues.Length - 1
                    else index
                currentValues.[idx]

        this.FindControl<TextBlock>("CurrentValueText").Text <- value

        if index - 2 < 0 then
            this.FindControl<TextBlock>("CurrentValueTextMinus2").Text <- ""
        else
            this.FindControl<TextBlock>("CurrentValueTextMinus2").Text <- currentValues.[index - 2] |> snd

        if index + 2 > currentValues.Length - 1 then
            this.FindControl<TextBlock>("CurrentValueTextPlus2").Text <- ""
        else
            this.FindControl<TextBlock>("CurrentValueTextPlus2").Text <- currentValues.[index + 2] |> snd


        if index - 1 < 0 then
            this.FindControl<TextBlock>("CurrentValueTextMinus1").Text <- ""
        else
            this.FindControl<TextBlock>("CurrentValueTextMinus1").Text <- currentValues.[index - 1] |> snd

        if index + 1 > currentValues.Length - 1 then
            this.FindControl<TextBlock>("CurrentValueTextPlus1").Text <- ""
        else
            this.FindControl<TextBlock>("CurrentValueTextPlus1").Text <- currentValues.[index + 1] |> snd


    member private this.DoneClick(sender: obj, e: RoutedEventArgs) =
        InteractiveContainer.CloseDialog()



and ValueSelector() as self =
    inherit UserControl()

    
    static let BaseFontSizeProperty =
        AvaloniaProperty.Register<ValueSelector, double>("BaseFontSize", defaultValue = 50.0)    
        
    
    static let ValueProperty =
        AvaloniaProperty.RegisterDirect<ValueSelector, obj>(
            "Value",
            (fun o -> o.Value),
            (fun o v -> o.Value <- v),
            defaultBindingMode = BindingMode.TwoWay,
            enableDataValidation = true
        )


    static let ItemListProperty =
        AvaloniaProperty.RegisterDirect<ValueSelector, ICollection>(
            "ItemList",
            (fun o -> o.ItemList),
            (fun o v -> o.ItemList <- v),
            defaultBindingMode = BindingMode.OneWay,
            enableDataValidation = true
        )


    static let DisplayValueProperty =
        AvaloniaProperty.RegisterDirect<ValueSelector, string>(
            "DisplayValue",
            (fun o -> o.DisplayValue),
            defaultBindingMode = BindingMode.OneWayToSource,
            enableDataValidation = true
        )


    let mutable _value = Unchecked.defaultof<obj>
    let mutable _displayValue = ""
    let mutable _itemList:ICollection = [||]

    do
        self.InitializeComponent()
        self.HorizontalContentAlignment <- HorizontalAlignment.Center // standard center
        

    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)


    member this.BaseFontSize
        with get() = this.GetValue(BaseFontSizeProperty)
        and set(value) = this.SetValue(BaseFontSizeProperty, value) |> ignore
    
    member this.Value
        with get():obj = _value
        and set(value:obj) =
            this.SetAndRaise(ValueProperty,&_value, value) |> ignore
            // set possible DisplayValue
            let displayValue =
                this.ItemList |> convertCollectionToTupleArray
                |> Array.tryFind (fun (key,_) -> key = this.Value)
                |> Option.map snd
                |> Option.defaultValue ""
            this.SetAndRaise(DisplayValueProperty, &_displayValue, displayValue) |> ignore




    member this.DisplayValue
        with get() = _displayValue

    member this.SelectedIndex
        with get():int =
            let cast =
                this.ItemList
                |> convertCollectionToTupleArray
                |> Array.tryFindIndex (fun (key,_) -> key = this.Value)

            match cast with
            | Some index -> index
            | None -> -1

        and set(index) =
            let cast = this.ItemList |> convertCollectionToTupleArray
            if index < 0 || index > this.ItemList.Count - 1 then
                this.Value <- Unchecked.defaultof<obj>
            else
                this.Value <- cast.[index] |> fst

    member this.ItemList
        with get():ICollection = _itemList
        and set(value:ICollection) =
            this.SetAndRaise<ICollection>(ItemListProperty, &_itemList, value) |> ignore
            // set possible DisplayValue
            let displayValue =
                value |> convertCollectionToTupleArray
                |> Array.tryFind (fun (key,_) -> key = this.Value)
                |> Option.map snd
                |> Option.defaultValue ""
            this.SetAndRaise(DisplayValueProperty, &_displayValue, displayValue) |> ignore



    member private this.OpenPopup(sender: obj, e: RoutedEventArgs) =
        let control = new ValueSelectorPopup(this)
        InteractiveContainer.ShowDialog(control, true)



