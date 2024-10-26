namespace PerryRhodan.AudiobookPlayer.Services


type GlobalSettingsService() =
    let mutable _rewindWhenStartAfterShortPeriodInSec = 0
    let mutable _rewindWhenStartAfterLongPeriodInSec = 0
    let mutable _longPeriodBeginsAfterInMinutes = 0
    let mutable _jumpDistance = 0
    let mutable _isFirstStart = true
    let mutable _playbackSpeed = 1.0m

    member this.Init() =
        task {
            let! rewindWhenStartAfterShortPeriodInSec = Services.SystemSettings.getRewindWhenStartAfterShortPeriodInSec() |> Async.StartAsTask
            let! rewindWhenStartAfterLongPeriodInSec = Services.SystemSettings.getRewindWhenStartAfterLongPeriodInSec() |> Async.StartAsTask
            let! longPeriodBeginsAfterInMinutes = Services.SystemSettings.getLongPeriodBeginsAfterInMinutes() |> Async.StartAsTask
            let! jumpDistance = Services.SystemSettings.getJumpDistance() |> Async.StartAsTask
            let! isFirstStart = Services.SystemSettings.getIsFirstStart() |> Async.StartAsTask
            let! playbackSpeed = Services.SystemSettings.getPlaybackSpeed() |> Async.StartAsTask


            _rewindWhenStartAfterShortPeriodInSec <- rewindWhenStartAfterShortPeriodInSec
            _rewindWhenStartAfterLongPeriodInSec <- rewindWhenStartAfterLongPeriodInSec
            _longPeriodBeginsAfterInMinutes <- longPeriodBeginsAfterInMinutes
            _jumpDistance <- jumpDistance
            _isFirstStart <- isFirstStart
            _playbackSpeed <- playbackSpeed
        }

    member this.RewindWhenStartAfterShortPeriodInSec
        with get() = _rewindWhenStartAfterShortPeriodInSec
        and set value =
            _rewindWhenStartAfterShortPeriodInSec <- value
            Services.SystemSettings.setRewindWhenStartAfterShortPeriodInSec(value) |> ignore

    member this.RewindWhenStartAfterLongPeriodInSec
        with get() = _rewindWhenStartAfterLongPeriodInSec
        and set value =
            _rewindWhenStartAfterLongPeriodInSec <- value
            Services.SystemSettings.setRewindWhenStartAfterLongPeriodInSec(value) |> ignore

    member this.LongPeriodBeginsAfterInMinutes
        with get() = _longPeriodBeginsAfterInMinutes
        and set value =
            _longPeriodBeginsAfterInMinutes <- value
            Services.SystemSettings.setLongPeriodBeginsAfterInMinutes(value) |> ignore

    member this.JumpDistance
        with get() = _jumpDistance
        and set value =
            _jumpDistance <- value
            Services.SystemSettings.setJumpDistance(value) |> ignore

    member this.IsFirstStart
        with get() = _isFirstStart
        and set value =
            _isFirstStart <- value
            Services.SystemSettings.setIsFirstStart(value) |> ignore

    member this.PlaybackSpeed
        with get() = _playbackSpeed
        and set value =
            _playbackSpeed <- value
            Services.SystemSettings.setPlaybackSpeed(value) |> ignore



