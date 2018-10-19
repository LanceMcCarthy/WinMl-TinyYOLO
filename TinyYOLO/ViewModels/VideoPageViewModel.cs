using System.Collections.ObjectModel;
using Windows.ApplicationModel;
using TinyYOLO.Models;
using TinyYOLO.VideoEffects;

namespace TinyYOLO.ViewModels
{
    public class VideoPageViewModel : ViewModelBase
    {
        private ObservableCollection<VideoEffectItem> _videoEffects;
        private VideoEffectItem _selectedEffect;

        public VideoPageViewModel()
        {
            if (DesignMode.DesignModeEnabled || DesignMode.DesignMode2Enabled)
            {
                return;
            }
        }

        public ObservableCollection<VideoEffectItem> VideoEffects => _videoEffects ?? (_videoEffects = new ObservableCollection<VideoEffectItem>
        {
            new VideoEffectItem(null, "None"),
            new VideoEffectItem(typeof(TinyYoloVideoEffect), "TinyYolo"),
            new VideoEffectItem(typeof(SepiaVideoEffect), "Sepia", "Sepia", 0.5f, 1f)
        });

        public VideoEffectItem SelectedEffect
        {
            get => _selectedEffect;
            set => SetProperty(ref _selectedEffect, value);
        }
    }
}