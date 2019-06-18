# Development tips and native debugging

Tips and tricks while developing Xamarin.Android.

## Update directory

When a Xamarin.Android app launches on an Android device, and the app was
built in the `Debug` configuration, it will create an "update" directory
during process startup, printing the created directory to `adb logcat`:

     W/monodroid( 2796): Creating public update directory: `/data/data/Mono.Android_Tests/files/.__override__`

When the app needs to resolve native libraries and assemblies, it will look
for those files within the update directory *first*. This includes the Mono
runtime library and BCL assemblies.

Note that the update directory is *per-app*. The above mentioned `Mono.Android_Tests`
directory is created when running the
[`Mono.Android-Tests.csproj`](../../src/Mono.Android/Test/Mono.Android-Tests.csproj)
unit tests.

The update directory is not used in `Release` configuration builds.
(Note: `Release` configuration for the *app itself*, not for xamarin-android.)

Keep in mind that only the app that owns the update directory has permission to
write to it, so on a normal non-rooted device or emulator, you'll need to use
`adb shell run-as` to add new files.  For example, if you're working on a
mono/x86 bug and need to quickly update the app on the device to test
`libmonosgen-2.0.so` changes:

    $ make -C src/mono-runtimes/obj/Debug/x86 && \
      adb push src/mono-runtimes/obj/Debug/x86/mono/mini/.libs/libmonosgen-2.0.so \
        /data/local/tmp/ && \
      adb shell run-as Mono.Android_Tests cp /data/local/tmp/libmonosgen-2.0.so \
        /data/data/Mono.Android_Tests/files/.__override__/

Alternatively, if you're working on an `mscorlib.dll` bug:

    $ make -C external/mono/mcs/class/corlib PROFILE=monodroid && \
      adb push external/mono/mcs/class/lib/monodroid/mscorlib.dll \
        /data/local/tmp/ && \
      adb shell run-as Mono.Android_Tests cp /data/local/tmp/mscorlib.dll \
        /data/data/Mono.Android_Tests/files/.__override__/

On some devices, the `run-as` command might not have permission to read the
files in `/data/local/tmp/`.  In that case, you can use a `cat` command that
*pipes to* the `run-as` command:

    $ make -C external/mono/mcs/class/corlib PROFILE=monodroid && \
      adb push external/mono/mcs/class/lib/monodroid/mscorlib.dll \
        /data/local/tmp/ && \
      adb shell "cat /data/local/tmp/mscorlib.dll | \
        run-as Mono.Android_Tests sh -c \
        'cat > /data/data/Mono.Android_Tests/files/.__override__/mscorlib.dll'"

## Attaching LLDB using Android Studio on Windows or macOS

Android Studio can attach LLDB to Xamarin.Android for native debugging.  The
integration includes the usual features like a graphical thread and stack frame
browser and the ability to set breakpoints using the source code editor.

 1. Install [Android Studio][android-studio].  If you already have an Android
    SDK installation for Xamarin.Android, you can click **Cancel** on the **Android
    Studio Setup Wizard** when you launch Android Studio.

 2. Open the signed debuggable APK for the application in Android Studio via
    **Profile or debug APK** on the start window or the **File > Profile or
    Debug APK** menu item.

    ![Profile or debug in the Android Studio start
      window](../images/android-studio-start-window.png)

 3. If you skipped the **Android Studio Setup Wizard**, navigate to **File >
    Project Structure > Modules > Mono.Android_Tests-Signed > Dependencies**,
    click **New > Android SDK** next to the **Module SDK**.

    ![New SDK in the Android Studio Project Structure Modules Dependencies
      window](../images/android-studio-modules-dependencies.png)

    Select the Android SDK folder you're using with Xamarin.Android, and then
    under **Build target**, pick the appropriate Android API to match the APK.

    ![Create New Android SDK window in Android
      Studio](../images/android-studio-create-new-android-sdk.png)

 4. Wait for the **Indexing** status message at the bottom of the Android Studio
    window to disappear.

 5. Start the app, for example by launching it with or without managed debugging
    from Visual Studio, or by tapping the app on the device.

 6. In Android Studio, select **Run > Attach Debugger to Android Process**, or
    click the corresponding toolbar icon.

    ![Attach Debugger to Android Process in Android Studio Run
      menu](../images/android-studio-attach-debugger.png)

 7. Set the **Debugger** to **Native**, select the running app, and click
    **OK**.

    If the `adb` connection is slow, the first connection to the app will take a
    while to download all the system libraries.  The connection might time out
    if this takes too long, but the next connection attempt will have fewer
    libraries left to download and will likely succeed.

 8. You can set LLDB to continue through the various native signals that Mono
    uses for its normal internal operations by opening **View > Tool Windows >
    Debug**, selecting the **Android Native Debugger** tab, navigating to the
    inner **Debugger \[tab\] > LLDB \[tab\]** command prompt, and running the
    following `process handle` command:

        (lldb) process handle -p true -n true -s false SIGXCPU SIG33 SIG35 SIGPWR SIGTTIN SIGTTOU SIGSYS

    ![LLDB process handle command in Android Studio LLDB command
      prompt](../images/android-studio-lldb-no-stop-signals.png)

[android-studio]: https://developer.android.com/studio/

## Adding debug symbols for `libmonosgen-2.0.so`

First, you'll need to get a version of `libmonosgen-2.0.so` that includes debug
symbols.  You can either use a custom local build or download the debug version
of `libmonosgen-2.0.so` for a published Xamarin.Android version:

 1. Go to <https://github.com/xamarin/xamarin-android/tags> and click on the
    Xamarin.Android version you are debugging.

 2. Find the **OSS core** section at the bottom of the release information and
    click the link to the open-source build.

 3. Navigate to **Azure Artifacts** in the left sidebar and download the
    `xamarin-android/xamarin-android/bin/Release/bundle*.zip` file.

 4. Extract the `libmonosgen-2.0.d.so` files from the bundle.  For example, run:

        $ unzip bundle*.zip '**libmonosgen-2.0.d.so'

    (On Windows, the Git Bash command prompt includes the `unzip` command, so
    that's one way to complete this step.)

Next, there are a few options to get LLDB to see the debug symbols.

### Option A: Add the library with symbols as an `@(AndroidNativeLibrary)`

This option is convenient because it doesn't require any `adb` commands.  On the
other hand, it requires rebuilding and redeploying the app to test each version
of `libmonosgen-2.0.so`, so it's not ideal if you need to test several different
versions of `libmonosgen-2.0.so`.

 1. Ensure that **Android Options > Use Shared Runtime** is enabled.

 2. Add the appropriate architecture of `libmonosgen-2.0.d.so` to the
    corresponding `lib` subdirectory of the app project as described in the
    [Using Native Libraries][using-native-libraries] documentation.  For
    example, if debugging an arm64-v8a app, add the arm64-v8a version of
    `libmonosgen-2.0.d.so` to the project in `lib/arm64-v8a/.

 3. Rename the file to `libmonosgen-2.0.so`.

    ![libmonosgen-2.0.so added to the lib/arm64-v8a directory of the
      Xamarin.Android app project in the Visual Studio Solution
      Explorer](../images/lib-arm64-v8a-libmonosgen.png)

 4. Set the **Build Action** of the file to **AndroidNativeLibrary**.

    ![Build Action for libmonosgen-2.0.so set to AndroidNativeLibrary in the
      Visual Studio Properties
      window](../images/build-action-android-native-library.png)

 5. Build, deploy, and run the app.  Then attach LLDB.

 6. If desired, follow the `image lookup` and `settings set --
    target.source-map` steps from the [Debugging Mono binaries with LLDB
    guide][lldb-source-map] to allow stepping through the Mono runtime source
    files.

[using-native-libraries]: https://docs.microsoft.com/xamarin/android/platform/native-libraries
[lldb-source-map]: https://www.mono-project.com/docs/debug+profile/debug/lldb-source-map/

### Option B: Upload the library with symbols to the update directory

This option is useful for testing a number of different `libmonosgen-2.0.so`
versions quickly without rebuilding or redeploying the app, but it requires a
little care to complete the `adb` steps correctly on the command line.

 1. Push the appropriate architecture of `libmonosgen-2.0.d.so` into the
    application's update directory and rename it to `libmonosgen-2.0.so`:

        $ adb push libmonosgen-2.0.d.so \
            /data/local/tmp/libmonosgen-2.0.so && \
          adb shell run-as Mono.Android_Tests cp /data/local/tmp/libmonosgen-2.0.so \
            /data/data/Mono.Android_Tests/files/.__override__/

 2. Ensure all users have execute permissions on the application's data
    directory:

        $ adb shell run-as Mono.Android_Tests \
            chmod a+x /data/data/Mono.Android_Tests/

    This will allow LLDB to re-download `libmonosgen-2.0.so` and load the
    symbols from it.

 3. Run the app and attach LLDB.

### Option C: Manually load the library with symbols

This option allows testing an existing debuggable APK without pushing anything
new to the device.  The other options are usually more convenient, but loading
the symbols by hand might be useful in some cases.

 1. After attaching LLDB to the app, add the appropriate architecture of
    `libmonosgen-2.0.d.so` into LLDB with a command like:

        (lldb) image add ~/Downloads/lib/xamarin.android/xbuild/Xamarin/Android/lib/arm64-v8a/libmonosgen-2.0.d.so

 2. Find the current in-memory address of the `.text` section of
    `libmonosgen-2.0`.  For example, for a 64-bit app that's using the shared
    runtime, run the following command:

        (lldb) image dump sections libmonosgen-64bit-2.0.so

    Look for the row of the table that shows "code" as the "Type":

        SectID     Type             Load Address                             Perm File Off.  File Size  Flags      Section Name
        ---------- ---------------- ---------------------------------------  ---- ---------- ---------- ---------- ----------------------------
        0x0000000a code             [0x00000071106c4e80-0x0000007110932674)  r-x  0x0002ee80 0x0026d7f4 0x00000006 libmonosgen-64bit-2.0.so..text

 3. Load the `.text` section from `libmonosgen-2.0.d.so` at the in-memory
    starting memory address of the `.text` section:

        (lldb) image load -f libmonosgen-2.0.d.so .text 0x00000071106c4e80

## Attaching LLDB or GDB without Android Studio

### Attaching LLDB using mono/lldb-binaries on macOS

Download the precompiled `lldb` and `lldb-server` binaries from
<https://github.com/mono/lldb-binaries/releases>, and follow the instructions
within [README.md][lldb-readme].

[lldb-readme]: https://github.com/mono/lldb-binaries/blob/master/README.md

### Attaching GDB using Visual Studio on Windows

The GDB integration in Visual Studio is quite similar to the LLDB integration in
Android Studio.  It might be a more convenient option for users who already have
the Android NDK installed and who don't currently have Android Studio installed.

 1. In the Visual Studio Installer, under the **Individual components** tab,
    ensure that **Development activities > C++ Android development tools** is
    installed.

 2. Install the Android NDK if you don't already have it.  For example, use
    **Tools > Android > Android SDK Manager** to install it.

 3. Set **Tools > Options > Cross Platform > C++ > Android > Android NDK** to
    the Android NDK path.  For example:

        C:\Program Files (x86)\Android\android-sdk\ndk-bundle

 4. Quit and relaunch Visual Studio.

 5. Use **File > Open > Project/Solution** to open the signed debuggable APK for
    the application.

 6. Set the **Build > Configuration Manager > Active solution platform** to the
    application ABI.  If debugging an arm64-v8a application, explicitly add a
    platform named `ARM64` and set it as the active platform.

 7. If you need symbols for `libmonosgen-2.0`, copy the library file with
    symbols to a convenient local location, make sure the file name matches the
    name on device (for example, `libmonosgen-64bit-2.0.so` if using the 64-bit
    shared runtime), and add the local location of the library to **Project >
    Properties > Additional Symbol Search Paths**.

 8. Start the app, for example by launching it with or without managed debugging
    from Visual Studio, or by tapping the app on the device.

 9. Select **Debug > Attach to Android process** and wait for the connection to
    complete.

10. If needed, you can use **Debug > Windows > Immediate** to interact with the
    GDB command line.  Prefix GDB commands with `-exec` to get the expected
    behavior.  For example to view the stack backtrace:

        -exec backtrace

11. You can set GDB to continue through the various native signals that Mono
    uses for its normal internal operations by running the following command in
    the **Immediate** window:

        -exec handle SIGXCPU SIG33 SIG35 SIGPWR SIGTTIN SIGTTOU SIGSYS nostop noprint
