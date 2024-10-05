namespace PerryRhodan.AudiobookPlayer.Controls

open CherylUI.Controls
open PerryRhodan.AudiobookPlayer.ViewModel
open PerryRhodan.AudiobookPlayer.ViewModels

type ActionMenuService() =
    interface IActionMenuService with
        member this.ShowAudiobookActionMenu audioBookItemViewModel =
            let control = PerryRhodan.AudiobookPlayer.Views.ActionMenuView()
            let vm = new ActionMenuViewModel(audioBookItemViewModel :?> AudioBookItemViewModel)
            control.DataContext <- vm
            InteractiveContainer.ShowDialog (control, true)
