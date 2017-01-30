/* 
    Copyright (c) 2011 Microsoft Corporation.  All rights reserved.
    Use of this sample source code is subject to the terms of the Microsoft license 
    agreement under which you licensed this sample source code and is provided AS-IS.
    If you did not accept the terms of the license agreement, you are not authorized 
    to use this sample source code.  For the terms of the license, please see the 
    license agreement between you and Microsoft.
  
    To see all Code Samples for Windows Phone, visit http://go.microsoft.com/fwlink/?LinkID=219604 
  
*/
using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Phone.BackgroundAudio;
using System.Diagnostics;

namespace PlayStation
{
    public class AudioPlayer : AudioPlayerAgent
    {
        private static volatile bool _classInitialized;

        /// <remarks>
        /// AudioPlayer instances can share the same process. 
        /// Static fields can be used to share state between AudioPlayer instances
        /// or to communicate with the Audio Streaming agent.
        /// </remarks>
        public AudioPlayer()
        {
            if (!_classInitialized)
            {
                _classInitialized = true;
                // Subscribe to the managed exception handler
                Deployment.Current.Dispatcher.BeginInvoke(delegate
                {
                    Application.Current.UnhandledException += AudioPlayer_UnhandledException;
                });
            }
        }

        /// Code to execute on Unhandled Exceptions
        private void AudioPlayer_UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                // An unhandled exception has occurred; break into the debugger
                System.Diagnostics.Debugger.Break();
            }
        }

        /// <summary>
        /// Called when the playstate changes, except for the Error state (see OnError)
        /// </summary>
        /// <param name="player">The BackgroundAudioPlayer</param>
        /// <param name="track">The track playing at the time the playstate changed</param>
        /// <param name="playState">The new playstate of the player</param>
        /// <remarks>
        /// Play State changes cannot be cancelled. They are raised even if the application
        /// caused the state change itself, assuming the application has opted-in to the callback.
        /// 
        /// Notable playstate events: 
        /// (a) TrackEnded: invoked when the player has no current track. The agent can set the next track.
        /// (b) TrackReady: an audio track has been set and it is now ready for playack.
        /// 
        /// Call NotifyComplete() only once, after the agent request has been completed, including async callbacks.
        /// </remarks>
        protected override void OnPlayStateChanged(BackgroundAudioPlayer player, AudioTrack track, PlayState playState)
        {
            try
            {
                switch (playState)
                {
                    case PlayState.TrackEnded:
                        break;

                    case PlayState.TrackReady:
                        player.Volume = 1.0;
                        player.Play();
                        break;

                    case PlayState.Shutdown:
                        // TODO: Handle the shutdown state here (e.g. save state)
                        break;

                    case PlayState.Unknown:
                        break;

                    case PlayState.Stopped:
                        break;

                    case PlayState.Paused:
                        break;

                    case PlayState.Playing:
                        break;

                    case PlayState.BufferingStarted:
                        break;

                    case PlayState.BufferingStopped:
                        break;

                    case PlayState.Rewinding:
                        break;

                    case PlayState.FastForwarding:
                        break;
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine(string.Format("{0} : The exception is at ", ex.Message));
            }
            finally
            {
                NotifyComplete();
            }
        }


        /// <summary>
        /// Called when the user requests an action using application/system provided UI
        /// </summary>
        /// <param name="player">The BackgroundAudioPlayer</param>
        /// <param name="track">The track playing at the time of the user action</param>
        /// <param name="action">The action the user has requested</param>
        /// <param name="param">The data associated with the requested action.
        /// In the current version this parameter is only for use with the Seek action,
        /// to indicate the requested position of an audio track</param>
        /// <remarks>
        /// User actions do not automatically make any changes in system state; the agent is responsible
        /// for carrying out the user actions if they are supported.
        /// 
        /// Call NotifyComplete() only once, after the agent request has been completed, including async callbacks.
        /// </remarks>
        protected override void OnUserAction(BackgroundAudioPlayer player, AudioTrack track, UserAction action, object param)
        {
            try
            {
                switch (action)
                {
                    case UserAction.Play:
                        if (PlayState.Playing != player.PlayerState)
                        {
                            player.Play();
                        }
                        break;

                    case UserAction.Stop:
                        if (PlayState.Playing == player.PlayerState)
                        {
                            player.Stop();
                        }

                        break;

                    case UserAction.Pause:
                        if (PlayState.Playing == player.PlayerState)
                        {
                            player.Pause();
                        }
                        break;

                    case UserAction.FastForward:
                        // Fast Forward only works with non-MSS clients.
                        // If the Source is null, we are streaming an MSS.
                        if (track.Source != null)
                        {
                            player.FastForward();
                        }
                        break;

                    case UserAction.Rewind:
                        // Rewind only works with non-MSS clients.
                        // If the Source is null, we are streaming an MSS.
                        if (track.Source != null)
                        {
                            player.Rewind();
                        }
                        break;

                    case UserAction.Seek:
                        // Seek only works with non-MSS clients.
                        // If the Source is null, we are streaming an MSS.
                        if (track.Source != null)
                        {
                            player.Position = (TimeSpan)param;
                        }
                        break;

                    case UserAction.SkipNext:
                       break;

                    case UserAction.SkipPrevious:
                       break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("{0} : Exception thrown", ex.Message));
            }
            finally
            {
                NotifyComplete();
            }
        }

        /// <summary>
        /// Called whenever there is an error with playback, such as an AudioTrack not downloading correctly
        /// </summary>
        /// <param name="player">The BackgroundAudioPlayer</param>
        /// <param name="track">The track that had the error</param>
        /// <param name="error">The error that occured</param>
        /// <param name="isFatal">If true, playback cannot continue and playback of the track will stop</param>
        /// <remarks>
        /// This method is not guaranteed to be called in all cases. For example, if the background agent 
        /// itself has an unhandled exception, it won't get called back to handle its own errors.
        /// </remarks>
        protected override void OnError(BackgroundAudioPlayer player, AudioTrack track, Exception error, bool isFatal)
        {
            if (isFatal)
            {
                Debug.WriteLine(string.Format("{0} : Exception thrown", error.Message));
            }
            else
            {
                NotifyComplete();
            }

        }

        /// <summary>
        /// Called when the agent request is getting cancelled
        /// </summary>
        /// <remarks>
        /// Once the request is Cancelled, the agent gets 5 seconds to finish its work,
        /// by calling NotifyComplete()/Abort().
        /// </remarks>
        protected override void OnCancel()
        {
            // Do any necessary cleanup work, such as saving state.
        }
    }
}
