using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using PerryRhodan.AudiobookPlayer.Wpf.Helpers;

namespace PerryRhodan.AudiobookPlayer.Wpf
{


    public class AudioPlayerImpl : AudioPlayer.IAudioServiceImplementation, AudioPlayer.IAudioPlayer
    {
        public FSharpMailboxProcessor<AudioPlayer.AudioPlayerCommand> StateMailbox =>
            AudioPlayer.audioPlayerStateMailbox(this, AudioPlayer.InformationDispatcher.audioPlayerStateInformationDispatcher);


        #region IAudioPlayer
        public FSharpAsync<FSharpOption<AudioPlayer.AudioPlayerInfo>> GetCurrentState()
        {
            Func<FSharpAsyncReplyChannel<AudioPlayer.AudioPlayerInfo>, AudioPlayer.AudioPlayerCommand> func = (x) => AudioPlayer.AudioPlayerCommand.NewGetCurrentState(x);
            var result = StateMailbox.PostAndTryAsyncReply<AudioPlayer.AudioPlayerInfo>(func.ToFSharpFunc(),2000);
            return result;

        }

        public void GotToPosition(int value)
        {
            StateMailbox.Post(AudioPlayer.AudioPlayerCommand.NewSetPosition(value));
        }

        public void JumpBackward()
        {
            StateMailbox.Post(AudioPlayer.AudioPlayerCommand.JumpBackwards);
        }

        public void JumpForward()
        {
            StateMailbox.Post(AudioPlayer.AudioPlayerCommand.JumpForward);
        }

        public void MoveBackward()
        {
            StateMailbox.Post(AudioPlayer.AudioPlayerCommand.MoveToPreviousTrack);
        }

        public void MoveForward()
        {
            StateMailbox.Post(AudioPlayer.AudioPlayerCommand.NewMoveToNextTrack(-1));
        }

        public void RunService(Domain.AudioBook value1, FSharpList<Tuple<string, int>> value2)
        {
            var cmd = AudioPlayer.AudioPlayerCommand.NewStartAudioService(value1,value2);
            StateMailbox.Post(cmd);
        }

        public void SetSleepTimer(FSharpOption<TimeSpan> value)
        {

            StateMailbox.Post(AudioPlayer.AudioPlayerCommand.NewStartSleepTimer(value));
        }

        public void StartAudio(string value1, int value2)
        {
            StateMailbox.Post(AudioPlayer.AudioPlayerCommand.NewStartAudioPlayerExtern(value1,value2));
        }

        public void StopAudio()
        {
            StateMailbox.Post(AudioPlayer.AudioPlayerCommand.NewStopAudioPlayer(false));
        }

        public void StopService()
        {
            StateMailbox.Post(AudioPlayer.AudioPlayerCommand.StopAudioService);
        }

        public void TogglePlayPause()
        {
            StateMailbox.Post(AudioPlayer.AudioPlayerCommand.TogglePlayPause);
        }


        #endregion


        #region IAudioPlayerImpl

        public FSharpAsync<AudioPlayer.AudioPlayerInfo> MoveToNextTrack(AudioPlayer.AudioPlayerInfo value)
        {
            System.Windows.MessageBox.Show("MoveToNextTrack");
            return FSharpAsync.AwaitTask(Task.FromResult( value));
        }

        public FSharpAsync<AudioPlayer.AudioPlayerInfo> MoveToPreviousTrack(AudioPlayer.AudioPlayerInfo value)
        {
            System.Windows.MessageBox.Show("MoveToPreviousTrack");
            return FSharpAsync.AwaitTask(Task.FromResult(value));
        }

        public AudioPlayer.AudioPlayerInfo OnUpdatePositionNumber(AudioPlayer.AudioPlayerInfo value)
        {
            System.Windows.MessageBox.Show("OnUpdatePositionNumber");
            return value;
        }

        

        public FSharpAsync<AudioPlayer.AudioPlayerInfo> SetPosition(AudioPlayer.AudioPlayerInfo value)
        {
            System.Windows.MessageBox.Show("SetPosition");
            return FSharpAsync.AwaitTask(Task.FromResult(value));
        }

        

        public FSharpAsync<AudioPlayer.AudioPlayerInfo> StartAudioPlayer(AudioPlayer.AudioPlayerInfo value)
        {
            System.Windows.MessageBox.Show("StartAudioPlayer");
            return FSharpAsync.AwaitTask(Task.FromResult(value));
        }

        public AudioPlayer.AudioPlayerInfo StartAudioService(AudioPlayer.AudioPlayerInfo value)
        {
            System.Windows.MessageBox.Show("StartAudioService");
            return value;
        }

        

        public AudioPlayer.AudioPlayerInfo StopAudioPlayer(AudioPlayer.AudioPlayerInfo value)
        {
            System.Windows.MessageBox.Show("StopAudioPlayer");
            return value;
        }

        public AudioPlayer.AudioPlayerInfo StopAudioService(AudioPlayer.AudioPlayerInfo value)
        {
            System.Windows.MessageBox.Show("StopAudioService");
            return value;
        }

        

        #endregion
    }



   
}
