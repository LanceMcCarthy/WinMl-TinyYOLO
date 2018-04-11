using System.Collections.ObjectModel;
using Windows.ApplicationModel;
using TinyYOLO.Models;
using TinyYOLO.VideoEffects;

namespace TinyYOLO.ViewModels
{
    public class VideoPageViewModel : ViewModelBase
    {
        private ObservableCollection<VideoEffectItem> videoEffects;
        private VideoEffectItem selectedEffect;

        public VideoPageViewModel()
        {
            if (DesignMode.DesignModeEnabled)
                return;
        }

        public ObservableCollection<VideoEffectItem> VideoEffects => videoEffects ?? (videoEffects = new ObservableCollection<VideoEffectItem>
        {
            new VideoEffectItem(null, "None"),
            new VideoEffectItem(typeof(TinyYoloVideoEffect), "TinyYolo"),
            new VideoEffectItem(typeof(SepiaVideoEffect), "Sepia", "Sepia", 0.5f, 1f)
        });

        public VideoEffectItem SelectedEffect
        {
            get => selectedEffect;
            set => SetProperty(ref selectedEffect, value);
        }
    }
}