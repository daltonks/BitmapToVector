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

            long numBlack = 0;
            // TODO: Change back to for (var y = 0; y < height; y++)
            for (var y = height - 1; y >= 0; y--)
            for (var x = 0; x < width; x++)
            {
                // For speed, only check 1 byte for the pixel.
                // This is the red color if the ColorType has a red component.
                if (*ptr < 124)
                {
                    potraceBitmap.SetBlackUnsafe(x, y);
                    numBlack++;
                }

                ptr += bytesPerPixel;
            }

            var potracePath = Potrace.Trace(param, potraceBitmap).Plist;
            for (var i = 0; i < potracePath.Curve.N; i++)
            {
                var tag = potracePath.Curve.Tag[i];
                var segment = potracePath.Curve.C[i];
                Debug.WriteLine($"Tag: {tag}. Segment: [{string.Join(", ", segment.Select(s => $"({s.X}, {s.Y})"))}]");
            }
            var path = new SKPath();

            var potraceCurve = potracePath.Curve;

            var first = true;
            var curX = 0f;
            var curY = 0f;
            var prevX = 0f;
            var prevY = 0f;
            var inX = 0f;
            var inY = 0f;
            var outX = 0f;
            var outY = 0f;

            for (var i = 0; i < potraceCurve.N; i++)
            {
                var tag = potraceCurve.Tag[i];
                var segment = potraceCurve.C[i];
                ApplySegment(tag, segment);
            }
            ApplySegment(potraceCurve.Tag[0], potraceCurve.C[0]);

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

            void ApplySegment(int tag, PotraceDPoint[] segment)
            {
                PotraceDPoint handleIn, handleOut, potracePoint;
                if (tag == PotraceCurve.PotraceCorner)
                {
                    handleIn = handleOut = potracePoint = segment[0];
                }
                else
                {
                    handleIn = segment[0];
                    handleOut = segment[1];
                    potracePoint = segment[2];
                }

                curX = (float) potracePoint.X;
                curY = (float) potracePoint.Y;
                if (first)
                {
                    path.MoveTo(curX, curY);
                    first = false;
                }
                else
                {
                    inX = (float) handleIn.X;
                    inY = (float) handleIn.Y;

                    if (inX == curX && inY == curY
                                    && outX == prevX && outY == prevY)
                    {
                        path.LineTo(curX, curY);
                    }
                    else
                    {
                        path.CubicTo(outX, outY, inX, inY, curX, curY);
                    }
                }

                prevX = curX;
                prevY = curY;
                
                outX = (float) handleOut.X;
                outY = (float) handleOut.Y;
            }
        }
    }
}
