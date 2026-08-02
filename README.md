# BitmapToVector

[![NuGet BitmapToVector](https://img.shields.io/nuget/v/BitmapToVector?label=BitmapToVector)](https://www.nuget.org/packages/BitmapToVector)
[![NuGet BitmapToVector.SkiaSharp](https://img.shields.io/nuget/v/BitmapToVector.SkiaSharp?label=BitmapToVector.SkiaSharp)](https://www.nuget.org/packages/BitmapToVector.SkiaSharp)

A .NET Standard port of [Potrace](http://potrace.sourceforge.net/), which turns a black-and-white bitmap into smooth vector outlines. It is the algorithm behind Inkscape's Trace Bitmap, running in managed C# with no native dependency.

The port stays as close to the C source as it can — same structure, same comments, same file names — so that future Potrace releases are straightforward to fold in. Only the public API is written in idiomatic C#.

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

A `PotraceBitmap` is one bit per pixel: black or white, nothing in between. Deciding *which* pixels should be black — thresholding, edge detection, alpha, whatever suits your image — is up to you. This library doesn't do it.

Every pixel operation comes in two forms. `SetBlack`, `SetWhite`, `SetColor`, `InverseColor`, and `IsBlack` bounds-check first, and quietly ignore coordinates that fall outside the bitmap. The `...Unsafe` versions skip the check, which is faster when you are already looping within known bounds — and will corrupt memory if you aren't.

### Tracing options

`PotraceParam` mirrors the C library's parameters:

| Property | Default | Effect |
| --- | --- | --- |
| `TurdSize` | 2 | Drops any path enclosing fewer pixels than this. Raise it to shed speckle. |
| `TurnPolicy` | `PotraceTurnpolicyMinority` | How ambiguous turns get resolved. See the `PotraceTurnPolicy*` constants. |
| `AlphaMax` | 1.0 | Corner threshold. 0 makes every junction a corner; 1.334 and up makes them all smooth. |
| `OptiCurve` | true | Join adjacent bezier segments where one curve will do. |
| `OptTolerance` | 0.2 | How much error that joining may introduce. |

Potrace's [technical documentation](http://potrace.sourceforge.net/potracelib.pdf) explains all of them in more depth, and applies directly here.

## BitmapToVector.SkiaSharp

```
dotnet add package BitmapToVector.SkiaSharp
dotnet add package SkiaSharp
```

Skips the curve types and gives you paths you can draw:

```cs
IEnumerable<SKPath> paths = PotraceSkiaSharp.Trace(new PotraceParam(), skBitmap);
```

There is also an overload taking a `PotraceBitmap`, for when you want to build the bitmap yourself.

Converting an `SKBitmap` counts a pixel as black when its **red** channel is under 128. If your image needs different logic, build a `PotraceBitmap` and use that overload instead.

## Demo

[`src/BitmapToVector.Demo`](src/BitmapToVector.Demo) is a Xamarin.Forms app that traces an image and draws the result.

## License

GPL-3.0-or-later, inherited from Potrace itself.

This is not a formality. The GPL is copyleft: if you distribute software that links this library, that software has to be licensed under the GPL as well, with source available. That rules it out for most closed-source and commercial work. If your project can't accept those terms, [Potrace offers commercial licensing](http://potrace.sourceforge.net/#license) — take it up with the Potrace authors, not with me.
