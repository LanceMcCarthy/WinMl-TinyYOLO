using System;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using TinyYOLO.VideoEffects;

namespace TinyYOLO
{
    public sealed partial class VideoPage : Page
    {
        public VideoPage()
        {
            this.InitializeComponent();
        }

        private async void LoadVideoButton_OnClick(object sender, RoutedEventArgs e)
        {
            var fileOpenPicker = new FileOpenPicker();
            fileOpenPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            fileOpenPicker.FileTypeFilter.Add(".mp4");
            fileOpenPicker.ViewMode = PickerViewMode.Thumbnail;
            var selectedStorageFile = await fileOpenPicker.PickSingleFileAsync();
            
            VideoPlayer.Source = new Uri(selectedStorageFile.Path);

            StartProcessingButton.IsEnabled = true;
        }

        private void StartProcessingButton_OnClick(object sender, RoutedEventArgs e)
        {
            VideoPlayer.AddVideoEffect(typeof(YoloVideoEffectDefinition).FullName,false,null);

            VideoPlayer.Play();
        }
    }
}
