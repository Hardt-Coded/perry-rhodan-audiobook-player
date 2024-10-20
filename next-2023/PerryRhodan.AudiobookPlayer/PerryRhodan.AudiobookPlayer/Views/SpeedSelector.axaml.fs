namespace PerryRhodan.AudiobookPlayer.SpeedSelector

open System
open System.Globalization
open Avalonia.Data.Converters
open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Markup.Xaml
open Avalonia.Data
open Avalonia.Interactivity
open CherylUI.Controls
open PerryRhodan.AudiobookPlayer.Views


type SpeedSelectorPopup() as this =
    inherit UserControl()

    //let mutable _mobileNumberPicker: SpeedSelector = Unchecked.defaultof<SpeedSelector>
    let mutable isScrolling = false
    let mutable StartingPosition = Point()
    let mutable _speedSelector: SpeedSelector = Unchecked.defaultof<SpeedSelector>
    let mutable _currentValue = 0.0m
    
    do
        this.InitializeComponent()

    new (_mobile: SpeedSelector) as this =
        SpeedSelectorPopup()
        then
            this.SpeedSelector <- _mobile
            this.InitializeComponent()
            this.SetTextValues(_mobile.Value)
            this.CurrentValue <- _mobile.Value
        

    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)

    member this.CurrentValue
        with get():decimal = _currentValue
        and set(value:decimal) = _currentValue <- value
    
    //member val StartingPosition = Point() with get, set
    member this.SpeedSelector
        with get() =_speedSelector
        and set(value) = _speedSelector <- value
    
    member this.PointerPressed(sender: obj, e: PointerPressedEventArgs) =
        isScrolling <- true
        StartingPosition <- e.GetPosition(this.FindControl<TextBlock>("CurrentValueText"))

    member this.PointerReleased(sender: obj, e: PointerReleasedEventArgs) =
        isScrolling <- false
        let difference = ((StartingPosition.Y - e.GetPosition(this.FindControl<TextBlock>("CurrentValueText")).Y) |> decimal) / 100.0m
        // floor to next or previous 0.05
        let difference = difference - (difference % 0.05m)
        _speedSelector.Value <- _currentValue + difference
        _currentValue <- _currentValue + difference

    member this.PointerMoved(sender: obj, e: PointerEventArgs) =
        if isScrolling then
            let difference = ((StartingPosition.Y - e.GetPosition(this.FindControl<TextBlock>("CurrentValueText")).Y) |> decimal) / 100.0m
            let difference = difference - (difference % 0.05m)
            let mutable temporaryValue = _currentValue + difference

            if temporaryValue > _speedSelector.Maximum then
                StartingPosition <- e.GetPosition(this.FindControl<TextBlock>("CurrentValueText"))
                temporaryValue <- _speedSelector.Maximum
                _currentValue <- temporaryValue

            if temporaryValue < _speedSelector.Minimum then
                temporaryValue <- _speedSelector.Minimum
                StartingPosition <- e.GetPosition(this.FindControl<TextBlock>("CurrentValueText"))
                _currentValue <- temporaryValue

            this.SetTextValues(temporaryValue)

    member private this.SetTextValues(temporaryValue: decimal) =
        this.FindControl<TextBlock>("CurrentValueText").Text <- $"%1.2f{temporaryValue}x"

        if temporaryValue - 0.05m < _speedSelector.Minimum  then
            this.FindControl<TextBlock>("CurrentValueTextMinus1").Text <- ""
        else
            this.FindControl<TextBlock>("CurrentValueTextMinus1").Text <- $"%1.2f{(temporaryValue - 0.05m)}x"

        if temporaryValue + 0.05m > _speedSelector.Maximum then
            this.FindControl<TextBlock>("CurrentValueTextPlus1").Text <- ""
        else
            this.FindControl<TextBlock>("CurrentValueTextPlus1").Text <- $"%1.2f{(temporaryValue + 0.05m)}x"

        if temporaryValue + 0.1m > _speedSelector.Maximum then
            this.FindControl<TextBlock>("CurrentValueTextPlus2").Text <- ""
        else
            this.FindControl<TextBlock>("CurrentValueTextPlus2").Text <- $"%1.2f{(temporaryValue + 0.1m)}x"

        if temporaryValue - 0.1m < _speedSelector.Minimum then
            this.FindControl<TextBlock>("CurrentValueTextMinus2").Text <- ""
        else
            this.FindControl<TextBlock>("CurrentValueTextMinus2").Text <- $"%1.2f{(temporaryValue - 0.1m)}x"

    member private this.DoneClick(sender: obj, e: RoutedEventArgs) =
        InteractiveContainer.CloseDialog()

and SpeedSelector() as self =
    inherit UserControl()
    
    static let ValueProperty =
        AvaloniaProperty.RegisterDirect<SpeedSelector, decimal>(
            "Value",
            (fun o -> o.Value),
            (fun o v -> o.Value <- v),
            defaultBindingMode = BindingMode.TwoWay,
            enableDataValidation = true
        )
        
    static let MinimumProperty:StyledProperty<decimal> =
        AvaloniaProperty.Register<SpeedSelector, decimal>("Minimum", 0.m)
        
    static let MaximumProperty:StyledProperty<decimal> =
        AvaloniaProperty.Register<SpeedSelector, decimal>("Maximum", 2.m)
    

    let mutable _value = 0.0m

    do self.InitializeComponent()

    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)

    
    
    member this.Value
        with get():decimal = _value
        and set(value:decimal) = this.SetAndRaise<decimal>(ValueProperty, &_value, value) |> ignore

    

    member this.Minimum
        with get():decimal = this.GetValue<decimal>(MinimumProperty)
        and set(value) = this.SetValue(MinimumProperty, value) |> ignore

    

    member this.Maximum
        with get():decimal = this.GetValue<decimal>(MaximumProperty)
        and set(value) = this.SetValue(MaximumProperty, value) |> ignore

    

    member private this.OpenPopup(sender: obj, e: RoutedEventArgs) =
        let control = new SpeedSelectorPopup(this)
        InteractiveContainer.ShowDialog(control, true)



type SpeedToStringConverter() =
    static member val Instance = SpeedToStringConverter() with get, set

    interface IValueConverter with
        member this.Convert(value, targetType, parameter, culture) =
            $"%1.2f{value :?> decimal}x"

        member this.ConvertBack(value, targetType, parameter, culture) =
            raise (NotSupportedException())