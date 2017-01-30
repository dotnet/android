# Development Tips

Tips and tricks while developing Xamarin.Android.

# How do I rebuild the Mono Runtime and Native Binaries?

The various Mono runtimes -- over *20* of them (!) -- all store object code
within `build-tools/mono-runtimes/obj/$(Configuration)/TARGET`.

If you change sources within `external/mono`, a top-level `make`/`xbuild`
invocation may not rebuild those mono native binaries. To explicitly rebuild
*all* Mono runtimes, use the `ForceBuild` target:

	# Build and install all runtimes
	$ xbuild /t:ForceBuild build-tools/mono-runtimes/mono-runtimes.mdproj

To build Mono for a specific target, run `make` from the relevant directory
and invoke the `_InstallRuntimes` target. For example, to rebuild
Mono for armeabi-v7a:

	$ cd build-tools/mono-runtimes
	$ make -C obj/Debug/armeabi-v7a
	
	# This updates bin/$(Configuration)/lib/xbuild/Xamarin/Android/lib/armeabi-v7a/libmonosgen-2.0.so
	$ xbuild /t:_InstallRuntimes

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


# Testing Updated Assemblies

The `xamarin-android` repo does not support [fast deployment][fastdep],
which means that, *normally*, if you wanted to e.g. test a fix within
`Mono.Android.dll` you would need to:

[fastdep]: https://developer.xamarin.com/guides/android/under_the_hood/build_process/#Fast_Deployment

1. Build `src/Mono.Android/Mono.Android.csproj`
2. Rebuild your test project, e.g.
    `src/Mono.Android/Test/Mono.Android-Tests.csproj`
3. Reinstall the test project
4. Re-run the test project.

The resulting `.apk`s can be quite big, e.g.
`bin/TestDebug/Mono.Android_Tests-Signed.apk` is 59MB, so steps
(2) through (4) can be annoyingly time consuming.

Fortunately, a key part of fast deployment *is* part of the `xamarin-android`:
an "update directory" is created by `libmono-android*.so` during process
startup, in *non*-`RELEASE` builds. This directory is printed to `adb logcat`:

	 W/monodroid( 2796): Creating public update directory: `/data/data/Mono.Android_Tests/files/.__override__`

Assemblies located within the "update directory" are used *in preference to*
assemblies located within the executing `.apk`. Assemblies can be `adb push`ed
into the update directory:

	adb push bin/Debug/lib/xbuild-frameworks/MonoAndroid/v7.1/Mono.Android.dll /data/data/Mono.Android_Tests/files/.__override__

When the process restarts the new assembly will be used.

# Debugging using lldb

Download a precompiled lldb binary from
`https://github.com/mono/lldb-binaries/releases`, and follow the instructions
in README.md.
