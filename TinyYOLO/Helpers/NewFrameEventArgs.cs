using System;
using Windows.Media;

namespace TinyYOLO.Helpers
{
    public class NewFrameEventArgs<TAnalysisResultType> : EventArgs
    {
        public NewFrameEventArgs(VideoFrame frame)
        {
            Frame = frame;
        }

        public VideoFrame Frame { get; }
    }
}
