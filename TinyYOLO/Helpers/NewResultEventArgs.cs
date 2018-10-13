using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media;

namespace TinyYOLO.Helpers
{
    public class NewResultEventArgs<TAnalysisResultType> : EventArgs
    {
        public NewResultEventArgs(VideoFrame frame)
        {
            Frame = frame;
        }

        public VideoFrame Frame { get; }

        public TAnalysisResultType Analysis { get; set; } = default(TAnalysisResultType);

        public bool TimedOut { get; set; } = false;

        public Exception Exception { get; set; } = null;
    }
}
