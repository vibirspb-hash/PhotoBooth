# PhotoBooth

PhotoBooth OS is a Windows photo booth application built with C#/.NET and WPF.

The first milestone is a clean foundation:

- fullscreen-ready WPF UI;
- template folders loaded from `Templates`;
- configuration from `config.json`;
- demo mode for development without camera and printer hardware.

## Demo flow

Each matching pair such as `1.json` + `1.png` is shown as a separate selectable frame.
In demo mode the app:

1. creates a named event session or continues the previous session after a restart;
2. takes the required number of images from `DemoPhotos`;
3. creates numbered placeholders when `DemoPhotos` is empty;
4. shows a camera-style framing screen with the countdown over the live/demo preview;
5. keeps the selected frame visible beside the camera preview;
6. saves originals under `Output/<session>/Photos/<capture>`;
7. crops and places every shot according to the template JSON;
8. saves finished collages under `Output/<session>/Prints`;
9. shows the collage and lets the guest choose one, two, or three print copies.

## Development Notes

WPF runs on Windows. This repository can be edited on macOS, but the app should be built and tested on Windows with Visual Studio 2022 or the .NET SDK.
