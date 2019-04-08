# Native Debugging of Mono using Android Studio

With these steps it is possible to start a XA app with managed debugging in Visual Studio (on Mac and Windows) and native debugging of mono (with debugging symbols) using Android Studio.  

Steps:
1. Get the same github mono version that is in Android SDK
2. Generate debug version of mono for android x86:
    make -C sdks/builds build-android-x86 -j8 CONFIGURATION=debug
3. Start x86 emulator
4. Get root access on emulator:
    adb root
5. Find mono lib on emulator:
    adb shell
    find / -name libmonosgen-32bit-2.0.so
6. Replace mono lib from path found using find command with mono debug lib :
    adb push sdks/builds/android-x86-debug/mono/mini/.libs/libmonosgen-2.0.so /data/app/Mono.Android.DebugRuntime-oZUTbxntb9m9aK1M2CdL3g==/lib/x86/libmonosgen-32bit-2.0.so
7. Run the apk from VS on emulator
8. Open the apk that you want to debug from Android Studio
9. Attach LLDB from Android Studio on the emulator


