# Development Tips

Tips and tricks while developing Xamarin.Android.

# How do I rebuild the Mono Runtime and Native Binaries?

The various Mono runtimes -- over *20* of them (!) -- all store object code
within `build-tools/mono-runtimes/obj/$(Configuration)/TARGET`.

If you change sources within `external/mono`, a top-level `make`/`xbuild`
invocation may not rebuild those mono native binaries. To explicitly rebuild
*all* Mono runtimes, you must do two things:

1. Ensure that the timestamp of the HEAD commit in `external/mono` has changed.
2. Use the `ForceBuild` target on `mono-runtimes.mdproj`.

Changing the timestamp of the HEAD commit can be done with `git pull`,
`git commit` or `git commit --amend`. *How* the timestamp changes isn't
important; it needs to change in order for `ForceBuild` to do anything.
(This is admittedly annoying for those working directly on Mono; it requires
an "intermediate" commit in order to trigger a rebuild.)

The `ForceBuild` target can be executed as:

	# Build and install all runtimes
	$ xbuild /t:ForceBuild build-tools/mono-runtimes/mono-runtimes.mdproj

The `ForceBuild` target will build mono for *all* configured architectures,
then invoke the `_InstallRuntimes` target when all the mono's have finished
building; see the `$(AndroidSupportedHostJitAbis)`,
`$(AndroidSupportedTargetAotAbis)`, and `$(AndroidSupportedTargetJitAbis)`
MSBuild properties within [README.md](../README.md). This may not always be
desirable, for example if you're trying to fix a Mono runtime bug for a
specific ABI, and improving turnaround time is paramount.
(Building for all ABIs can be time consuming.)

To build Mono for a specific target, run `make` from the relevant directory,
where the "relevant directory" is the target of interest within
`build-tools/mono-runtimes/obj/$(Configuration)`. When `make` has completed,
invoke the `_InstallRuntimes` target so that the updated native libraries
are copied into `bin/$(Configuration)/lib`, which will allow subsequent
top-level `make` and [`xabuild`](../tools/xabuild) invocations to use them.

For example, to rebuild Mono for armeabi-v7a:

	$ make -C build-tools/mono-runtimes/obj/Debug/armeabi-v7a
	
	# This updates bin/$(Configuration)/lib/xbuild/Xamarin/Android/lib/armeabi-v7a/libmonosgen-2.0.so
	$ xbuild /t:_InstallRuntimes build-tools/mono-runtimes/mono-runtimes.mdproj

# How do I rebuild BCL assemblies?

The Xamarin.Android Base Class Library assemblies, such as `mscorlib.dll`,
are built within `external/mono`, using Mono's normal build system:

	# This updates external/mono/mcs/class/lib/monodroid/ASSEMBLY.dll
	$ make -C external/mono/mcs/class/ASSEMBLY PROFILE=monodroid

Alternatively, if you want to rebuild *all* the assemblies, the "host"
Mono needs to be rebuilt. Note that the name of the "host" directory
varies based on the operating system you're building from:

	$ make -C build-tools/mono-runtimes/obj/Debug/host-Darwin

Once the assemblies have been rebuilt, they can be copied into the appropriate
Xamarin.Android SDK directory by using the `_InstallBcl` target:

	# This updates bin/$(Configuration)/lib/xbuild-frameworks/MonoAndroid/v1.0/ASSEMBLY.dll
	$ xbuild build-tools/mono-runtimes/mono-runtimes.mdproj /t:_InstallBcl

# Update Directory

When a Xamarin.Android app launches on an Android device, and the app was
built in the `Debug` configuration, it will create an "update" directory
during process startup, printing the created directory to `adb logcat`:

	 W/monodroid( 2796): Creating public update directory: `/data/data/Mono.Android_Tests/files/.__override__`

When the app needs to resolve native libraries and assemblies, it will look
for those files within the update directory *first*. This includes the Mono
runtime library and BCL assemblies.

Note that the update directory is *per-app*. The above mentioned `Mono.Android_Tests`
directory is created when running the
[`Mono.Android-Tests.csproj`](../src/Mono.Android/Test/Mono.Android-Tests.csproj)
unit tests.

The update directory is not used in `Release` configuration builds.
(Note: `Release` configuration for the *app itself*, not for xamarin-android.)

For example, if you're working on a mono/x86 bug and need to quickly update
the app on the device to test `libmonosgen-2.0.so` changes:

	$ make -C build-tools/mono-runtimes/obj/Debug/x86 && \
	  adb push build-tools/mono-runtimes/obj/Debug/x86/mono/mini/.libs/libmonosgen-2.0.so \
	    /data/data/Mono.Android_Tests/files/.__override__

Alternatively, if you're working on an `mscorlib.dll` bug:

	$ make -C external/mono/mcs/class/corlib PROFILE=monodroid && \
	  adb push external/mono/mcs/class/lib/monodroid/mscorlib.dll \
	    /data/data/Mono.Android_Tests/files/.__override__

# Unit Tests

The `xamarin-android` repo contains several unit tests:

* NUnit-based unit tests, for stand-alone assemblies and utilities.

* `.apk`-based unit tests, which are NUnitLite-based tests that need to
    execute on an Android device.

All unit tests can be executed via the `make run-all-tests` target:

	$ make run-all-tests

All NUnit-based tests can be executed via the `make run-nunit-tests` target:

	$ make run-nunit-tests

All `.apk`-based unit tests can be executed via the `make run-apk-tests` target:

	$ make run-apk-tests


## Running Individual NUnit Tests

Individual NUnit-based tests can be executed by overriding the `$(NUNIT_TESTS)`
make variable:

	$ make run-nunit-tests NUNIT_TESTS=bin/TestDebug/Xamarin.Android.Build.Tests.dll

## Running Individual `.apk` Projects

See also the [`tests/RunApkTests.targets`](../tests/RunApkTests.targets) and
[`build-tools/scripts/UnitTestApks.targets`](../build-tools/scripts/UnitTestApks.targets)
files.

All `.apk`-based unit test projects provide the following targets:

* `DeployUnitTestApks`: Installs the associated `.apk` to an Android device.
* `UndeployUnitTestApks`: Uninstalls the associated `.apk` from an Android device.
* `RunUnitTestApks`: Executes the unit tests contained within a `.apk`.
    Must be executed *after* the `DeployUnitTestApks` target.

To run an individual `.apk`-based test project, a package must be built, using the
`SignAndroidPackage` target, installed, and executed.

For example:

	$ tools/scripts/xabuild /t:SignAndroidPackage tests/locales/Xamarin.Android.Locale-Tests/Xamarin.Android.Locale-Tests.csproj
	$ tools/scripts/xabuild /t:DeployUnitTestApks tests/locales/Xamarin.Android.Locale-Tests/Xamarin.Android.Locale-Tests.csproj
	$ tools/scripts/xabuild /t:RunUnitTestApks    tests/locales/Xamarin.Android.Locale-Tests/Xamarin.Android.Locale-Tests.csproj

### Running A Single Test Fixture

A single NUnit *Test Fixture* -- a class with the `[TestFixture]`
custom attribute -- may be executed instead of executing *all* test fixtures.

The `RunUnitTestApks` target accepts a `TestFixture` MSBuild property
to specify the test fixture class to execute:

	$ tools/scripts/xabuild /t:RunUnitTestApks \
	    /p:TestFixture=Xamarin.Android.LocaleTests.SatelliteAssemblyTests \
	    tests/locales/Xamarin.Android.Locale-Tests/Xamarin.Android.Locale-Tests.csproj

If using `Xamarin.Android.NUnitLite` for projects outside the `xamarin-android`
repository, such as NUnit tests for a custom app, the `RunUnitTestApks` target
will not exist. In such scenarios, the [`adb shell am`][adb-shell-am]
`instrument` command can be used instead. It follows the format:

[adb-shell-am]: https://developer.android.com/studio/command-line/adb.html#am

	$ adb shell am instrument -e suite FIXTURE -w PACKAGE/INSTRUMENTATION

Where:

* `FIXTURE` is the full *managed* class name of the NUnit test fixture to
    execute.
* `PACKAGE` is the Android package name containing the NUnit tests
* `INSTRUMENTATION` is the *Java callable wrapper* class name to execute,
    located within the Android package `PACKAGE`.

For example:

	$ adb shell am instrument -e suite Xamarin.Android.LocaleTests.SatelliteAssemblyTests \
		-w "Xamarin.Android.Locale_Tests/xamarin.android.localetests.TestInstrumentation"

# Debugging using lldb

Download a precompiled lldb binary from
<https://github.com/mono/lldb-binaries/releases>, and follow the instructions
within [README.md][lldb-readme].

[lldb-readme]: https://github.com/mono/lldb-binaries/blob/master/README.md
