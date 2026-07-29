PHOTOBOOTH: FIRST BOOTH TEST
============================

THIS FOLDER IS PORTABLE

The PhotoBooth folder contains a self-contained Windows x64 build.
The booth computer does not need the .NET SDK or Desktop Runtime.

BEFORE LEAVING HOME

1. Copy the entire PhotoBooth-USB folder to the USB drive.
2. Keep the folder structure unchanged.
3. Add real template pairs to:
   PhotoBooth\Templates\<event name>\

   Every template needs a matching JSON and PNG file, for example:
   2.json
   2.png

4. Optional: add JPG or PNG files to PhotoBooth\DemoPhotos.
5. Safely eject the USB drive.

AT THE BOOTH

1. Do not uninstall or overwrite the old booth software.
2. Copy PhotoBooth-USB from the USB drive to C:\PhotoBooth-USB.
3. Open C:\PhotoBooth-USB.
4. Double-click START_PHOTOBOOTH.bat.
5. Operator settings PIN: 2016.
6. F11 toggles fullscreen. F10 toggles the mouse cursor.

IMPORTANT FOR THE FIRST TEST

- DemoMode is enabled.
- Camera capture is currently simulated.
- Printer output is currently simulated.
- Session photos and collages are saved under:
  C:\PhotoBooth-USB\PhotoBooth\Output
- Leave automatic shutdown disabled during the first hardware visit.
- Test screen calibration only after confirming touch input works.
- Restart the computer only after all other checks are complete.

COMPATIBILITY

This build requires 64-bit Windows 10 version 1607 or newer.
If the app does not open, run winver and photograph the Windows version.
