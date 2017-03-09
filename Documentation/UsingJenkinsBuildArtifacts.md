# Jenkins Build Artifacts

The [**Project xamarin-android** Jenkins page][xa-jenkins-page] has a link
to the [Latest Azure Artifacts][xa-macOS-azure-artifacts], which contains an
`oss-xamarin.android*.zip` file for every Jenkins build on macOS.
The `oss-xamarin.android*.zip` file can be downloaded and used, without
requiring a local `xamarin-android` repo build.

[xa-jenkins-page]: https://jenkins.mono-project.com/view/Xamarin.Android/job/xamarin-android/
[xa-macOS-azure-artifacts]: https://jenkins.mono-project.com/view/Xamarin.Android/job/xamarin-android/lastSuccessfulBuild/Azure/

The Jenkins build artifacts do *not* include Android SDK or NDK bits,
while requiring that parts of the Android SDK be installed to work.
Please see the [Android SDK Setup](#Android_SDK_Setup) section for instructions
on setting up the Android NDK & SDK if an Android SDK is not already setup.

# Installing Jenkins Build Artifacts

macOS and Windows users should go to the
[Latest xamarin-android Azure Artifacts][xa-macOS-azure-artifacts] page and
download the `oss-xamarin.android*.zip` file, e.g.
[oss-xamarin.android_v7.2.99.19_Darwin-x86_64_master_3b893cd.zip][oss-xa-macOS].

[oss-xa-macOS]: https://jenkins.mono-project.com/view/Xamarin.Android/job/xamarin-android/lastSuccessfulBuild/Azure/processDownloadRequest/xamarin-android/oss-xamarin.android_v7.2.99.19_Darwin-x86_64_master_3b893cd.zip

macOS users may just extract this into their **Downloads** folder using
**Archive Utility.app** or `unzip` from **Terminal.app**. This will result
in a file such as:

	$HOME/Downloads/oss-xamarin.android_v7.2.99.19_Darwin-x86_64_master_3b893cd/bin/Debug/bin/xabuild

Windows users should right-click this file within Explorer and click
**Extract All...**, and in the **Extract Compressed (Zipped) Folders** dialog
enter a *short* path such as `C:\xa-sdk`. This is necessary because some
of the contained filenames are quite long. This will result in a path
such as:

	C:\xa-sdk\oss-xamarin.android_v7.2.99.19_Darwin-x86_64_master_3b893cd\bin\Debug\bin\mono-symbolicate.cmd

Linux/x86_64 users can go to the
[Latest xamarin-android-linux Azure Artifacts page][xa-linux-azure-artifacts]
and download the `oss-xamarin.android*.zip` file, e.g.
[xamarin-android/oss-xamarin.android_v7.2.99.19_Linux-x86_64_master_3b893cd.zip][oss-xa-Linux].

[xa-linux-azure-artifacts]: https://jenkins.mono-project.com/view/Xamarin.Android/job/xamarin-android-linux/lastSuccessfulBuild/Azure/
[oss-xa-Linux]: https://jenkins.mono-project.com/view/Xamarin.Android/job/xamarin-android-linux/lastSuccessfulBuild/Azure/processDownloadRequest/xamarin-android/oss-xamarin.android_v7.2.99.19_Linux-x86_64_master_3b893cd.zip

# Using Jenkins Build Artifacts

## Command-line use: Linux and macOS

Within the `oss-xamarin.android*.zip` extracted contents is a
`oss-xamarin.android*/bin/Debug/bin/xabuild` script.

`xabuild` requires that the `$ANDROID_SDK_PATH` and `$ANDROID_NDK_PATH`
environment variables be set to the location of the Android SDK and
Android NDK directories, respectively.

If you have a xamarin-android build environment, then
`$HOME/android-toolchain` will contain an Android SDK and NDK:

	export ANDROID_SDK_PATH=$HOME/android-toolchain/sdk
	export ANDROID_NDK_PATH=$HOME/android-toolchain/ndk

If you followed the [Android SDK Setup](#Android_SDK_Setup) instructions, then:

	export ANDROID_SDK_PATH=$HOME/xa-sdk/android-sdk
	export ANDROID_NDK_PATH=$HOME/xa-sdk/android-ndk/android-ndk-r14

Once the `oss-xamarin.android*.zip` file has been installed, Unix users
may use the `oss-xamarin.android*/bin/Debug/bin/xabuild` script to build
projects.

	# macOS users: build the xamarin-android HelloWorld.csproj sample
	$HOME/Downloads/oss-xamarin.android_v7.2.99.19_Darwin-x86_64_master_3b893cd/bin/Debug/bin/xabuild \
		/t:SignAndroidPackage \
		samples/HelloWorld/HelloWorld.csproj


## Command-line use: Windows

There is not currently an `xabuild.cmd` script for Windows use. Instead,
Windows users should execute `msbuild.exe` directly, providing the following
MSBuild properties:

* `AndroidSdkDirectory`: The location of the Android SDK.
* `AndroidNdkDirectory`: The location of the Android NDK.
* `JavaSdkDirectory`:  The location of the Java SDK/JDK.
* `MonoAndroidBinDirectory`: The `xbuild\Xamarin\Android` directory in the
    `oss-xamarin.android*.zip` installation.
* `MonoAndroidToolsDirectory`: The `xbuild\Xamarin\Android` directory in the
    `oss-xamarin.android*.zip` installation.
* `TargetFrameworkRootPath`: The `xbuild-frameworks` directory in the
    `oss-xamarin.android*.zip` installation.

For example (using the paths from [Android SDK Setup](#Android_SDK_Setup)):

	msbuild /p:AndroidSdkDirectory="C:\xa-sdk\android-sdk" ^
		/p:AndroidNdkDirectory="C:\xa-sdk\android-ndk\android-ndk-r14" ^
		/p:MonoAndroidBinDirectory="C:\xa-sdk\oss-xamarin.android_v7.2.99.19_Darwin-x86_64_master_3b893cd\bin\Debug\lib\xbuild\Xamarin\Android" ^
		/p:MonoAndroidToolsDirectory="C:\xa-sdk\oss-xamarin.android_v7.2.99.19_Darwin-x86_64_master_3b893cd\bin\Debug\lib\xbuild\Xamarin\Android" ^
		/p:TargetFrameworkRootPath="C:\xa-sdk\oss-xamarin.android_v7.2.99.19_Darwin-x86_64_master_3b893cd\bin\Debug\lib\xbuild-frameworks" ^
		/t:SignAndroidPackage ^
		samples\HelloWorld\HelloWorld.csproj

<a name="Android_SDK_Setup" />
# Android SDK Setup

Please see the [Android Studio][android-studio] page to download the
Android SDK, if you do not already have the Android SDK installed.
If you don't want Android Studio, you can follow the
**Get just the command line tools** section at the Android Studio page.

[android-studio]: https://developer.android.com/studio/index.html

The Android NDK can be downloaded from the [NDK Downloads][ndk-downloads]
page.

[ndk-downloads]: https://developer.android.com/ndk/downloads/index.html

## macOS Android SDK Setup

Download the macOS SDK tools package, e.g.
[tools_r25.2.3-macosx.zip][android-tools-macOS], and the
    macOS NDK package, e.g. [android-ndk-r14-darwin-x86_64.zip][ndk-macOS]

[android-tools-macOS]: https://dl.google.com/android/repository/tools_r25.2.3-macosx.zip
[ndk-macOS]: https://dl.google.com/android/repository/android-ndk-r14-darwin-x86_64.zip

Within **Terminal.app**, run the following commands, assuming that the
above mentioned files were downloaded into `$HOME/Downloads`, and we want
to create a new Android SDK & NDK within `$HOME/xa-sdk`:

	mkdir -p $HOME/xa-sdk/android-sdk
	cd $HOME/xa-sdk/android-sdk
	unzip $HOME/Downloads/tools_r25.2.3-macosx.zip
	tools/bin/sdkmanager build-tools;25.0.2"
	tools/bin/sdkmanager "platform-tools"
	tools/bin/sdkmanager "platforms;android-25"
	
	mkdir -p $HOME/xa-sdk/android-ndk
	cd $HOME/xa-sdk/android-ndk
	unzip $HOME/Downloads/android-ndk-r14-darwin-x86_64.zip

## Windows Android SDK Setup

1. Download the Windows SDK tools package, e.g.
[tools_r25.2.3-windows.zip][android-tools-windows], and the
    Windows NDK package, e.g. [android-ndk-r14-windows-x86.zip][ndk-win64]

[android-tools-windows]: https://dl.google.com/android/repository/tools_r25.2.3-windows.zip
[ndk-win64]: https://dl.google.com/android/repository/android-ndk-r14-windows-x86.zip


2. Right-click the `tools*.zip` file within Explorer, then click
    **Extract All...**
3. Within the **Extract Compressed (Zipped) Folders** dialog, enter
    a path such as `C:\xa-sdk\android-sdk`. Click **Next**.
4. Right-click the `android-ndk*.zip` file within Explorer, then click
    **Extract All...**
5. Within the **Extract Compressed (Zipped) Folders** dialog, enter
    a path such as `C:\xa-sdk\android-ndk`. Click **Next**.
6. Within `cmd.exe`:
        cd C:\xa-sdk\android-sdk
        tools\bin\sdkmanager "build-tools;25.0.2"
        tools\bin\sdkmanager "platform-tools"
        tools\bin\sdkmanager "platforms;android-25"
