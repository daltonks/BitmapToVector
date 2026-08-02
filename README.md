# BitmapToVector

[![NuGet BitmapToVector](https://img.shields.io/nuget/v/BitmapToVector?label=BitmapToVector)](https://www.nuget.org/packages/BitmapToVector)
[![NuGet BitmapToVector.SkiaSharp](https://img.shields.io/nuget/v/BitmapToVector.SkiaSharp?label=BitmapToVector.SkiaSharp)](https://www.nuget.org/packages/BitmapToVector.SkiaSharp)

A .NET Standard port of [Potrace](http://potrace.sourceforge.net/), which turns a black-and-white bitmap into smooth vector outlines. It's the algorithm behind Inkscape's Trace Bitmap, in managed C# with no native dependency.

The port stays as close to the C source as it can: same structure, same comments, same file names. That makes new Potrace releases easier to fold in. Only the public API is written in normal C#.

> **Read the license section before you use this.** Potrace is GPL, so this port is too.

## Packages

| Package | What it adds |
| --- | --- |
| `BitmapToVector` | The port itself. Traces a `PotraceBitmap` into Potrace's own curve types. |
| `BitmapToVector.SkiaSharp` | The same, but takes an `SKBitmap` and hands back `SKPath` objects. |

## BitmapToVector

```
dotnet add package BitmapToVector
```

```cs
using var potraceBitmap = PotraceBitmap.Create(100, 100);

// Fill in the pixels you want traced
potraceBitmap.SetBlack(0, 0);
potraceBitmap.SetBlack(0, 1);

var param = new PotraceParam();
var traceResult = Potrace.Trace(param, potraceBitmap);

// traceResult.Plist is a linked list of PotracePath, each with a Curve of
// bezier and corner segments, and ChildList/Sibling forming the hole tree.
```

A `PotraceBitmap` is one bit per pixel, black or white. Deciding which pixels should be black is up to you, whether that means a threshold, edge detection, or an alpha test. This library doesn't do it.

Every pixel operation comes in two forms. `SetBlack`, `SetWhite`, `SetColor`, `InverseColor`, and `IsBlack` bounds-check first, and ignore coordinates outside the bitmap. The `...Unsafe` versions skip the check. They're faster when you're already looping within known bounds, and they will corrupt memory when you aren't.

### Tracing options

`PotraceParam` mirrors the C library's parameters:

| Property | Default | Effect |
| --- | --- | --- |
| `TurdSize` | 2 | Drops any path enclosing fewer pixels than this. Raise it to shed speckle. |
| `TurnPolicy` | `PotraceTurnpolicyMinority` | How ambiguous turns get resolved. See the `PotraceTurnPolicy*` constants. |
| `AlphaMax` | 1.0 | Corner threshold. 0 makes every junction a corner; 1.334 and up makes them all smooth. |
| `OptiCurve` | true | Join adjacent bezier segments where one curve will do. |
| `OptTolerance` | 0.2 | How much error that joining may introduce. |

Potrace's [technical documentation](http://potrace.sourceforge.net/potracelib.pdf) covers all of them in more depth and applies directly here.

## BitmapToVector.SkiaSharp

```
dotnet add package BitmapToVector.SkiaSharp
dotnet add package SkiaSharp
```

Skips the curve types and gives you paths you can draw with:

```cs
IEnumerable<SKPath> paths = PotraceSkiaSharp.Trace(new PotraceParam(), skBitmap);
```

There is also an overload taking a `PotraceBitmap`, for when you want to build the bitmap yourself.

Converting an `SKBitmap` counts a pixel as black when its **red** channel is under 128. If your image needs different logic, build a `PotraceBitmap` and use that overload instead.

## Demo

[`src/BitmapToVector.Demo`](src/BitmapToVector.Demo) is a Xamarin.Forms app that traces an image and draws the result.

## License

GPL-3.0-or-later, inherited from Potrace itself.

The GPL is copyleft. If you distribute software that links this library, that software has to be GPL as well, with source available. That rules it out for most closed-source and commercial work. [Potrace offers commercial licensing](http://potrace.sourceforge.net/#license) if you need different terms.
