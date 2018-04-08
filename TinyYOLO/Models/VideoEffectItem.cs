using System;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;

namespace TinyYOLO.Models
{
    public class VideoEffectItem : ObservableObject
    {
        private Type videoEffect;
        private string displayName;
        private float minPropertyValue;
        private float maxPropertyValue;
        private object propertyValue;
        private string propertyName;
        private string iconImagePath;
        private bool isSelected;
        private bool isSliderEnabled;
        private bool isToggleEnabled;
        private Visibility sliderVisiblility = Visibility.Collapsed;
        private Visibility toggleVisibility = Visibility.Collapsed;

        public VideoEffectItem() { }

        public VideoEffectItem(Type effect, string effectName)
        {
            videoEffect = effect;
            displayName = effectName;
            iconImagePath = $"ms-appx:///Images/{effectName}.jpg";
        }

        public VideoEffectItem(Type effect, string effectName, string propertyName, bool defaultValue)
        {
            videoEffect = effect;
            displayName = effectName;
            iconImagePath = $"ms-appx:///Images/{effectName}.jpg";
            this.propertyName = propertyName;
            propertyValue = defaultValue;
            isToggleEnabled = true;
        }

        public VideoEffectItem(Type effect, string effectName, string propertyName, float defaultValue, float maxValue, float minValue = 0f)
        {
            videoEffect = effect;
            displayName = effectName;
            iconImagePath = $"ms-appx:///Images/{effectName}.jpg";
            this.propertyName = propertyName;
            propertyValue = defaultValue;
            maxPropertyValue = maxValue;
            minPropertyValue = minValue;
            isSliderEnabled = true;
        }

        public Type VideoEffect
        {
            get => videoEffect;
            set => SetProperty(ref videoEffect, value);
        }

        public string DisplayName
        {
            get => displayName;
            set => SetProperty(ref displayName, value);
        }

        public float MinPropertyValue
        {
            get => minPropertyValue;
            set => SetProperty(ref minPropertyValue, value);
        }

        public float MaxPropertyValue
        {
            get => maxPropertyValue;
            set => SetProperty(ref maxPropertyValue, value);
        }

        public object PropertyValue
        {
            get => propertyValue;
            set => SetProperty(ref propertyValue, value);
        }

        public string PropertyName
        {
            get => propertyName;
            set => SetProperty(ref propertyName, value);
        }

        public string IconImagePath
        {
            get => iconImagePath;
            set => SetProperty(ref iconImagePath, value);
        }

        public bool IsSelected
        {
            get => isSelected;
            set => SetProperty(ref isSelected, value);
        }

        public bool IsSliderEnabled
        {
            get => isSliderEnabled;
            set => SetProperty(ref isSliderEnabled, value);
        }

        public Visibility SliderVisiblility
        {
            get => sliderVisiblility;
            set => SetProperty(ref sliderVisiblility, value);
        }

        public bool IsToggleEnabled
        {
            get => isToggleEnabled;
            set => SetProperty(ref isToggleEnabled, value);
        }

        public Visibility ToggleVisibility
        {
            get => toggleVisibility;
            set => SetProperty(ref toggleVisibility, value);
        }
    }
}