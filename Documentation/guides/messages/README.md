---
title: Xamarin.Android errors and warnings reference
description: Build and deployment error and warning codes in Xamarin.Android, their meanings, and guidance on how to address them.
ms.date: 01/24/2020
---
# Xamarin.Android errors and warnings reference


## ADBxxxx: ADB tooling

+ [ADB0000](adb0000.md): Generic `adb` error/warning.
+ [ADB0010](adb0010.md): Generic `adb` APK installation error/warning.
+ [ADB0020](adb0020.md): The package does not support the CPU architecture of this device.
+ [ADB0030](adb0030.md): APK installation failed due to a conflict with the existing package.
+ [ADB0040](adb0040.md): The device does not support the minimum SDK level specified in the manifest.
+ [ADB0050](adb0050.md): Package {packageName} already exists on device.
+ [ADB0060](adb0060.md): There is not enough storage space on the device to store package: {packageName}. Free up some space and try again.

## ANDXXxxxx: Generic Android tooling

+ [ANDAS0000](andas0000.md): Generic `apksigner` error/warning.
+ [ANDJS0000](andjs0000.md): Generic `jarsigner` error/warning.
+ [ANDKT0000](andkt0000.md): Generic `keytool` error/warning.
+ [ANDZA0000](andza0000.md): Generic `zipalign` error/warning.

## APTxxxx: AAPT tooling

+ [APT0000](apt0000.md): Generic `aapt` error/warning.
+ [APT0001](apt0001.md): Unknown option \`{option}\`. Please check \`$(AndroidAapt2CompileExtraArgs)\` and \`$(AndroidAapt2LinkExtraArgs)\` to see if they include any \`aapt\` command line arguments that are no longer valid for \`aapt2\` and ensure that all other arguments are valid for `aapt2`.
+ APT0002: Invalid file name: It must contain only \[^a-zA-Z0-9_.-\]+.
+ APT0003: Invalid file name: It must contain only \[^a-zA-Z0-9_.\]+.

## JAVACxxxx: Java compiler

+ [JAVAC0000](javac0000.md): Generic Java compiler error

## XA0xxx: Environment issue or missing tooling

+ [XA0000](xa0000.md): Could not determine $(AndroidApiLevel) or $(TargetFrameworkVersion).
+ [XA0001](xa0001.md): Invalid or unsupported `$(TargetFrameworkVersion)` value.
+ [XA0002](xa0002.md): Could not find mono.android.jar
+ [XA0003](xa0003.md): Invalid \`android:versionCode\` value \`{code}\` in \`AndroidManifest.xml\`. It must be an integer value.
+ [XA0004](xa0004.md): Invalid \`android:versionCode\` value \`{code}\` in \`AndroidManifest.xml\`. The value must be in the range of 0 to {maxVersionCode}.
+ [XA0030](xa0030.md): Building with JDK version `{versionNumber}` is not supported.
+ [XA0031](xa0031.md): Java SDK {requiredJavaForFrameworkVersion} or above is required when when using $(TargetFrameworkVersion) {targetFrameworkVersion}.
+ [XA0032](xa0032.md): Java SDK {requiredJavaForBuildTools} or above is required when using Android SDK Build-Tools {buildToolsVersion}.
+ [XA0033](xa0033.md): Failed to get the Java SDK version because the returned value does not appear to contain a valid version number.
+ [XA0034](xa0034.md): Failed to get the Java SDK version.
+ [XA0035](xa0035.md): Failed to determine the Android ABI for the project.
+ [XA0036](xa0036.md): $(AndroidSupportedAbis) is not supported in .NET 5 and higher.
+ XA0100: EmbeddedNativeLibrary is invalid in Android Application projects. Please use AndroidNativeLibrary instead.
+ [XA0101](xa0101.md): warning XA0101: @(Content) build action is not supported.
+ [XA0102](xa0102.md): Generic `lint` Warning.
+ [XA0103](xa0103.md): Generic `lint` Error.
+ XA0104: Invalid value for \`$(AndroidSequencePointsMode)\`
+ [XA0105](xa0105.md): The $(TargetFrameworkVersion) for a library is greater than the $(TargetFrameworkVersion) for the application project.
+ [XA0107](xa0107.md): `{Assmebly}` is a Reference Assembly.
+ [XA0108](xa0108.md): Could not get version from `lint`.
+ [XA0109](xa0109.md): Unsupported or invalid `$(TargetFrameworkVersion)` value of 'v4.5'.
+ [XA0111](xa0111.md): Could not get the `aapt2` version. Please check it is installed correctly.
+ [XA0112](xa0112.md): `aapt2` is not installed. Disabling `aapt2` support. Please check it is installed correctly.
+ [XA0113](xa0113.md): Google Play requires that new applications and updates must use a TargetFrameworkVersion of v8.0 (API level 26) or above.
+ [XA0115](xa0115.md): Invalid value 'armeabi' in $(AndroidSupportedAbis). This ABI is no longer supported. Please update your project properties to remove the old value. If the properties page does not show an 'armeabi' checkbox, un-check and re-check one of the other ABIs and save the changes.
+ [XA0116](xa0116.md): Unable to find `EmbeddedResource` named `{ResourceName}`.
+ [XA0117](xa0117.md): The TargetFrameworkVersion {TargetFrameworkVersion} is deprecated. Please update it to be v4.4 or higher.
+ [XA0118](xa0118.md): Could not parse '{TargetMoniker}'
+ [XA0119](xa0119.md): A non-ideal configuration was found in the project.
+ [XA0121](xa0121.md): Assembly '{assembly}' is using '[assembly: Java.Interop.JavaLibraryReferenceAttribute]', which is no longer supported. Use a newer version of this NuGet package or notify the library author.
+ [XA0122](xa0122.md): Assembly '{assembly}' is using a deprecated attribute '[assembly: Java.Interop.DoNotPackageAttribute]'. Use a newer version of this NuGet package or notify the library author.
+ XA0123: Removing {issue} from {propertyName}. Lint {version} does not support this check.
+ [XA0124](xa0124.md): Interpreter is not supported by the x86 ABI
+ [XA0125](xa0125.md): Sorry. This device does not support Fast Deployment. Please rebuild your app using `EmbedAssembliesIntoApk = True`.
+ [XA0126](xa0126.md): Error installing FastDev Tools. This device does not support Fast Deployment. Please rebuild your app using `EmbedAssembliesIntoApk = True`.
+ [XA0127](xa0127.md): There was an issue deploying {destination} using {FastDevTool}. We encountered the following error {output}. Please rebuild your app using `EmbedAssembliesIntoApk = True`.
+ [XA0128](xa0128.md): Stdio Redirection is enabled. Please disable it to use Fast Deployment.

## XA1xxx: Project related

+ [XA1000](xa1000.md): There was a problem parsing {file}. This is likely due to incomplete or invalid XML.
+ [XA1001](xa1001.md): AndroidResgen: Warning while updating Resource XML '{filename}': {Message}
+ [XA1002](xa1002.md): The closest match found for '{customViewName}' is '{customViewLookupName}', but the capitalization does not match. Please correct the capitalization.
+ [XA1003](xa1003.md): '{zip}' does not exist. Please rebuild the project.
+ [XA1004](xa1004.md): There was an error opening {filename}. The file is probably corrupt. Try deleting it and building again.
+ [XA1005](xa1005.md): Attempting basic type name matching for element with ID '{id}' and type '{managedType}'
+ [XA1006](xa1006.md): The TargetFrameworkVersion (Android API level {compileSdk}) is higher than the targetSdkVersion ({targetSdk}).
+ [XA1007](xa1007.md): The minSdkVersion ({minSdk}) is greater than targetSdkVersion. Please change the value such that minSdkVersion is less than or equal to targetSdkVersion ({targetSdk}).
+ [XA1008](xa1008.md): The TargetFrameworkVersion (Android API level {compileSdk}) is lower than the targetSdkVersion ({targetSdk}).
+ [XA1009](xa1009.md): The {assembly} is Obsolete. Please upgrade to {assembly} {version}
+ [XA1010](xa1010.md): Invalid \`$(AndroidManifestPlaceholders)\` value for Android manifest placeholders. Please use \`key1=value1;key2=value2\` format. The specified value was: `{placeholders}`
+ [XA1011](xa1011.md): Using ProGuard with the D8 DEX compiler is no longer supported. Please set the code shrinker to \`r8\` in the Visual Studio project property pages or edit the project file in a text editor and set the \`AndroidLinkTool\` MSBuild property to \`r8\`.
+ XA1012: Included layout root element override ID '{id}' is not valid.
+ XA1013: Failed to parse ID of node '{name}' in the layout file '{file}'.
+ XA1014: JAR library references with identical file names but different contents were found: {libraries}. Please remove any conflicting libraries from EmbeddedJar, InputJar and AndroidJavaLibrary.
+ XA1015: More than one Android Wear project is specified as the paired project. It can be at most one.
+ XA1016: Target Wear application's project '{project}' does not specify required 'AndroidManifest' project property.
+ XA1017: Target Wear application's AndroidManifest.xml does not specify required 'package' attribute.
+ XA1018: Specified AndroidManifest file does not exist: {file}.
+ XA1019: \`LibraryProjectProperties\` file \`{file}\` is located in a parent directory of the bindings project's intermediate output directory. Please adjust the path to use the original \`project.properties\` file directly from the Android library project directory.
+ XA1020: At least one Java library is required for binding. Check that a Java library is included in the project and has the appropriate build action: 'LibraryProjectZip' (for AAR or ZIP), 'EmbeddedJar', 'InputJar' (for JAR), or 'LibraryProjectProperties' (project.properties).
+ XA1021: Specified source Java library not found: {file}
+ XA1022: Specified reference Java library not found: {file}
+ [XA1023](xa1023.md): Using the DX DEX Compiler is deprecated.
+ [XA1024](xa1024.md): Ignoring configuration file 'Foo.dll.config'. .NET configuration files are not supported in Xamarin.Android projects that target .NET 5 or higher.

## XA2xxx: Linker

+ [XA2000](xa2000.md): Use of AppDomain.CreateDomain() detected in assembly: {assembly}. .NET 5 will only support a single AppDomain, so this API will no longer be available in Xamarin.Android once .NET 5 is released.
+ [XA2001](xa2001.md): Source file '{filename}' could not be found.
+ [XA2002](xa2002.md): Can not resolve reference: \`{missing}\`, referenced by {assembly}. Perhaps it doesn't exist in the Mono for Android profile?
+ XA2006: Could not resolve reference to '{member}' (defined in assembly '{assembly}') with scope '{scope}'. When the scope is different from the defining assembly, it usually means that the type is forwarded.
+ XA2007: Exception while loading assemblies: {exception}
+ XA2008: In referenced assembly {assembly}, Java.Interop.DoNotPackageAttribute requires non-null file name.

## XA3xxx: Unmanaged code compilation

+ XA3001: Could not AOT the assembly: {assembly}
+ XA3002: Invalid AOT mode: {mode}
+ XA3003: Could not strip IL of assembly: {assembly}
+ XA3004: Android NDK r10d is buggy and provides an incompatible x86_64 libm.so.
+ XA3005: The detected Android NDK version is incompatible with the targeted LLVM configuration.
+ XA3006: Could not compile native assembly file: {file}
+ XA3007: Could not link native shared library: {library}

## XA4xxx: Code generation

+ XA4209: Failed to generate Java type for class: {managedType} due to {exception}
+ XA4210: Please add a reference to Mono.Android.Export.dll when using ExportAttribute or ExportFieldAttribute.
+ XA4211: AndroidManifest.xml //uses-sdk/@android:targetSdkVersion '{targetSdk}' is less than $(TargetFrameworkVersion) '{targetFramework}'. Using API-{targetFrameworkApi} for ACW compilation.
+ XA4213: The type '{type}' must provide a public default constructor
+ [XA4214](xa4214.md): The managed type \`Library1.Class1\` exists in multiple assemblies: Library1, Library2. Please refactor the managed type names in these assemblies so that they are not identical.
+ [XA4215](xa4215.md): The Java type \`com.contoso.library1.Class1\` is generated by more than one managed type. Please change the \[Register\] attribute so that the same Java type is not emitted.
+ [XA4216](xa4216.md): AndroidManifest.xml //uses-sdk/@android:minSdkVersion '{min_sdk?.Value}' is less than API-{XABuildConfig.NDKMinimumApiAvailable}, this configuration is not supported.
+ XA4217: Cannot override Kotlin-generated method '{method}' because it is not a valid Java method name. This method can only be overridden from Kotlin.
+ [XA4218](xa4218.md): Unable to find //manifest/application/uses-library at path: {path}
+ XA4219: Cannot find binding generator for language {language} or {defaultLanguage}.
+ XA4220: Partial class item '{file}' does not have an associated binding for layout '{layout}'.
+ XA4221: No layout binding source files were generated.
+ XA4222: No widgets found for layout ({layoutFiles}).
+ XA4223: Malformed full class name '{name}'. Missing namespace.
+ XA4224: Malformed full class name '{name}'. Missing class name.
+ XA4225: Widget '{widget}' in layout '{layout}' has multiple instances with different types. The property type will be set to: {type}
+ XA4226: Resource item '{file}' does not have the required metadata item '{metadataName}'.
+ XA4228: Unable to find specified //activity-alias/@android:targetActivity: '{targetActivity}'
+ XA4229: Unrecognized \`TransformFile\` root element: {element}.
+ XA4230: Error parsing XML: {exception}
+ [XA4231](xa4231.md): The Android class parser value 'jar2xml' is deprecated and will be removed in a future version of Xamarin.Android. Update the project properties to use 'class-parse'.
+ [XA4232](xa4232.md): The Android code generation target 'XamarinAndroid' is deprecated and will be removed in a future version of Xamarin.Android. Update the project properties to use 'XAJavaInterop1'.
+ XA4300: Native library '{library}' will not be bundled because it has an unsupported ABI.
+ [XA4301](xa4301.md): Apk already contains the item `xxx`.
+ [XA4302](xa4302.md): Unhandled exception merging \`AndroidManifest.xml\`: {ex}
+ [XA4303](xa4303.md): Error extracting resources from "{assemblyPath}": {ex}
+ [XA4304](xa4304.md): ProGuard configuration file '{file}' was not found.
+ [XA4305](xa4305.md): Multidex is enabled, but \`$(\_AndroidMainDexListFile)\` is empty.
+ [XA4306](xa4306.md): R8 does not support \`@(MultiDexMainDexList)\` files when android:minSdkVersion >= 21
+ [XA4307](xa4307.md): Invalid ProGuard configuration file.
+ [XA4308](xa4308.md): Failed to generate type maps
+ [XA4309](xa4309.md): 'MultiDexMainDexList' file '{file}' does not exist.
+ [XA4310](xa4310.md): \`$(AndroidSigningKeyStore)\` file \`{keystore}\` could not be found.
+ XA4311: The application won't contain the paired Wear package because the Wear application package APK is not created yet. If building on the command line, be sure to build the "SignAndroidPackage" target.
+ [XA4312](xa4312.md): Referencing an Android Wear application project from an Android application project is deprecated.

## XA5xxx: GCC and toolchain

+ XA5101: Missing Android NDK toolchains directory '{path}'. Please install the Android NDK.
+ XA5102: Conversion from assembly to native code failed. Exit code {exitCode}
+ XA5103: NDK C compiler exited with an error. Exit code {0}
+ XA5104: Could not locate the Android NDK.
+ XA5105: Toolchain utility '{utility}' for target {arch} was not found. Tried in path: "{path}"
+ XA5201: NDK linker exited with an error. Exit code {0}
+ [XA5205](xa5205.md): Cannot find `{ToolName}` in the Android SDK.
+ [XA5207](xa5207.md): Could not find android.jar for API level `{compileSdk}`.
+ XA5211: Embedded Wear app package name differs from handheld app package name ({wearPackageName} != {handheldPackageName}).
+ XA5213: java.lang.OutOfMemoryError. Consider increasing the value of $(JavaMaximumHeapSize). Java ran out of memory while executing '{tool} {arguments}'
+ [XA5300](xa5300.md): The Android/Java SDK Directory could not be found.
+ [XA5301](xa5301.md): Failed to generate Java type for class: {managedType} due to MAX_PATH: {exception}
+ [XA5302](xa5302.md): Two processes may be building this project at once. Lock file exists at path: {path}

## XA6xxx: Internal tools

## XA7xxx: Unhandled MSBuild Exceptions

Exceptions that have not been gracefully handled yet.  Ideally these will be fixed or replaced with better errors in the future.

These take the form of `XACCC7NNN`, where `CCC` is a 3 character code denoting the MSBuild Task that is throwing the exception,
and `NNN` is a 3 digit number indicating the type of the unhandled `Exception`.

**Tasks:**
* `A2C` - `Aapt2Compile`
* `A2L` - `Aapt2Link`
* `AAS` - `AndroidApkSigner`
* `ACD` - `AndroidCreateDebugKey`
* `ACM` - `AppendCustomMetadataToItemGroup`
* `ADB` - `Adb`
* `AJV` - `AdjustJavacVersionArguments`
* `AOT` - `Aot`
* `APT` - `Aapt`
* `ASP` - `AndroidSignPackage`
* `AZA` - `AndroidZipAlign`
* `BAB` - `BuildAppBundle`
* `BAS` - `BuildApkSet`
* `BBA` - `BuildBaseAppBundle`
* `BGN` - `BindingsGenerator`
* `BLD` - `BuildApk`
* `CAL` - `CreateAdditionalLibraryResourceCache`
* `CAR` - `CalculateAdditionalResourceCacheDirectories`
* `CCR` - `CopyAndConvertResources`
* `CCV` - `ConvertCustomView`
* `CDF` - `ConvertDebuggingFiles`
* `CDJ` - `CheckDuplicateJavaLibraries`
* `CFI` - `CheckForInvalidResourceFileNames`
* `CFR` - `CheckForRemovedItems`
* `CGJ` - `CopyGeneratedJavaResourceClasses`
* `CGS` - `CheckGoogleSdkRequirements`
* `CIC` - `CopyIfChanged`
* `CIL` - `CilStrip`
* `CLA` - `CollectLibraryAssets`
* `CLC` - `CalculateLayoutCodeBehind`
* `CLP` - `ClassParse`
* `CLR` - `CreateLibraryResourceArchive`
* `CMD` - `CreateMultiDexMainDexClassList`
* `CML` - `CreateManagedLibraryResourceArchive`
* `CMM` - `CreateMsymManifest`
* `CNA` - `CompileNativeAssembly`
* `CNE` - `CollectNonEmptyDirectories`
* `CNL` - `CreateNativeLibraryArchive`
* `CPD` - `CalculateProjectDependencies`
* `CPF` - `CollectPdbFiles`
* `CPI` - `CheckProjectItems`
* `CPR` - `CopyResource`
* `CPT` - `ComputeHash`
* `CRC` - `ConvertResourcesCases`
* `CRM` - `CreateResgenManifest`
* `CRN` - `Crunch`
* `CRP` - `AndroidComputeResPaths`
* `CTD` - `CreateTemporaryDirectory`
* `CTX` - `CompileToDalvik`
* `DES` - `Desugar`
* `DJL` - `DetermineJavaLibrariesToCompile`
* `DX8` - `D8`
* `FD`  - `FastDeploy`
* `FLB` - `FindLayoutsToBind`
* `FLT` - `FilterAssemblies`
* `GAD` - `GetAndroidDefineConstants`
* `GAP` - `GetAndroidPackageName`
* `GAR` - `GetAdditionalResourcesFromAssemblies`
* `GAS` - `GetAppSettingsDirectory`
* `GCB` - `GenerateCodeBehindForLayout`
* `GCJ` - `GetConvertedJavaLibraries`
* `GEP` - `GetExtraPackages`
* `GFT` - `GetFilesThatExist`
* `GIL` - `GetImportedLibraries`
* `GJP` - `GetJavaPlatformJar`
* `GJS` - `GenerateJavaStubs`
* `GLB` - `GenerateLayoutBindings`
* `GLR` - `GenerateLibraryResources`
* `GMA` - `GenerateManagedAidlProxies`
* `GMJ` - `GetMonoPlatformJar`
* `GPM` - `GeneratePackageManagerJava`
* `GRD` - `GenerateResourceDesigner`
* `IAS` - `InstallApkSet`
* `IJD` - `ImportJavaDoc`
* `JDC` - `JavaDoc`
* `JVC` - `Javac`
* `JTX` - `JarToXml`
* `KEY` - `KeyTool`
* `LAS` - `LinkApplicationSharedLibraries`
* `LEF` - `LogErrorsForFiles`
* `LNK` - `LinkAssemblies`
* `LNS` - `LinkAssembliesNoShrink`
* `LNT` - `Lint`
* `LWF` - `LogWarningsForFiles`
* `MBN` - `MakeBundleNativeCodeExternal`
* `MDC` - `MDoc`
* `PAI` - `PrepareAbiItems`
* `PAW` - `ParseAndroidWearProjectAndManifest`
* `PRO` - `Proguard`
* `PWA` - `PrepareWearApplicationFiles`
* `R8D` - `R8`
* `RAM` - `ReadAndroidManifest`
* `RAR` - `ReadAdditionalResourcesFromAssemblyCache`
* `RAT` - `ResolveAndroidTooling`
* `RDF` - `RemoveDirFixed`
* `RIL` - `ReadImportedLibrariesCache`
* `RJJ` - `ResolveJdkJvmPath`
* `RLC` - `ReadLibraryProjectImportsCache`
* `RLP` - `ResolveLibraryProjectImports`
* `RRA` - `RemoveRegisterAttribute`
* `RSA` - `ResolveAssemblies`
* `RSD` - `ResolveSdks`
* `RUF` - `RemoveUnknownFiles`
* `SPL` - `SplitProperty`
* `SVM` - `SetVsMonoAndroidRegistryKey`
* `UNZ` - `Unzip`
* `VJV` - `ValidateJavaVersion`
* `WLF` - `WriteLockFile`

**Exceptions:**

* `7000` - Other Exception
* `7001` - `NullReferenceException`
* `7002` - `ArgumentOutOfRangeException`
* `7003` - `ArgumentNullException`
* `7004` - `ArgumentException`
* `7005` - `FormatException`
* `7006` - `IndexOutOfRangeException`
* `7007` - `InvalidCastException`
* `7008` - `ObjectDisposedException`
* `7009` - `InvalidOperationException`
* `7010` - `InvalidProgramException`
* `7011` - `KeyNotFoundException`
* `7012` - `TaskCanceledException`
* `7013` - `OperationCanceledException`
* `7014` - `OutOfMemoryException`
* `7015` - `NotSupportedException`
* `7016` - `StackOverflowException`
* `7017` - `TimeoutException`
* `7018` - `TypeInitializationException`
* `7019` - `UnauthorizedAccessException`
* `7020` - `ApplicationException`
* `7021` - `KeyNotFoundException`
* `7022` - `PathTooLongException`
* `7023` - `DirectoryNotFoundException`
* `7024` - `IOException`
* `7025` - `DriveNotFoundException`
* `7026` - `EndOfStreamException`
* `7027` - `FileLoadException`
* `7028` - `FileNotFoundException`
* `7029` - `PipeException`

## XA8xxx:	Reserved

## XA9xxx:	Licensing

## Removed messages

### Removed in Xamarin.Android 10.4

+ XA5215: Duplicate Resource found for {elementName}. Duplicates are in {filenames}
+ XA5216: Resource entry {elementName} is already defined in {filename}

### Removed in Xamarin.Android 10.3

+ [XA0110](xa0110.md): Disabling $(AndroidExplicitCrunch) as it is not supported by `aapt2`. If you wish to use $(AndroidExplicitCrunch) please set $(AndroidUseAapt2) to false.

### Removed in Xamarin.Android 10.2

+ [XA0120](xa0120.md): Failed to use SHA1 hash algorithm

### Removed in Xamarin.Android 9.3

+ [XA0114](xa0114.md): Google Play requires that application updates must use a `$(TargetFrameworkVersion)` of v8.0 (API level 26) or above.
