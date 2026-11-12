# Third-party notices

AirNet is distributed with the following third-party components. This file is a notice
and attribution list, not legal advice; the upstream license texts control.

## Runtime and UI

| Component | Copyright | License |
| --- | --- | --- |
| .NET runtime and libraries | .NET Foundation and contributors | MIT |
| Avalonia / Avalonia.Desktop / Avalonia.Themes.Fluent | AvaloniaUI OÜ and contributors | MIT |
| Avalonia.Fonts.Inter | AvaloniaUI OÜ; Inter Project Authors | MIT / SIL Open Font License 1.1 |
| CommunityToolkit.Mvvm | .NET Foundation and contributors | MIT |
| SkiaSharp / libSkiaSharp | Microsoft Corporation and Skia contributors | MIT |
| HarfBuzzSharp / libHarfBuzzSharp | Microsoft Corporation and HarfBuzz contributors | MIT |
| ANGLE (`av_libglesv2.dll` on Windows) | The ANGLE Project Authors | BSD 3-Clause |

## Links

- .NET: <https://github.com/dotnet/runtime/blob/main/LICENSE.TXT>
- Avalonia: <https://github.com/AvaloniaUI/Avalonia/blob/master/licence.md>
- CommunityToolkit.Mvvm: <https://github.com/CommunityToolkit/dotnet/blob/main/License.md>
- SkiaSharp: <https://github.com/mono/SkiaSharp/blob/main/LICENSE.md>
- HarfBuzzSharp: <https://github.com/mono/SkiaSharp/blob/main/LICENSE.md>
- ANGLE: <https://chromium.googlesource.com/angle/angle/+/refs/heads/main/LICENSE>
- Inter: <https://github.com/rsms/inter/blob/master/LICENSE.txt>

## Notes

- The app embeds the Inter font through `Avalonia.Fonts.Inter`; the font is distributed
  under the SIL Open Font License 1.1.
- `libSkiaSharp`, `libHarfBuzzSharp`, `libAvaloniaNative`, and ANGLE are shipped as
  native files next to the executable. Their upstream copyright and license notices
  apply unchanged.
- If you redistribute a modified build, retain this file and the upstream license texts.
