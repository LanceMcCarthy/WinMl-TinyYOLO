using TinyYOLO.Models;

namespace TinyYOLO.ViewModels
{
    public class ViewModelBase : ObservableObject
    {
        private bool isBusy;
        private string isBusyMessage = "working...";

        public bool IsBusy
        {
            get => isBusy;
            set => SetProperty(ref isBusy, value);
        }

        public string IsBusyMessage
        {
            get => isBusyMessage;
            set => SetProperty(ref isBusyMessage, value);
        }
    }
}