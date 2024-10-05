namespace PerryRhodan.AudiobookPlayer.Notification.ViewModels


type MessageBoxViewModel(title:string, message:string) =
    
    member this.Title = title
    member this.Message = message



type QuestionBoxViewModel(title:string, message:string, okButtonLabel:string, cancelButtonLabel:string) =
    
    member this.Title = title
    member this.Message = message
    member this.OkButton = okButtonLabel
    member this.CancelButton = cancelButtonLabel
    