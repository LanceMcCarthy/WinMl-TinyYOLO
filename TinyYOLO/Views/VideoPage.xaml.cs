﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using Windows.Media.Effects;
using Windows.UI.Input;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using TinyYOLO.Helpers;
using TinyYOLO.Models;

namespace TinyYOLO.Views
{
    public sealed partial class VideoPage : Page
    {
        #region Fields

        // General
        private RecordingState _currentState = RecordingState.NotInitialized;

        // Camera API fields
        private MediaCapture _mediaCapture;
        private DeviceInformation _selectedCamera;

        // Effect fields
        private IVideoEffectDefinition _previewEffect;
        private IPropertySet _effectPropertySet;

        #endregion

        public VideoPage()
        {
            InitializeComponent();
        }
        
        #region ListView Related

        private async void EffectsListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PageViewModel.SelectedEffect == null)
                return;

            await ClearVideoEffectsAsync();

            // If the user selected "None" the effect will be null
            if (PageViewModel.SelectedEffect.VideoEffect == null)
                return;
            
            await ApplyVideoEffectAsync();
        }

        #endregion
        
        #region Video effect selection, creation and management

        private IVideoEffectDefinition ConstructVideoEffect()
        {
            if (string.IsNullOrEmpty(PageViewModel.SelectedEffect.VideoEffect.FullName))
                return null;

            if (string.IsNullOrEmpty(PageViewModel.SelectedEffect.PropertyName))
                return new VideoEffectDefinition(PageViewModel.SelectedEffect.VideoEffect.FullName);

            _effectPropertySet = new PropertySet();
            _effectPropertySet[PageViewModel.SelectedEffect.PropertyName] = PageViewModel.SelectedEffect.PropertyValue;

            return new VideoEffectDefinition(PageViewModel.SelectedEffect.VideoEffect.FullName, _effectPropertySet);
        }

        private async Task ApplyVideoEffectAsync()
        {
            if (_currentState == RecordingState.Previewing)
            {
                _previewEffect = ConstructVideoEffect();
                await _mediaCapture.AddVideoEffectAsync(_previewEffect, MediaStreamType.VideoPreview);
            }
            else if (_currentState == RecordingState.NotInitialized || _currentState == RecordingState.Stopped)
            {
                await new MessageDialog("The preview or recording stream is not available.", "Effect not applied").ShowAsync();
            }
        }

        private async Task ClearVideoEffectsAsync()
        {
            await _mediaCapture.ClearEffectsAsync(MediaStreamType.VideoPreview);
            _previewEffect = null;
        }
        
        #endregion

        #region MediaCapture initialization and disposal

        private async Task InitializeVideoAsync()
        {
            ReloadVideoStreamButton.Visibility = Visibility.Collapsed;
            ShowBusyIndicator("Initializing...");

            try
            {
                _currentState = RecordingState.NotInitialized;

                PreviewMediaElement.Source = null;

                ShowBusyIndicator("starting video device...");

                _mediaCapture = new MediaCapture();
                App.MediaCaptureManager = _mediaCapture;

                _selectedCamera = await DeviceHelpers.FindBestCameraAsync();

                if (_selectedCamera == null)
                {
                    await new MessageDialog("There are no cameras connected, please connect a camera and try again.").ShowAsync();
                    await DisposeMediaCaptureAsync();
                    HideBusyIndicator();
                    return;
                }

                await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings { VideoDeviceId = _selectedCamera.Id });

                if (_mediaCapture.MediaCaptureSettings.VideoDeviceId != "" && _mediaCapture.MediaCaptureSettings.AudioDeviceId != "")
                {
                    ShowBusyIndicator("camera initialized..");

                    _mediaCapture.Failed += Failed;
                }
                else
                {
                    ShowBusyIndicator("camera error!");
                }

                //------starting preview----------//

                ShowBusyIndicator("starting preview...");

                PreviewMediaElement.Source = _mediaCapture;
                await _mediaCapture.StartPreviewAsync();

                _currentState = RecordingState.Previewing;

            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"InitializeVideo UnauthorizedAccessException\r\n {ex}");

                ShowBusyIndicator("Unauthorized Access Error");

                await new MessageDialog("-----Unauthorized Access Error!-----\r\n\n" +
                                        "This can happen for a couple reasons:\r\n" +
                                        "-You have disabled Camera access to the app\r\n" +
                                        "-You have disabled Microphone access to the app\r\n\n" +
                                        "To fix this, go to Settings > Privacy > Camera (or Microphone) and reenable it.").ShowAsync();

                await DisposeMediaCaptureAsync();
            }
            catch (Exception ex)
            {
                ShowBusyIndicator("Initialize Video Error");
                await new MessageDialog("InitializeVideoAsync() Exception\r\n\nError Message: " + ex.Message).ShowAsync();

                _currentState = RecordingState.NotInitialized;
                PreviewMediaElement.Source = null;
            }
            finally
            {
                HideBusyIndicator();
            }
        }

        private async Task DisposeMediaCaptureAsync()
        {
            try
            {
                ShowBusyIndicator("Freeing up resources...");

                if (_currentState == RecordingState.Recording && _mediaCapture != null)
                {
                    ShowBusyIndicator("recording stopped...");
                    await _mediaCapture.StopRecordAsync();

                }
                else if (_currentState == RecordingState.Previewing && _mediaCapture != null)
                {
                    ShowBusyIndicator("video preview stopped...");
                    await _mediaCapture.StopPreviewAsync();
                }

                _currentState = RecordingState.Stopped;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DisposeAll Error: {ex.Message}");
                await new MessageDialog($"Error disposing MediaCapture: {ex.Message}").ShowAsync();
            }
            finally
            {
                if (_mediaCapture != null)
                {
                    _mediaCapture.Failed -= Failed;
                    _mediaCapture.Dispose();
                    _mediaCapture = null;
                }

                PreviewMediaElement.Source = null;
                HideBusyIndicator();
            }
        }

        //private static async Task<DeviceInformation> FindBestCameraAsync()
        //{
        //    var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

        //    Debug.WriteLine($"{devices.Count} devices found");

        //    // If there are no cameras connected to the device
        //    if (devices.Count == 0)
        //        return null;

        //    // If there is only one camera, return that one
        //    if (devices.Count == 1)
        //        return devices.FirstOrDefault();

        //    //check if the preferred device is available
        //    var frontCamera = devices.FirstOrDefault(
        //        x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);

        //    //if front camera is available return it, otherwise pick the first available camera
        //    return frontCamera ?? devices.FirstOrDefault();
        //}

        private async void Failed(MediaCapture currentCaptureObject, MediaCaptureFailedEventArgs currentFailure)
        {
            await TaskUtilities.RunOnDispatcherThreadAsync(async () =>
            {
                await new MessageDialog(currentFailure.Message, "MediaCaptureFailed Fired").ShowAsync();

                await DisposeMediaCaptureAsync();

                ReloadVideoStreamButton.Visibility = Visibility.Visible;
            });
        }

        private async void ReloadVideoStreamButton_OnClick(object sender, RoutedEventArgs e)
        {
            await InitializeVideoAsync();
        }

        #endregion

        #region Status messaging

        private void ShowBusyIndicator(string message)
        {
            PageViewModel.IsBusyMessage = message;
            PageViewModel.IsBusy = true;
        }

        private void HideBusyIndicator()
        {
            PageViewModel.IsBusyMessage = "";
            PageViewModel.IsBusy = false;
        }

        #endregion


        #region page lifecycle

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            EffectsListView.Visibility = RadialControllerConfiguration.IsAppControllerEnabled
                ? Visibility.Collapsed
                : Visibility.Visible;

            // Set up preview video stream
            await InitializeVideoAsync();
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // Dispose camera
            await DisposeMediaCaptureAsync();
        }

        #endregion

    }
}