module NumberPickerModal

open Fabulous
open Fabulous.XamarinForms
open Xamarin.Forms


let modalTitle = "Select Number"

type Model = {
    Value:int
    Range:int list
    LabelText:string
}

type Msg =
    | SelectValue of int 
    | Ok

type ExternalMsg =
    | TakeValue


let init labeltext range value =
    { Value = value; Range = range; LabelText = labeltext}, Cmd.none


let update msg model =
    match msg with
    | Ok ->
        model, Cmd.none, Some (TakeValue) 
    | SelectValue v ->
        {model with Value = v }, Cmd.ofMsg Ok, None



let view model (dispatch:Msg->unit) =
    View.ContentPage(
        title = modalTitle,
        content=View.Grid(
            rowdefs=["auto";"auto";"auto"],
            verticalOptions=LayoutOptions.Center,
            horizontalOptions=LayoutOptions.Center,
            children=[
                (Controls.primaryTextColorLabel Common.FontSizeHelper.largeLabel model.LabelText)
                    .GridRow(0)
                    
                View.Picker(itemsSource=model.Range,
                    fontSize=Common.FontSizeHelper.largePicker,
                    selectedIndex=(model.Range |> List.tryFindIndex (fun x-> x=model.Value) |> Option.defaultValue 0),
                    selectedIndexChanged=(
                        fun (idx,e) -> 
                            e 
                            |> Option.map (fun v->
                                dispatch (SelectValue v)
                            ) 
                            |> ignore
                        )
                    )
                    .GridRow(1)
                    .HorizontalOptions(LayoutOptions.Center)

                //View.Button(text="Ok",command=(fun ()-> dispatch Ok)).GridRow(2)
            ]
        )
    )
    

