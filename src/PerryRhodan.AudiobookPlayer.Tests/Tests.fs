module Tests

open Dependencies
open PerryRhodan.AudiobookPlayer.ViewModel
open PerryRhodan.AudiobookPlayer.ViewModels
open Xunit


[<Fact>]
let ``Check BrowserView Design init`` () =
    DependencyService.SetComplete()
    let vm = new HomeViewModel([| AudioBookItemViewModel.DesignVM; AudioBookItemViewModel.DesignVM2 |])
    Assert.Equal(2, vm.AudioBooks.Length)
    ()