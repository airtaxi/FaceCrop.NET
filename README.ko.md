# FaceCrop.NET

[![NuGet](https://img.shields.io/nuget/v/FaceCrop.NET.svg)](https://www.nuget.org/packages/FaceCrop.NET)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/airtaxi/FaceCrop.NET/blob/main/LICENSE.txt)

🌐 [English](https://github.com/airtaxi/FaceCrop.NET/blob/main/README.md) | 한국어

FaceCrop.NET은 이미지 속 얼굴이 모두 들어가도록 가능한 한 큰 정사각형으로 잘라 내는 .NET 10 라이브러리입니다.

얼굴 검출에는 Google의 BlazeFace short-range 모델을 사용합니다. 모델은 라이브러리에 내장되어 있으며 ONNX Runtime으로 실행됩니다. 이미지를 읽고 저장하는 작업은 Magick.NET이 담당하고, 결과는 WebP로 저장합니다.

저장소에는 폴더 안의 이미지를 한 번에 잘라 내고 CSV 보고서를 남기는 콘솔 앱도 함께 들어 있습니다.

## 주요 기능

- **얼굴을 지키는 가장 큰 정사각형:** 잘라 낼 정사각형의 한 변은 항상 사용할 수 있는 영역의 짧은 변과 같습니다. 그 안에서 모든 얼굴 상자와 특징점이 들어가면서 눈, 코, 입의 중심에 가장 가까운 위치를 고릅니다.
- **내장 얼굴 검출기:** BlazeFace short-range 모델이 어셈블리에 포함되어 있어 모델을 따로 내려받거나 경로를 지정할 필요가 없습니다. 전처리와 후처리는 MediaPipe 얼굴 검출기와 같은 방식을 따릅니다. 128×128 레터박스 입력을 만들고, SSD 앵커로 결과를 해석한 뒤, 가중 NMS로 겹친 검출을 합칩니다.
- **타일 교차 검증:** 전체 이미지에서 얼굴이 정확히 하나만 검출되지 않으면, 크기가 다른 두 종류의 겹치는 타일을 추가로 검사합니다. 서로 다른 검출 창 여러 곳에서 확인된 얼굴만 남기므로, 한 번만 나타난 오검출은 걸러집니다.
- **오른쪽 패널 제외:** 카드의 이름 띠처럼 오른쪽 가장자리에 붙은 단색 패널을 찾아 잘라 낼 영역에서 제외합니다. 제외할 너비를 직접 지정할 수도 있습니다.
- **낮은 신뢰도 얼굴 정리:** 모든 얼굴이 정사각형 하나에 들어가지 않으면, 신뢰도가 낮은 얼굴을 하나씩 빼면서 나머지 얼굴이 들어가는 배치를 찾습니다. 제외한 얼굴은 모두 결과에 기록됩니다.
- **투명도와 색 프로필 유지:** 투명한 영역은 그대로 두거나 지정한 색으로 채웁니다. EXIF 회전 정보를 적용하고, 원본의 ICC 프로필을 결과 파일에 그대로 넣습니다.
- **크로스 플랫폼:** Windows(x64, ARM64), Linux(x64, ARM64), macOS(ARM64)에서 실행됩니다.

## 기존 방식과 다른 점

가운데를 기준으로 자르면 세로로 긴 인물 사진이나 여러 사람이 함께 나온 사진에서 얼굴이 잘리기 쉽습니다. 반대로 일반적인 얼굴 크롭 도구는 얼굴 하나에 맞춰 바짝 잘라 내기 때문에 배경과 구도가 대부분 사라집니다.

FaceCrop.NET은 이미지를 최대한 많이 남기도록 항상 가장 큰 정사각형을 사용하고, 얼굴이 잘리지 않도록 정사각형의 위치만 옮깁니다. 얼굴이 없거나 모든 얼굴이 함께 들어가지 않는 경우에도 이미지를 어떻게 처리했는지 결과 상태로 알려 줍니다.

## 자를 영역을 정하는 방식

1. **오른쪽 패널:** 이미지 세로 방향의 가운데 70% 구간을 표본으로 검사합니다. 오른쪽 가장자리가 하나의 단색이고, 그 색이 너비의 65%에서 97% 사이의 일정한 경계부터 대부분의 행을 덮고 있으면, 경계 오른쪽을 모두 제외합니다. 패널 위에 인쇄된 글자는 허용합니다.
2. **얼굴 검출:** 투명한 픽셀을 흰색 위에 합성한 뒤, 각 검출 창을 `DetectSize` 이내로 축소해 모델에 전달합니다. 전체 이미지에서 얼굴이 정확히 하나만 나오지 않으면, 짧은 변의 100%와 60% 크기로 절반씩 겹치는 정사각형 타일도 검사합니다. 같은 얼굴에 대한 검출은 하나로 합치고, 서로 다른 검출 창 `MinFaceVotes`곳 이상에서 확인된 얼굴만 남깁니다.
3. **정사각형 배치:** 정사각형의 한 변은 사용할 수 있는 너비와 높이 중 짧은 쪽과 같습니다. 모든 얼굴 상자와 특징점 6개가 들어가는 위치 가운데 눈, 코, 입이 가장 가운데에 오는 위치를 고릅니다.
4. **얼굴이 다 들어가지 않을 때:** 오른쪽 패널까지 걸친 얼굴은 패널 경계에서 잘라 내고, 그래도 들어가지 않으면 눈, 코, 입을 중심으로 자릅니다. 그 밖의 경우에는 낮은 신뢰도 얼굴 정리를 진행합니다. 이때 점수가 0.90 이상인 얼굴을 기준으로 삼고, 점수가 0.85 미만이면서 기준보다 0.10 이상 낮은 얼굴만 제외 대상으로 봅니다. 기준 얼굴이 이미지 위쪽에 있으면 그보다 한참 아래에 있는 얼굴을 먼저 제외하고, 그다음에는 점수가 낮은 순서로 제외합니다.

## 시작하기

### 요구 사항

- .NET 10 SDK

### 라이브러리

NuGet에서 패키지를 설치합니다.

```bash
dotnet add package FaceCrop.NET
```

파일을 잘라 WebP로 저장하는 예시입니다.

```csharp
using FaceCrop;

using var cropper = new FaceCropper();
var result = cropper.CropFile("card.png", "card_crop.webp");
Console.WriteLine($"{result.Status.Code} {result.CropBox}");
```

메모리에서 처리하면서 옵션을 지정하는 예시입니다.

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

다른 경로로 얻은 픽셀은 `new RgbaImage(width, height, rgbaBytes)`로 감싸서 사용할 수 있습니다.

얼굴 검출기만 따로 사용하는 예시입니다.

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

`FaceCropper`와 `BlazeFaceDetector`는 모델을 한 번만 불러오며, 여러 스레드에서 함께 사용할 수 있습니다. 처리 중에 발생한 실패는 예외로 던지지 않고 `error` 상태로 반환합니다.

### 콘솔 앱

```bash
dotnet run --project samples/FaceCrop.NET.Sample -c Release -- <입력 폴더> [옵션]
```

예를 들어 다음 명령은 하위 폴더를 포함해 `cards` 아래의 이미지를 모두 잘라 내고, 투명한 영역을 흰색으로 채웁니다.

```bash
dotnet run --project samples/FaceCrop.NET.Sample -c Release -- ./cards --recursive --background "#ffffff"
```

결과는 `cards/cropped` 폴더에 `<이름>_crop.webp`로 저장되고, 같은 폴더에 `crop_report.csv`도 함께 만들어집니다. 이미지마다 한 줄씩 처리 결과를 출력하고, 마지막에 요약을 출력합니다. 종료 코드는 성공하면 0, 실패한 이미지가 있으면 1, 인수가 잘못되었으면 2입니다.

## 옵션

| 콘솔 옵션 | `FaceCropOptions` 속성 | 기본값 | 설명 |
| --- | --- | --- | --- |
| `input` | | 필수 | 잘라 낼 이미지가 있는 폴더입니다. |
| `--output` | | `INPUT/cropped` | 잘라 낸 이미지와 `crop_report.csv`를 저장할 폴더입니다. 입력 폴더와 달라야 합니다. |
| `--recursive` | | 끔 | 하위 폴더의 이미지도 처리합니다. 결과 폴더에도 같은 폴더 구조를 유지합니다. |
| `--overwrite` | | 끔 | 기존 결과를 덮어씁니다. 지정하지 않으면 이미 결과가 있는 이미지는 `exists`로 기록합니다. |
| `--no-face` | `NoFace` | `skip` | `skip`이면 아무것도 저장하지 않고, `center`이면 가운데 정사각형으로 자릅니다. |
| `--faces` | `FaceSelection` | `all` | `all`이면 모든 얼굴을 포함하고, `largest`이면 상자가 가장 큰 얼굴만 포함합니다. |
| `--right-strip` | `RightStrip` | `auto` | `auto`이면 오른쪽 가장자리의 단색 패널을 찾아 제외하고, `off`이면 전체 너비를 사용합니다. |
| `--right-strip-px` | `RightStripPixels` | 없음 | 오른쪽 가장자리에서 지정한 픽셀만큼 제외합니다. `--right-strip`보다 우선합니다. |
| `--confidence` | `MinDetectionConfidence` | `0.5` | 최소 검출 점수입니다. 0보다 크고 1 이하여야 합니다. |
| `--detect-size` | `DetectSize` | `1600` | 각 검출 창을 한 변이 이 크기 이내가 되도록 축소합니다. 128 이상이어야 합니다. |
| `--tile-detection` | `TileDetection` | `fallback` | `fallback`이면 전체 이미지에서 얼굴이 정확히 하나만 나오지 않을 때 타일을 검사합니다. `always`이면 항상 검사하고, `off`이면 검사하지 않습니다. |
| `--min-face-votes` | `MinFaceVotes` | `2` | 타일을 검사할 때 얼굴을 확인해야 하는 서로 다른 검출 창의 최소 개수입니다. |
| `--webp-quality` | `Webp.Quality` | `85` | 손실 압축 WebP의 품질이며, 0부터 100까지 지정합니다. |
| `--webp-lossless` | `Webp.Lossless` | 끔 | 무손실 WebP로 저장합니다. 픽셀이 그대로 보존되며 `--webp-quality`는 무시합니다. |
| `--background` | `Background` | 없음 | 투명한 영역을 채울 `#RRGGBB` 색입니다. 지정하지 않으면 투명도를 유지합니다. |
| `--model` | `ModelPath` | 내장 모델 | 다른 BlazeFace short-range ONNX 모델의 경로입니다. |

## 처리 결과

| 상태 | 의미 |
| --- | --- |
| `face_crop` | 선택한 얼굴이 모두 들어가도록 잘랐습니다. |
| `face_crop_strip_clipped` | 얼굴이 오른쪽 패널까지 걸쳐 있어 얼굴 영역을 패널 경계에서 잘라 냈습니다. 그래도 들어가지 않으면 눈, 코, 입을 중심으로 잘랐습니다. |
| `face_crop_low_confidence_filtered` | 모든 얼굴이 함께 들어가지 않아 신뢰도가 낮은 얼굴을 제외했습니다. 제외 내역은 `detail`에 JSON으로 기록됩니다. |
| `center_fallback` | 얼굴을 찾지 못해 가운데 정사각형으로 잘랐습니다. |
| `no_face` | 얼굴을 찾지 못해 아무것도 저장하지 않았습니다. |
| `exists` | 이미 결과가 있어 건너뛰었습니다. 콘솔 앱에서만 사용합니다. |
| `error` | 처리에 실패했습니다. 실패 사유는 `detail`에 기록됩니다. |

## 보고서 열

| 열 | 내용 |
| --- | --- |
| `input`, `output` | 원본 경로와 결과 경로입니다. |
| `status` | 위 표의 처리 결과 중 하나입니다. |
| `detected_faces` | 확인된 얼굴 수입니다. |
| `crop_box` | 원본 픽셀 기준의 `(left, top, right, bottom)`입니다. 오른쪽과 아래쪽 경계는 포함하지 않습니다. |
| `detail` | 검출 방식과 자르기 메모, 얼굴 제외 내역, 또는 오류 메시지입니다. |
| `excluded_right_px` | 오른쪽 가장자리에서 제외한 너비입니다. |
| `detections_json` | 얼굴마다 원본 픽셀 기준의 상자, 점수, 확인한 검출 창 수, 특징점을 기록합니다. |

## 참고 사항

- 애니메이션 WebP나 여러 페이지로 된 TIFF처럼 프레임이 둘 이상인 이미지는 오류로 처리합니다.
- 콘솔 앱은 `.jpg`, `.jpeg`, `.png`, `.webp`, `.bmp`, `.tif`, `.tiff` 파일을 처리합니다. 출력 폴더 안의 파일과 이름이 `_crop`으로 끝나는 파일은 건너뜁니다.
- 결과 파일에는 ICC 프로필만 복사됩니다. EXIF, XMP 같은 다른 메타데이터는 저장하지 않습니다.
- 오른쪽 패널 검출은 하나의 단색을 찾는 방식이며 글자를 인식하지 않습니다. 다른 형태의 레이아웃에서는 `--right-strip-px` 또는 `RightStripPixels`로 너비를 지정하시기 바랍니다.
- BlazeFace short-range는 화면에서 어느 정도 크게 보이는 얼굴을 대상으로 설계된 모델입니다. 멀리서 찍은 사진의 작은 얼굴은 검출하지 못할 수 있습니다.

## 라이선스

FaceCrop.NET은 [MIT 라이선스](https://github.com/airtaxi/FaceCrop.NET/blob/main/LICENSE.txt)로 배포됩니다. 내장된 BlazeFace 모델은 Apache License 2.0을 따르며, 자세한 내용은 [THIRD-PARTY-NOTICES.md](https://github.com/airtaxi/FaceCrop.NET/blob/main/THIRD-PARTY-NOTICES.md)에서 확인할 수 있습니다.
