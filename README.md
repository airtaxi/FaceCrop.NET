# FaceCrop.NET

[![NuGet](https://img.shields.io/nuget/v/FaceCrop.NET.svg)](https://www.nuget.org/packages/FaceCrop.NET)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/airtaxi/FaceCrop.NET/blob/main/LICENSE.txt)

🌐 English | [한국어](https://github.com/airtaxi/FaceCrop.NET/blob/main/README.ko.md)

FaceCrop.NET is a .NET 10 library that crops images to the largest possible square while keeping every face inside.

Faces are found with Google's BlazeFace short-range model, which is embedded in the library and runs on ONNX Runtime. Images are read and written with Magick.NET, and results are saved as WebP.

The repository also includes a console app that crops a whole folder in one run and writes a CSV report.

## Highlights

- **Largest square, faces kept inside.** The side of the crop always equals the shorter side of the usable area. Within that, the crop is placed so that every face box and keypoint stays inside, as close as possible to the center of the eyes, nose, and mouth.
- **Embedded face detector.** The BlazeFace short-range model is built into the assembly, so there is no model to download or locate. Pre- and post-processing follow MediaPipe's face detector: a letterboxed 128×128 input, SSD anchor decoding, and weighted non-maximum suppression.
- **Tile verification.** When the full image does not yield exactly one face, overlapping tiles at two scales are scanned as well. A face is kept only when several distinct windows find it, which filters out one-off false detections.
- **Right panel exclusion.** A flat-color panel attached to the right edge, such as the name strip of a card, is detected and kept out of the crop. The excluded width can also be set explicitly.
- **Low-confidence recovery.** If the faces cannot all fit in one square, lower-confidence faces are dropped one at a time until the rest fit. Every removed face is recorded in the result.
- **Transparency and color profiles.** Transparent areas are kept or filled with a chosen color. EXIF orientation is applied, and the source ICC profile is carried over to the output.
- **Cross-platform.** Runs on Windows (x64, ARM64), Linux (x64, ARM64), and macOS (ARM64).

## Why FaceCrop.NET?

Center cropping often cuts off faces in tall portraits and group shots. Typical face-crop tools go the other way and crop tightly around a single face, discarding most of the background and composition.

FaceCrop.NET keeps as much of the image as possible by always using the largest square, and only moves that square so that no face is cut off. When there is no face, or the faces cannot fit together, the result status says exactly how the image was handled.

## How the crop is chosen

1. **Right panel.** The middle 70% of the rows are sampled. If the right edge has one flat color and that color covers most rows from a consistent boundary between 65% and 97% of the width, everything right of the boundary is excluded. Lettering printed over the panel is tolerated.
2. **Face detection.** Transparent pixels are composited over white, and each detection window is shrunk to fit within `DetectSize` before it is passed to the model. If the full image does not yield exactly one face, square tiles with sides of 100% and 60% of the shorter side, overlapping by half, are scanned too. Hits of the same face are merged, and a face is kept only when at least `MinFaceVotes` distinct windows found it.
3. **Square placement.** The side of the square equals the shorter of the usable width and the height. Among the positions that keep every face box and all six keypoints inside, the one that best centers the eyes, nose, and mouth is chosen.
4. **When the faces do not fit.** Faces that reach into the right panel are clipped at its boundary; if they still do not fit, the crop is centered on the eyes, nose, and mouth. Otherwise, low-confidence recovery keeps a face scoring at least 0.90 as the anchor and removes faces scoring below 0.85 and at least 0.10 below the anchor. Faces sitting well below an anchor in the upper part of the image are removed first, then the lowest scores.

## Getting started

### Requirements

- .NET 10 SDK

### Library

Install the package from NuGet:

```bash
dotnet add package FaceCrop.NET
```

Crop a file and save the result as WebP:

```csharp
using FaceCrop;

using var cropper = new FaceCropper();
var result = cropper.CropFile("card.png", "card_crop.webp");
Console.WriteLine($"{result.Status.Code} {result.CropBox}");
```

Work in memory and choose the options:

```csharp
using FaceCrop;
using FaceCrop.Imaging;

var options = new FaceCropOptions
{
    FaceSelection = FaceSelectionMode.Largest,
    NoFace = NoFaceMode.Center,
    Background = RgbColor.White,
};
using var cropper = new FaceCropper(options);

var source = ImageFile.Load("card.png");
var result = cropper.Analyze(source.Image);
if (result.CropBox is { } cropBox)
{
    var cropped = cropper.Crop(source.Image, cropBox);
    ImageFile.SaveWebp(cropped, "card_crop.webp", new WebpEncodeOptions { Quality = 90 }, source.IccProfile);
}
```

Pixels from other sources can be wrapped with `new RgbaImage(width, height, rgbaBytes)`.

Use the face detector on its own:

```csharp
using FaceCrop.Detection;
using FaceCrop.Imaging;

using var detector = new BlazeFaceDetector();
var image = ImageFile.Load("photo.jpg").Image;
foreach (var face in detector.Detect(image))
{
    Console.WriteLine($"{face.Score:F2} {face.BoundingBox}");
}
```

`FaceCropper` and `BlazeFaceDetector` load the model once and can be shared across threads. Processing failures are returned as an `error` status instead of being thrown.

### Console app

```bash
dotnet run --project samples/FaceCrop.NET.Sample -c Release -- <input folder> [options]
```

For example, the following crops every image under `cards`, including subfolders, and fills transparent areas with white:

```bash
dotnet run --project samples/FaceCrop.NET.Sample -c Release -- ./cards --recursive --background "#ffffff"
```

Results are saved as `<name>_crop.webp` in `cards/cropped`, together with `crop_report.csv`. One line is printed per image, followed by a summary. The exit code is 0 on success, 1 if any image failed, and 2 for invalid arguments.

## Options

| Console option | `FaceCropOptions` property | Default | Description |
| --- | --- | --- | --- |
| `input` | | Required | Folder containing the images to crop. |
| `--output` | | `INPUT/cropped` | Folder for the cropped images and `crop_report.csv`. Must differ from the input folder. |
| `--recursive` | | Off | Includes images in subfolders. The output keeps the same folder structure. |
| `--overwrite` | | Off | Replaces existing results. Without it, images that already have a result are reported as `exists`. |
| `--no-face` | `NoFace` | `skip` | `skip` writes nothing. `center` crops the centered square. |
| `--faces` | `FaceSelection` | `all` | `all` keeps every face inside. `largest` keeps only the face with the largest box. |
| `--right-strip` | `RightStrip` | `auto` | `auto` detects and excludes a flat-color panel on the right edge. `off` uses the full width. |
| `--right-strip-px` | `RightStripPixels` | None | Excludes this many pixels from the right edge. Overrides `--right-strip`. |
| `--confidence` | `MinDetectionConfidence` | `0.5` | Minimum detection score. Greater than 0 and at most 1. |
| `--detect-size` | `DetectSize` | `1600` | Each detection window is shrunk to fit within this many pixels per side. At least 128. |
| `--tile-detection` | `TileDetection` | `fallback` | `fallback` scans tiles when the full image does not yield exactly one face. `always` always scans them, and `off` never does. |
| `--min-face-votes` | `MinFaceVotes` | `2` | Number of distinct windows that must find a face when tiles are scanned. |
| `--webp-quality` | `Webp.Quality` | `85` | Lossy WebP quality from 0 to 100. |
| `--webp-lossless` | `Webp.Lossless` | Off | Saves lossless WebP. Pixels are stored exactly, and `--webp-quality` is ignored. |
| `--background` | `Background` | None | `#RRGGBB` color that fills transparent areas. Transparency is kept by default. |
| `--model` | `ModelPath` | Embedded model | Path to another BlazeFace short-range ONNX model. |

## Result statuses

| Status | Meaning |
| --- | --- |
| `face_crop` | The crop contains every selected face. |
| `face_crop_strip_clipped` | The faces reach into the right panel, so the face region was clipped at the panel boundary. If it still did not fit, the crop was centered on the eyes, nose, and mouth. |
| `face_crop_low_confidence_filtered` | The faces did not fit together, so lower-confidence faces were left out. `detail` contains the removal report as JSON. |
| `center_fallback` | No face was found, and the centered square was used. |
| `no_face` | No face was found, and nothing was written. |
| `exists` | A result already exists, so the image was skipped. Console app only. |
| `error` | Processing failed. `detail` contains the reason. |

## Report columns

| Column | Content |
| --- | --- |
| `input`, `output` | Source and result paths. |
| `status` | One of the statuses above. |
| `detected_faces` | Number of confirmed faces. |
| `crop_box` | `(left, top, right, bottom)` in source pixels. The right and bottom edges are exclusive. |
| `detail` | Detection pass and crop notes, the removal report, or the error message. |
| `excluded_right_px` | Width excluded from the right edge. |
| `detections_json` | Box, scores, number of confirming windows, and keypoints of each face, in source pixels. |

## Notes

- Images with more than one frame, such as animated WebP or multi-page TIFF, are reported as errors.
- The console app processes `.jpg`, `.jpeg`, `.png`, `.webp`, `.bmp`, `.tif`, and `.tiff` files. Files inside the output folder and files whose names end with `_crop` are skipped.
- Only the ICC profile is copied to the output. EXIF, XMP, and other metadata are not written.
- The right panel detector looks for a single flat color and does not recognize text. For other layouts, set the width with `--right-strip-px` or `RightStripPixels`.
- BlazeFace short-range is designed for faces that take up a noticeable part of the image. Small faces in wide shots may be missed.

## License

FaceCrop.NET is licensed under the [MIT License](https://github.com/airtaxi/FaceCrop.NET/blob/main/LICENSE.txt). The embedded BlazeFace model is licensed under the Apache License 2.0. See [THIRD-PARTY-NOTICES.md](https://github.com/airtaxi/FaceCrop.NET/blob/main/THIRD-PARTY-NOTICES.md) for details.
