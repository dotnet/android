# Build Dependencies for Windows

Building Xamarin.Android requires:

  * An existing installation of the Xamarin.Android SDK and the Android SDK
  * The .NET Framework 3.5 &ndash; 4.7 development tools
  * Git for Windows
  * The Java Development Kit (JDK)

The recommended steps to install these dependencies are:

 1. Run the [Visual Studio Installer](https://visualstudio.microsoft.com/vs/).

 2. Under the **Workloads** tab, ensure that the **Mobile development with
    .NET** workload is installed.  Under the **Optional** items for the
    workload, ensure **Android SDK setup** is selected.

 3. Also ensure the **.NET desktop development** workload is installed.  Under
    the **Optional** items for the workload, ensure the following items are
    installed:

      * **.NET Framework 4 &ndash; 4.6 development tools**
      * **.NET Framework 4.6.2 development tools**
      * **.NET Framework 4.7 development tools**
      * **.NET Core 2.0 development tools**

    The following items are also recommended:

      * **.NET Framework 4.7.1 development tools**
      * **.NET Framework 4.7.2 development tools**

 4. Under the **Individual components** tab, ensure that **Code tools > Git for
    Windows** is installed.

 5. Ensure the .NET Framework 3.5 SP1 Runtime is installed by downloading and
    running the installer from
    <https://www.microsoft.com/net/download/visual-studio-sdks>.

 6. Download and install the Java SE 8 JDK from the [Oracle
    website][oracle-jdk].  You can use either the Windows x64 or Windows x86
    version.  Make sure **Development Tools** is selected for installation when
    running the installer.

[oracle-jdk]: http://www.oracle.com/technetwork/java/javase/downloads/index.html
