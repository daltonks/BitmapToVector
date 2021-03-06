[![NuGet BitmapToVector](https://img.shields.io/nuget/v/BitmapToVector?label=BitmapToVector)](https://www.nuget.org/packages/BitmapToVector)

[![NuGet BitmapToVector.SkiaSharp](https://img.shields.io/nuget/v/BitmapToVector.SkiaSharp?label=BitmapToVector.SkiaSharp)](https://www.nuget.org/packages/BitmapToVector.SkiaSharp)

# BitmapToVector
.NET Standard port of [Potrace](http://potrace.sourceforge.net/). In this port, I aim to keep the syntax and comments as close to the C version as possible. This will help with keeping up to date on any new Potrace versions. I only aim to use typical C# syntax in the public API.

Usage:

1. Install the [NuGet package](https://www.nuget.org/packages/BitmapToVector/)
2. Write some code:
```cs
// Create PotraceBitmap
var width = 100;
var height = 100;
using var potraceBitmap = PotraceBitmap.Create(width, height);

// Add black pixels
potraceBitmap.SetBlackUnsafe(0, 0);
potraceBitmap.SetBlackUnsafe(0, 1);

// Create options
var param = new PotraceParam();

// Trace using Potrace
var traceResult = Potrace.Trace(param, potraceBitmap);
```

3. Refer to Potrace's [technical documentation](http://potrace.sourceforge.net/potracelib.pdf) for more information.

# BitmapToVector.SkiaSharp
Builds off of BitmapToVector and adds support for `SKPath` and `SKBitmap`.

Usage:

1. Install the [NuGet package](https://www.nuget.org/packages/BitmapToVector.SkiaSharp/)
2. Install the [SkiaSharp NuGet package](https://www.nuget.org/packages/SkiaSharp/)
3. Use these additional methods:

```cs
IEnumerable<SKPath> skPathsFromSKBitmap = PotraceSkiaSharp.Trace(PotraceParam, SKBitmap);
IEnumerable<SKPath> skPathsFromPotraceBitmap = PotraceSkiaSharp.Trace(PotraceParam, PotraceBitmap);
```

## Licensing
Please note the GPL-3.0 license. This can have big implications if you decide to use this library.
