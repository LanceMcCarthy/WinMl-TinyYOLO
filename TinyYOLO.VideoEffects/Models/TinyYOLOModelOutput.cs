using System.Collections.Generic;

namespace TinyYOLO.VideoEffects.Models
{
    public sealed class TinyYOLOModelOutput
    {
        public IList<float> grid { get; set; }
        public TinyYOLOModelOutput()
        {
            this.grid = new List<float>();
        }
    }
}
