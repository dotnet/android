using System.Reflection;
using System.Runtime.CompilerServices;
using Android.App;

// Information about this assembly is defined by the following attributes.
// Change them to the values specific to your project.
[assembly: AssemblyTitle ("AndroidLibBinding")]
[assembly: AssemblyDescription ("")]
[assembly: AssemblyConfiguration ("")]
[assembly: AssemblyCompany ("")]
[assembly: AssemblyProduct ("")]
[assembly: AssemblyCopyright ("rodo")]
[assembly: AssemblyTrademark ("")]
[assembly: AssemblyCulture ("")]
// The assembly version has the format "{Major}.{Minor}.{Build}.{Revision}".
// The form "{Major}.{Minor}.*" will automatically update the build and revision,
// and "{Major}.{Minor}.{Build}.*" will update just the revision.
[assembly: AssemblyVersion ("1.0.0")]
// The following attributes are used to specify the signing key for the assembly,
// if desired. See the Mono documentation for more information about signing.
//[assembly: AssemblyDelaySign(false)]
//[assembly: AssemblyKeyFile("")]

[assembly: Android.IncludeAndroidResourcesFromAttribute ("./",
	SourceUrl="file:///JavaLib.zip")]
[assembly: Java.Interop.JavaLibraryReference ("classes.jar",
	SourceUrl="file:///JavaLib.zip")]

// native library path should contain abi
[assembly: Android.NativeLibraryReference ("arm64-v8a/libsimple.so",
	SourceUrl="file:///NativeLib.zip", Version="native-lib-1")]
[assembly: Android.NativeLibraryReference ("armeabi-v7a/libsimple.so",
	SourceUrl="file:///NativeLib.zip", Version="native-lib-1")]
[assembly: Android.NativeLibraryReference ("x86/libsimple.so",
	SourceUrl="file:///NativeLib.zip", Version="native-lib-1")]
[assembly: Android.NativeLibraryReference ("x86_64/libsimple.so",
	SourceUrl="file:///NativeLib.zip", Version="native-lib-1")]

// native library path should contain abi
[assembly: Android.NativeLibraryReference ("arm64-v8a/libsimple2.so",
	EmbeddedArchive="aar-test/EmbeddedNativeLib.zip",
	SourceUrl="file:///NativeLib2.zip", Version="native-lib-2")]
[assembly: Android.NativeLibraryReference ("armeabi-v7a/libsimple2.so",
	EmbeddedArchive="aar-test/EmbeddedNativeLib.zip",
	SourceUrl="file:///NativeLib2.zip", Version="native-lib-2")]
[assembly: Android.NativeLibraryReference ("x86/libsimple2.so",
	EmbeddedArchive="aar-test/EmbeddedNativeLib.zip",
	SourceUrl="file:///NativeLib2.zip", Version="native-lib-2")]
[assembly: Android.NativeLibraryReference ("x86_64/libsimple2.so",
	EmbeddedArchive="aar-test/EmbeddedNativeLib.zip",
	SourceUrl="file:///NativeLib2.zip", Version="native-lib-2")]
