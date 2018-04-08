using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;

namespace TinyYOLO.VideoEffects.Helpers
{
    public static class Extensions
    {
        /// <summary>
        /// Finds a matching DirectXPixelFormat for a BitmapPixelFormat.
        /// </summary>
        /// <param name="bitmapPixelFormat">BitmapPixelFormat, usually from a SoftwareBitmap</param>
        /// <returns>Matching DirectXPixelFormat, defaults to B8G8R8A8UIntNormalized if no match is found</returns>
        public static DirectXPixelFormat ToDirectXPixelFormat(this BitmapPixelFormat bitmapPixelFormat)
        {
            switch (bitmapPixelFormat)
            {
                case BitmapPixelFormat.Unknown:
                    break;
                case BitmapPixelFormat.Rgba16:
                    return DirectXPixelFormat.R16G16B16A16IntNormalized;
                case BitmapPixelFormat.Rgba8:
                    return DirectXPixelFormat.R8G8B8A8IntNormalized;
                case BitmapPixelFormat.Bgra8:
                    return DirectXPixelFormat.B8G8R8A8UIntNormalized;
                case BitmapPixelFormat.Nv12:
                    return DirectXPixelFormat.NV12;
                case BitmapPixelFormat.Yuy2:
                    return DirectXPixelFormat.Yuy2;
                case BitmapPixelFormat.Gray16:
                case BitmapPixelFormat.Gray8:
                default:
                    return DirectXPixelFormat.B8G8R8A8UIntNormalized;
            }

            return DirectXPixelFormat.B8G8R8A8UIntNormalized;
        }
    }
}
