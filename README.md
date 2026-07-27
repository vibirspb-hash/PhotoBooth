# PhotoBooth

PhotoBooth OS is a Windows photo booth application built with C#/.NET and WPF.

The first milestone is a clean foundation:

- fullscreen-ready WPF UI;
- template folders loaded from `Templates`;
- configuration from `config.json`;
- demo mode for development without camera and printer hardware.

## Demo flow

The `VVM` template uses the original JSON format and PNG overlay. In demo mode the app:

1. takes the required number of images from `DemoPhotos`;
2. creates numbered placeholders when `DemoPhotos` is empty;
3. saves session originals under `Output/<timestamp>/Originals`;
4. crops and places every shot according to the template JSON;
5. saves `result.png` and shows it before the simulated print step.

## Development Notes

WPF runs on Windows. This repository can be edited on macOS, but the app should be built and tested on Windows with Visual Studio 2022 or the .NET SDK.
