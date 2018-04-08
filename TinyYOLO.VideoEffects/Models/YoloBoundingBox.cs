using System.Drawing;

namespace TinyYOLO.VideoEffects.Models
{
    internal class YoloBoundingBox
    {
        public string Label { get; set; }
        public float X { get; set; }
        public float Y { get; set; }

        public float Height { get; set; }
        public float Width { get; set; }

        public float Confidence { get; set; }

        public RectangleF Rect => new RectangleF(X, Y, Width, Height);
    }
}
