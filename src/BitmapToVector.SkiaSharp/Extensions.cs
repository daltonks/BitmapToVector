using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace BitmapToVector.SkiaSharp
{
    public static class Extensions
    {
        public static unsafe SKPath Trace(this SKBitmap bitmap, PotraceParam param)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            using var potraceBitmap = PotraceBitmap.Create(width, height);

            var (bytesPerPixel, bytesOffset) = GetBytesInfo();
            var pixelsIntPtr = bitmap.GetPixels();
            var ptr = (byte*)pixelsIntPtr.ToPointer() + bytesOffset;
            
            // TODO: Change back to for (var y = 0; y < height; y++)
            //for (var y = height - 1; y >= 0; y--)
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                // For speed, only check 1 byte for the pixel.
                // This is the red color if the ColorType has a red component.
                if (*ptr < 124)
                {
                    potraceBitmap.SetBlackUnsafe(x, y);
                }

                ptr += bytesPerPixel;
            }

            var potracePath = Potrace.Trace(param, potraceBitmap).Plist;

            var path = new SKPath();

            var potraceCurve = potracePath.Curve;

            var lastPoint = potraceCurve.C[potraceCurve.N - 1][2];
            path.MoveTo((float) lastPoint.X, (float) lastPoint.Y);

            for (var i = 0; i < potraceCurve.N; i++)
            {
                var tag = potraceCurve.Tag[i];
                var segment = potraceCurve.C[i];
                if (tag == PotraceCurve.PotraceCorner)
                {
                    var firstPoint = segment[1];
                    var secondPoint = segment[2];
                    path.LineTo((float) firstPoint.X, (float) firstPoint.Y);
                    path.LineTo((float) secondPoint.X, (float) secondPoint.Y);
                }
                else
                {
                    var handle1 = segment[0];
                    var handle2 = segment[1];
                    var potracePoint = segment[2];
                    path.CubicTo(
                        (float) handle1.X, (float) handle1.Y, 
                        (float) handle2.X, (float) handle2.Y, 
                        (float) potracePoint.X, (float) potracePoint.Y
                    );
                }
            }

            return path;

            (int BytesPerPixel, int Offset) GetBytesInfo()
            {
                var colorType = bitmap.ColorType;
                switch (colorType)
                {
                    case SKColorType.Alpha8:
                        return (BytesPerPixel: 1, Offset: 0);
                    case SKColorType.Rgba8888:
                        return (BytesPerPixel: 4, Offset: 0);
                    case SKColorType.Rgb888x:
                        return (BytesPerPixel: 4, Offset: 0);
                    case SKColorType.Bgra8888:
                        return (BytesPerPixel: 4, Offset: 2);
                    case SKColorType.Gray8:
                        return (BytesPerPixel: 1, Offset: 0);
                    case SKColorType.Rg88:
                        return (BytesPerPixel: 2, Offset: 0);
                    
                    case SKColorType.Unknown:
                    case SKColorType.Rgb565:
                    case SKColorType.Argb4444:
                    case SKColorType.Rgba1010102:
                    case SKColorType.Rgb101010x:
                    case SKColorType.RgbaF16:
                    case SKColorType.RgbaF16Clamped:
                    case SKColorType.RgbaF32:
                    case SKColorType.AlphaF16:
                    case SKColorType.RgF16:
                    case SKColorType.Alpha16:
                    case SKColorType.Rg1616:
                    case SKColorType.Rgba16161616:
                    default: throw new ArgumentOutOfRangeException(
                        $"{nameof(SKColorType)} {colorType} is not supported"
                    );
                }
            }
        }
    }
}
