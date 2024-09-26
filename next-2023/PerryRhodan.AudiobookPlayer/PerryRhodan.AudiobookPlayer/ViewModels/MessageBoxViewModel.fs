namespace PerryRhodan.AudiobookPlayer.ViewModels


type MessageBoxViewModel(title:string, message:string) =
    
    member this.Title = title
    member this.Message = message

