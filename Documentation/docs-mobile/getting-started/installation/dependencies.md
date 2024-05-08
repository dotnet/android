---
title: "Install .NET for Android dependencies"
description: "Learn how to install .NET for Android dependencies so you can create native Android applications."
ms.date: 11/01/2023
---
# Install .NET for Android dependencies

In order to build .NET for Android applications you need to install the Android SDK and the Java SDK.

## Using "InstallAndroidDependencies" target

The easiest way to install the required dependencies for your Android application is to run the
[`InstallAndroidDependencies`](../../building-apps/build-targets.md#installandroiddependencies)
MSBuild target.

This target will examine your application project and install the exact components which are needed.
If you update your project to target a new Android API you will need to run this target again
to make sure you get the required components.

For example if you are upgrading your project to target API 34 from API 32, you will only have
API 32 installed. Running the `InstallAndroidDependencies` target will install API 34 for you.

If you do not have the Android SDK installed at all, this target can also handle installing the SDK
on a clean machine. You can change the destination of the installation by setting the
`AndroidSdkDirectory` MSBuild property.  It will also install the Java SDK if the `JavaSdkDirectory`
MSBuild property is provided.

```dotnetcli
dotnet build -t:InstallAndroidDependencies -f net8.0-android -p:AndroidSdkDirectory=c:\work\android-sdk -p:JavaSdkDirectory=c:\work\jdk -p:AcceptAndroidSdkLicenses=True
```

Here are all the arguments which the target will use when installing the dependencies:

* `-p:AndroidSdkDirectory="<PATH>"` installs or updates Android dependencies to the specified path.  
    *Note*: You must use an absolute path; Unix developers should not use tilde (`~`), as it is
    not expanded when used *within* a command-line argument.

* `-p:JavaSdkDirectory="<PATH>"` installs Java to the specified path.  
    *Note*: You must use an absolute path; Unix developers should not use tilde (`~`), as it is
    not expanded when used *within* a command-line argument.

* `-p:AcceptAndroidSDKLicenses=True` accepts the necessary Android licenses for development.

> [!NOTE]
> To make development easier try to avoid using paths which contain spaces or non-ASCII characters.

## Install the Android SDK manually

You might find it necessary to install the Android SDK manually:

 1. Go to [Android Studio download](https://developer.android.com/studio#download).  
    Scroll down to the "Command Line Tools only" section and download the zip file for your operating system.

 2. Create an `android-sdk` directory somewhere on your hard drive. To make your life easier create it near to the root of the drive. For example `c:\android-sdk`.

 3. Extract the files from the zip file into this directory. You should end up with a folder structure like
  `android-sdk\cmdline-tools`

 4. Open a terminal or Command Prompt.

 5. Navigate to the `android-sdk\cmdline-tools\bin` directory within the directory you created.

 6. Run the `sdkmanager` command to install the desired components.

For example, to install the latest platform and platform tools, use:

```console
sdkmanager "platforms;android-34" "platform-tools" "build-tools;34.0.0" "emulator" "system-images;android-34;default;x86_64" "cmdline-tools;11.0" --sdk_root=c:\android-sdk
```

Note that double-quotes should be used liberally to enclose the semicolon `;`, which is part of the component names.

You will be prompted to accept the license, after which the Android SDK will install.

You can use `sdkmanager` to install additional components. You can use the `--list` argument to get a list of all the available components. You can then look through the list and find the additional components you want.

```console
sdkmanager --list
```

The following component types are useful to know:

  * `platforms;android-XX`: Installs the platform `android-XX` into the sdk.
    Replace *XX* with the API Level of your chosen platform.
    For example `platforms;android-30` will install Android API 30, while
    `platforms;android-21` will install Android API 21.

  * `system-images;android-XX;default;x86_64`: Installs an emulator image for
    the specific API level. The `x86_64` can be swapped out for different ABIs
    such as `x86`, `arm64-v8a`, and `x86_64`. These reflect the ABI of the image
    being installed. This can be useful if you have issues on specific ABI's.

It is also good practice to set the `ANDROID_HOME` environment variable, as this
allows you to use certain tooling from the command line.

## Install Microsoft JDK manually

In order to build .NET for Android applications or libraries you need to have a version of the Java Development Kit installed.
We recommend you use the Microsoft Open JDK, this has been tested against our .NET for Android builds:

 1. Download [Microsoft OpenJDK 11](/java/openjdk/download#openjdk-11).

 2. Depending on your platform run the appropriate installer.

 3. It is also good practice to set the `JAVA_HOME` environment variable.
    This will allow you to use the JDK from the Command Prompt or Terminal.
