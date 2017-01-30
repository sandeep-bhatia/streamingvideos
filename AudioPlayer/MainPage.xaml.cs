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
using System.Windows;
using System.Windows.Threading;
using Microsoft.Phone.Controls;
using Microsoft.Phone.BackgroundAudio;
using Microsoft.Phone.Shell;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using PlayStation;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Collections.ObjectModel;

namespace StreamPlayer
{
    public class ItemDownload
    {
        public void OnPlayItemAdded(System.Windows.Threading.Dispatcher dispatcher)
        {
            dispatcher.BeginInvoke(new Action(() =>
            {

            }));
        }
    }


    public class Data
    {
        ObservableCollection<Image> images = new ObservableCollection<Image>();
        public ObservableCollection<Image> Images
        {
            get
            {
                return images;
            }
        }
    }

    public partial class MainPage : PhoneApplicationPage
    {
        // Timer for updating the UI
        DispatcherTimer _timer;
        List<YouTubeQueryResponse> listTracks;
        // Indexes into the array of ApplicationBar.Buttons
        const int prevButton = 0;
        const int playButton = 1;
        const int pauseButton = 2;
        const int nextButton = 3;
        int currentTrack = -1;
        const string SEPERATOR = "+";
        int trackLength = 0;
        int currentTrackCount = 0;
        bool isCachingInProgress = false;
        public Data dataImages = new Data();
        // Constructor
        public MainPage()
        {
            InitializeComponent();
            LoadingMessage.Visibility = System.Windows.Visibility.Collapsed;
            Loaded += new RoutedEventHandler(MainPage_Loaded);
            loadingwait.Visibility = System.Windows.Visibility.Collapsed;
            BaseImage.Visibility = System.Windows.Visibility.Visible;
            Cache.Visibility = System.Windows.Visibility.Collapsed;
            UpdateButtons(false, false, false, false);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize a timer to update the UI every half-second.
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(0.5);
            _timer.Tick += new EventHandler(UpdateState);

            BackgroundAudioPlayer.Instance.PlayStateChanged += new EventHandler(Instance_PlayStateChanged);

            if (BackgroundAudioPlayer.Instance.PlayerState == PlayState.Playing)
            {
                // If audio was already playing when the app was launched, update the UI.
                positionIndicator.IsIndeterminate = false;
                positionIndicator.Maximum = BackgroundAudioPlayer.Instance.Track.Duration.TotalSeconds;
                UpdateButtons(true, false, true, true);
                UpdateState(null, null);
            }
        }


        /// <summary>
        /// PlayStateChanged event handler.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Instance_PlayStateChanged(object sender, EventArgs e)
        {
            switch (BackgroundAudioPlayer.Instance.PlayerState)
            {
                case PlayState.Playing:
                    // Update the UI.
                    positionIndicator.IsIndeterminate = false;
                    positionIndicator.Maximum = BackgroundAudioPlayer.Instance.Track.Duration.TotalSeconds;
                    UpdateButtons(true, false, true, true);
                    UpdateState(null, null);

                    // Start the timer for updating the UI.
                    _timer.Start();
                    break;
                case PlayState.Paused:
                    // Update the UI.
                    UpdateButtons(true, true, false, true);
                    UpdateState(null, null);

                    // Stop the timer for updating the UI.
                    _timer.Stop();
                    break;
                case PlayState.TrackEnded:
                    PlayNextTrack();
                    break;
                case PlayState.TrackReady:
                    UpdateButtons(true, true, false, true);
                    break;
                case PlayState.BufferingStarted:
                    UpdateButtons(false, false, false, false);
                    break;

            }
        }


        private void PlayTrack(int index)
        {
            GetTrack(index, new Action(() =>
            {
                BackgroundAudioPlayer.Instance.Play();
            }));
        }

        private void PlayNextTrack()
        {
            currentTrack++;
            GetTrack(currentTrack, new Action(() =>
            {
                BackgroundAudioPlayer.Instance.Play();
            }));
        }




        /// <summary>
        /// Helper method to update the state of the ApplicationBar.Buttons
        /// </summary>
        /// <param name="prevBtnEnabled"></param>
        /// <param name="playBtnEnabled"></param>
        /// <param name="pauseBtnEnabled"></param>
        /// <param name="nextBtnEnabled"></param>
        void UpdateButtons(bool prevBtnEnabled, bool playBtnEnabled, bool pauseBtnEnabled, bool nextBtnEnabled)
        {
            // Set the IsEnabled state of the ApplicationBar.Buttons array
            ((ApplicationBarIconButton)(ApplicationBar.Buttons[prevButton])).IsEnabled = prevBtnEnabled;
            ((ApplicationBarIconButton)(ApplicationBar.Buttons[playButton])).IsEnabled = playBtnEnabled;
            ((ApplicationBarIconButton)(ApplicationBar.Buttons[pauseButton])).IsEnabled = pauseBtnEnabled;
            ((ApplicationBarIconButton)(ApplicationBar.Buttons[nextButton])).IsEnabled = nextBtnEnabled;
        }


        /// <summary>
        /// Updates the status indicators including the State, Track title, 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UpdateState(object sender, EventArgs e)
        {
            try
            {
                if (BackgroundAudioPlayer.Instance.Track != null)
                {
                    txtTrack.Text = string.Format("Track: {0}", BackgroundAudioPlayer.Instance.Track.Title);

                    // Set the current position on the ProgressBar.
                    positionIndicator.Value = BackgroundAudioPlayer.Instance.Position.TotalSeconds;

                    // Update the current playback position.
                    TimeSpan position = new TimeSpan();
                    position = BackgroundAudioPlayer.Instance.Position;
                    textPosition.Text = String.Format("{0:d2}:{1:d2}:{2:d2}", position.Hours, position.Minutes, position.Seconds);

                    // Update the time remaining digits.
                    TimeSpan timeRemaining = new TimeSpan();
                    timeRemaining = BackgroundAudioPlayer.Instance.Track.Duration - position;
                    textRemaining.Text = String.Format("-{0:d2}:{1:d2}:{2:d2}", timeRemaining.Hours, timeRemaining.Minutes, timeRemaining.Seconds);
                }
            }
            catch
            {
                //just don't bail oout if user repeatedly clicked and updated the state and properties like position were invalid
            }
        }


        /// <summary>
        /// Click handler for the Skip Previous button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void prevButton_Click(object sender, EventArgs e)
        {
            // Show the indeterminate progress bar.
            positionIndicator.IsIndeterminate = true;

            // Disable the button so the user can't click it multiple times before 
            // the background audio agent is able to handle their request.
            ((ApplicationBarIconButton)(ApplicationBar.Buttons[prevButton])).IsEnabled = false;
            currentTrack--;
            if (currentTrack == -1)
            {
                currentTrack = listTracks.Count - 1;
            }

            GetTrack(currentTrack, new Action(() =>
            {
                BackgroundAudioPlayer.Instance.SkipPrevious();
            }));
            // Disable the button so the user can't click it multiple times before 
            // the background audio agent is able to handle their request.
            ((ApplicationBarIconButton)(ApplicationBar.Buttons[prevButton])).IsEnabled = true;
        }


        /// <summary>
        /// Click handler for the Play button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void playButton_Click(object sender, EventArgs e)
        {
            if (currentTrack == -1)
            {
                currentTrack = 0;
            }
            GetTrack(currentTrack, new Action(() =>
            {
                BackgroundAudioPlayer.Instance.Play();
            }));
        }


        /// <summary>
        /// Click handler for the Pause button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pauseButton_Click(object sender, EventArgs e)
        {
            // Tell the backgound audio agent to pause the current track.
            BackgroundAudioPlayer.Instance.Pause();
        }


        /// <summary>
        /// Click handler for the Skip Next button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void nextButton_Click(object sender, EventArgs e)
        {
            // Show the indeterminate progress bar.
            positionIndicator.IsIndeterminate = true;

            // Disable the button so the user can't click it multiple times before 
            // the background audio agent is able to handle their request.
            ((ApplicationBarIconButton)(ApplicationBar.Buttons[nextButton])).IsEnabled = false;

            try
            {
                currentTrack++;
                if (currentTrack > listTracks.Count - 1 || currentTrack < 0)
                {
                    currentTrack = 0;
                }

                GetTrack(currentTrack, new Action(() =>
                {
                    BackgroundAudioPlayer.Instance.Play();
                }));
            }
            finally
            {
                ((ApplicationBarIconButton)(ApplicationBar.Buttons[nextButton])).IsEnabled = true;
            }
        }

        private void GetTrack(int currentTrack, Action T)
        {
            var dispatcher = Application.Current.RootVisual.Dispatcher;


            listTracks[currentTrack].GetUri(new Action<Exception, YouTubeQueryResponse>((ex, item) =>
            {
                dispatcher.BeginInvoke(new Action(() =>
                {
                    AudioTrack track = null;
                    do
                    {
                        track = listTracks[currentTrack].GetTrack();

                        if (track == null)
                        {
                            currentTrack++;
                        }
                    }
                    while (track == null);

                    BackgroundAudioPlayer.Instance.Track = track;
                    LoadingImage.Source = new BitmapImage(track.AlbumArt);
                    Cache.Content = item.InCache ? "InCache" : "Cache";
                    T();
                    if (!item.IsAdded)
                    {
                        Image imageSrc = new Image();
                        imageSrc.Width = 120;
                        imageSrc.Height = 120;
                        imageSrc.Source = new BitmapImage(new Uri(item.ThumbnailUrl));
                        dataImages.Images.Add(imageSrc);
                        Queue.ItemsSource = dataImages.Images;
                        if ((++currentTrackCount) == trackLength)
                        {
                            LoadingMessage.Visibility = System.Windows.Visibility.Collapsed;
                        }

                        item.IsAdded = true;
                    }
                }));
            }));
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            LoadingMessage.Visibility = System.Windows.Visibility.Visible;
            GetSearchItems();
        }

        private void GetSearchItems()
        {
            string query = QueryText.Text;
            trackLength = 0;
            listTracks = null;
            dataImages.Images.Clear();
            LoadingImage.Source = null;
            BaseImage.Visibility = System.Windows.Visibility.Collapsed;
            loadingwait.Visibility = System.Windows.Visibility.Visible;

            if (!string.IsNullOrWhiteSpace(query))
            {
                string[] queryElements = query.Split(' ');
                StringBuilder builder = new StringBuilder();
                builder.Append(queryElements[0]);

                for (var index = 1; index < queryElements.Length; index++)
                {
                    builder.Append(SEPERATOR);
                    builder.Append(queryElements[index]);
                }
                var dispatcher = Application.Current.RootVisual.Dispatcher;
                UpdateButtons(false, false, false, false);

                PlayStation.YouTubeClient.QueryYoutubeVideos(builder.ToString(), new Action<List<YouTubeQueryResponse>>((obj) =>
                {
                    if (obj != null)
                    {
                        trackLength = obj.Count;
                        listTracks = obj;

                        dispatcher.BeginInvoke(() =>
                        {
                            UpdateButtons(true, true, true, true);
                            loadingwait.Visibility = System.Windows.Visibility.Collapsed;
                            LoadingMessage.Visibility = System.Windows.Visibility.Collapsed;
                            Cache.Visibility = System.Windows.Visibility.Visible;
                            PlayNextTrack();
                        });
                    }
                }));
            }
        }

        private void Queue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int index = -1;
            try
            {
                index = dataImages.Images.IndexOf(Queue.SelectedItem as Image);
            }
            catch
            {
                Debug.Assert(false, "error in list selected item");
            }

            if (index >= 0)
            {
                UpdatePlayTrackImage(index, 120, 120);
                UpdatePlayTrackImage(currentTrack, 75, 75);
                currentTrack = index;
                PlayTrack(index);
            }
        }

        private void UpdatePlayTrackImage(int index, int h, int w)
        {
            var imageUri = new Uri(listTracks[index].ThumbnailUrl);
            var imageSrc = new Image();
            imageSrc.Width = h;
            imageSrc.Height = w;
            imageSrc.Source = new BitmapImage(imageUri);
            dataImages.Images[index] = imageSrc;
        }

        private void Cache_Click(object sender, RoutedEventArgs e)
        {
            if (listTracks != null && listTracks.Count > 0)
            {
                if (currentTrack >= 0 && currentTrack < listTracks.Count)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        Cache.Content = "Caching In Progress";
                        UpdateButtons(false, true, true, false);
                        try
                        {
                            isCachingInProgress = true;
                            listTracks[currentTrack].GetUri(new Action<Exception, YouTubeQueryResponse>((ex, item) =>
                           {
                               item.Download(
                                            new Action<int>((result) =>
                                            {
                                                Dispatcher.BeginInvoke(() =>
                                               {
                                                   Cache.Content = result == 0 ? "InCache" : "Cache Failure, Please Retry";
                                                   isCachingInProgress = false;
                                                   UpdateButtons(true, true, true, true);
                                               });
                                            })
                                );
                           }));
                        }
                        catch { }
                    });
                }
            }
        }
    }
}
