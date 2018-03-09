# Using Your Xamarin.Android Build

Once the build has finished, `bin/$(Configuration)/bin/xabuild` may be
used to build projects. This is a shell script on Linux and macOS,
and will execute `xabuild.exe` on Windows.


## Linux and macOS System-Wide Installation

Linux and macOS users may use the `make install` target to install
Xamarin.Android into `/usr/local`. This may be overridden by setting
the `$(prefix)` make variable:

	make install prefix=/opt/local

In order for `xbuild` or `msbuild` within `$PATH` to resolve the
Xamarin.Android assemblies, you must install Xamarin.Android into the
same prefix as mono.


## Windows System-Wide Installation

Windows users may use `bin\$(Configuration)\bin\setup-windows.exe` to
install the current build tree into `%ProgramFiles%`, allowing
`msbuild.exe` to use the current build tree.

If you have Visual Studio 2017 installed, `setup-window.exe` *must* be run
within an Administrator-elevated **Developer Command Prompt for VS 2017**
window:

 1. In the Start menu, search for **Developer Command Prompt for VS 2017**.
 2. Right-click the **Developer Command Prompt for VS 2017** entry.
 3. Click **Run as administrator**.

Within the elevated command prompt, execute the `setup-windows.exe` program:

	> bin\Debug\bin\setup-windows.exe
	Executing: MKLINK /D "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\ReferenceAssemblies\Microsoft\Framework\MonoAndroid" "C:\xa-sdk\oss-xamarin.android_v7.4.99.57_Darwin-x86_64_master_97f08f7\bin\Debug\lib\xamarin.android\xbuild-frameworks\MonoAndroid"
	Executing: MKLINK /D "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\MonoAndroid" "C:\xa-sdk\oss-xamarin.android_v7.4.99.57_Darwin-x86_64_master_97f08f7\bin\Debug\lib\xamarin.android\xbuild-frameworks\MonoAndroid"
	Executing: MKLINK /D "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\Xamarin\Android" "C:\xa-sdk\oss-xamarin.android_v7.4.99.57_Darwin-x86_64_master_97f08f7\bin\Debug\lib\xamarin.android\xbuild\Xamarin\Android"
	Executing: MKLINK "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\Xamarin\Xamarin.Android.Sdk.props" "C:\xa-sdk\oss-xamarin.android_v7.4.99.57_Darwin-x86_64_master_97f08f7\bin\Debug\lib\xamarin.android\xbuild\Xamarin\Xamarin.Android.Sdk.props"
	Executing: MKLINK "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\Xamarin\Xamarin.Android.Sdk.targets" "C:\xa-sdk\oss-xamarin.android_v7.4.99.57_Darwin-x86_64_master_97f08f7\bin\Debug\lib\xamarin.android\xbuild\Xamarin\Xamarin.Android.Sdk.targets"
	Success!

To uninstall, run `setup-windows.exe /uninstall`:

	> bin\Debug\bin\setup-windows.exe /uninstall

The `setup-windows.exe` utility checks for an existing Xamarin.Android install,
renames the existing directories for backup/easy restoration purposes, then
create symbolic links into the extracted Xamarin.Android directory.

(Unfortunately, this means that you can't easily have side-by-side installs
of the Xamarin.Android SDK. Only one install can be active at a time.)

# Samples

The [HelloWorld](samples/HelloWorld) sample may be built with the
[xabuild](tools/scripts/xabuild) script:

    bin/Debug/xabuild /t:SignAndroidPackage samples/HelloWorld/HelloWorld.csproj

`xabuild /t:SignAndroidPackage` will generate an `.apk` file, which may be
installed onto an Android device with the [`adb install`][adb-commands]
command:

[adb-commands]: http://developer.android.com/tools/help/adb.html#commandsummary

    adb install samples/HelloWorld/bin/Debug/com.xamarin.android.helloworld-Signed.apk

**HelloWorld** may be launched manually through the Android app launcher,
or via `adb shell am`:

    adb shell am start com.xamarin.android.helloworld/example.MainActivity
