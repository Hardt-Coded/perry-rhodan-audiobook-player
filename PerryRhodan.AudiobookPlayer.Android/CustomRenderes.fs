namespace PerryRhodan.AudiobookPlayer.Android

open Android.App
open Android.Content
open Android.OS
open Android.Runtime
open Android.Views
open Android.Widget
open Android.Support.Design.Widget
open Xamarin.Forms.Platform.Android

module CustomRender =


    type MyShellBottomNavViewAppearanceTracker(context,item) =
        inherit ShellBottomNavViewAppearanceTracker(context,item)

        let makeColorStateList () =
            let states = [|
                [| -Android.Resource.Attribute.StateEnabled |]
                [| Android.Resource.Attribute.StateChecked |]
                [| |]
            |]
    
            let disabledInt =         
                Android.Graphics.Color.Gray.ToArgb()
    
            let checkedInt = 
                Common.Consts.primaryTextColor.ToAndroid().ToArgb()
    
            let defaultColor =
                Common.Consts.secondaryTextColor.ToAndroid().ToArgb()
            

            let colors = [| disabledInt; checkedInt; defaultColor |]
    
            new Res.ColorStateList(states, colors);

        override this.SetAppearance(bottomView, appearance) =
            base.SetAppearance(bottomView,appearance)
            use colorStateList = makeColorStateList()
            bottomView.ItemTextColor <- colorStateList
            bottomView.ItemIconTintList <- colorStateList
        


    type CustomShellRenderer (context) =
        inherit ShellRenderer(context) 

        override this.CreateBottomNavViewAppearanceTracker(item) =
            let sctx = this :> IShellContext
            new MyShellBottomNavViewAppearanceTracker(sctx,item) :> IShellBottomNavViewAppearanceTracker




    type WorkaroundNotScrollableViewRenderer(context) =
        inherit WebViewRenderer(context)
    

        override this.OnElementChanged(e) =
            base.OnElementChanged(e)

        override this.DispatchTouchEvent(e:MotionEvent) =
            this.Parent.RequestDisallowInterceptTouchEvent(true)
            base.DispatchTouchEvent(e);
