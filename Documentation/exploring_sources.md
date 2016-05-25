# Exploring Xamarin.Android SDK source code

Welcome to Xamarin.Android open source world! This documentation tries to
demystify the entire jungle of the source code that makes up this SDK.


* `bin`
  `BuildDebug` | `BuildRelease` - build-tools binaries
  `Debug` | `Release` - binaries for the product outcome (SDK)
  `TestDebug` | `TestRelease` - binaries for tests
* `build-tools` - contains sources for tools that are used only at building this SDK itself.
  * `android-toolchain` - MSBuild artifacts to set up Android SDK and NDK.
  * `api-merge` - used to merge API description for various API levels.
  * `jnienv-gen` - source generator for JNIEnv class.
  * `mono-runtimes` - MSBuild artifacts to build mono runtime.
* Documentation - this directory!
* `external` - submodules
  * `Java.Interop` - implements interoperability between Java and Mono runtime via JNI.
  * `mono` - we build mono runtime for Android to be embedded, for each architecture.
* `packages` - There would be a lot of downloaded NuGet packages contents.
* `samples` - contains application samples.
* `src`
  * `Mono.Android` - Mono.Android.dll sources.
  * `Mono.Android.Export` - Mono.Android.Export.dll sources.
  * `monodroid` - "libmonodroid" (Android mono bootstrapper).
  * `Xamarin.Android.Build.Tasks` - MSBuild tasks for Android projects, primary sources.
  * `Xamarin.Android.Build.Utilities` - MSBuild tasks support sources.
  * `Xamarin.Android.NUnitLite` - NUnitLite for Android sources.
  * `Xamarin.AndroidTools.Aidl` - AIDL processor, used in MSBuild tasks.
  * `Xamarin.Android.Tools.BootstrapTasks` - supplemental build tasks used by some build-tools
* `tools`
  * `api-xml-adjuster` - part of binding generator toolset to generate old `jar2xml` compatible XML format from `class-parse` tool.
  * `scripts` - contains shell scripts such as `xabuild`

