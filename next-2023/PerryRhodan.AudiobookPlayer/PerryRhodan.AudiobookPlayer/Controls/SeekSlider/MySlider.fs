namespace PerryRhodan.AudiobookPlayer.Controls

open Avalonia
open Avalonia.Controls
open Avalonia.Data

type MySlider() =
    inherit Slider()
    
    // add property IsSeeking
    static let IsSeekingProperty =
        //AvaloniaProperty.Register<MySlider, bool>("IsSeeking", false)
        AvaloniaProperty.RegisterDirect<MySlider, bool>(
            "IsSeeking",
            (fun x -> x.IsSeeking),
            defaultBindingMode=BindingMode.OneWayToSource
            )
            
    let mutable _isSeeking = false
    member this.IsSeeking
        with get() = _isSeeking
        and set(value:bool) =
            this.SetAndRaise(IsSeekingProperty, ref _isSeeking, value) |> ignore
        
        
    override this.StyleKeyOverride = typeof<Slider>
    
            
    override this.OnThumbDragStarted e =
        base.OnThumbDragStarted(e)
        this.IsSeeking <- true
        _isSeeking <- true
        
    override this.OnThumbDragCompleted e =
        base.OnThumbDragCompleted(e)
        this.IsSeeking <- false
        _isSeeking <- false
            
    
