// Copyright 2012, Xamarin Inc. All rights reserved,

using System;
using System.Collections.Generic;
using System.Text;

using Mono.Cecil.Cil;

namespace Xamarin.Android {

	// Error allocation (not all used; values from MonoTouch)
	//
	// FIXME: THOSE CODE COMMENTS ARE VERY INACCURATE. Any MT-specific numbers should be removed.
	// XA0xxx	mtouch itself, e.g. parameters, environment (e.g. missing tools)
	//					XA0000	Unexpected error - Please fill a bug report at http://bugzilla.xamarin.com
	//					XA0001  Invalid or unsupported $(TargetFrameworkVersion) value of '{0}'.
	//					XA0002	Could not parse the environment variable '{0}'.
	//					XA0003	Application name '{0}.exe' conflicts with an SDK or product assembly (.dll) name.
	//					XA0004	New refcounting logic requires sgen to be enabled too
	//					XA0005	The output directory '{0}' does not exist
	//					XA0006	There is no devel platform at '{0}', use --platform=PLAT to specify the SDK
	//					XA0007	The root assembly '{0}' does not exist
	//					XA0008	You should provide one root assembly only
	//					XA0009	Error while loading assemblies: {0}
	//					XA0010	Could not parse the command line arguments: {0}
	//					XA0011	{0} was built against a more recent runtime ({1}) than MonoTouch supports
	//					XA0012	Incomplete data is provided to complete `{0}`.
	//					XA0013	Profiling support requires sgen to be enabled too
	//					XA0014	iOS {0} does not support building applications targeting ARMv6
	//					XA0020	Could not launch mandroid daemon.
	
	//					XA0100 EmbeddedNativeLibrary '{0}' is invalid in Android Application project. Please use AndroidNativeLibrary instead.
	//					XA0101 @(Content) build action is not supported
	//					XA0102 Lint Warning
	//					XA0103 Lint Error
	//					XA0104 Invalid Sequence Point mode
	//					XA0105 The TargetFrameworkVersion for {0} (v{1}) is greater than the TargetFrameworkVersion for your project (v{2}). You need to increase the TargetFrameworkVersion for your project.
	
	// XA1xxx	file copy / symlinks (project related)
	//			XA10xx	installer.cs / mtouch.cs
	//					XA1001	Could not find an application at the specified directory
	//					XA1002	Could not create symlinks, files were copied
	//					XA1003	Could not kill the application '{0}'. You may have to kill the application manually.
	//					XA1004	Could not get the list of installed applications.
	//					XA1005	Could not kill the application '{0}' on the device '{1}': {2}. You may have to kill the application manually.
	//					XA1006	Could not install the application '{0}' on the device '{1}': {2}.
	//					XA1007	Failed to launch the application '{0}' on the device '{1}': {2}. You can still launch the application manually by tapping on it.
	//					XA1008	Failed to launch the simulator: {0}
	//					XA1009	Could not copy the assembly '{0}' to '{1}': {2}
	//					XA1010	Could not load the assembly '{0}': {1}
	//					XA1011	Could not add missing resource file: '{0}'
	//			XA11xx	DebugService.cs
	//					XA1101	Could not start app
	//					XA1102	Could not attach to the app (to kill it): {0}
	//					XA1103	Could not detach
	//					XA1104	Failed to send packet: {0}
	//					XA1105	Unexpected response type
	//					XA1106	Could not get list of applications on the device: Request timed out.
	//					XA1107	Application failed to launch
	//			XA12xx	simcontroller.cs
	//					XA1201	Could not load the simulator: {0}
	//			XA13xx	[LinkWith]
	//					XA1301  Native library `{0}` ({1}) was ignored since it does not match the current build architecture(s) ({2})
	// XA2xxx	Linker
	//			XA20xx	Linker (general) errors
	//					XA2001	Could not link assemblies
	//					XA2002	Can not resolve reference: {0}
	//					XA2003	Option '{0}' will be ignored since linking is disabled
	//					XA2004	Extra linker definitions file '{0}' could not be located.
	//					XA2005	Definitions from '{0}' could not be parsed.
	//					XA2006  Reference to metadata item '{0}' (defined in '{1}') from '{2}' could not be resolved.
	// XA3xxx	AOT
	//			XA30xx	AOT (general) errors
	//					XA3001	Could not AOT the assembly '{0}'
	//					XA3002	AOT restriction: Method '{0}' must be static since it is decorated with [MonoPInvokeCallback]. See http://ios.xamarin.com/Documentation/Limitations#Reverse_Callbacks # this error message comes from the AOT compiler
	//					XA3003	Conflicting --debug and --llvm options. Soft-debugging is disabled.
	//					XA3004	Incompatible AOT configuration: '{0}'.
	// XA4xxx	code generation
	// 			XA40xx	main.m
	//					XA4001	The main template could not be expansed to `{0}`.
	//			XA41xx	registrar.m
	//					XA4101	The registrar cannot build a signature for type `{0}`.
	//					XA4102	The registrar found an invalid type `{0}` in signature for method `{2}`. Use `{1}` instead.
	//					XA4103	The registrar found an invalid type `{0}` in signature for method `{2}`: The type implements INativeObject, but does not have a constructor that takes two (IntPtr, bool) arguments
	//					XA4104	The registrar cannot marshal the return value for type `{0}` in signature for method `{1}`.
	//					XA4105	The registrar cannot marshal the parameter of type `{0}` in signature for method `{1}`.
	//					XA4106	The registrar cannot marshal the return value for structure `{0}` in signature for method `{1}`.
	//					XA4107	The registrar cannot marshal the parameter of type `{0}` in signature for method `{1}`.
	//					XA4108	The registrar cannot get the ObjectiveC type for managed type `{0}`."
	//					XA4109	Failed to compile the generated registrar code. Please file a bug report at http://bugzilla.xamarin.com
	//					XA4110	The registrar cannot marshal the out parameter of type `{0}` in signature for method `{1}`.
	//					XA4111	The registrar cannot build a signature for type `{0}' in method `{1}`.
	//			XA42xx	ACW generation
	//					XA4200	Can only generate ACW's for `claas` types.
	//					XA4201	Unable to determine JNI name for type {0}.
	//					XA4203	The specified type name must be fully qualified.
	//					XA4204	Unable to resolve interface type '{0}'. Are you missing an assembly reference?
	//					XA4205	[ExportField] can only be used on methods with 0 parameters.
	//					XA4206	[Export] cannot be used on a generic type.
	//					XA4207	[ExportField] cannot be used on a generic type.
	//					XA4208	[Java.Interop.ExportFieldAttribute] cannot be used on a method returning void.
	//					XA4209 Failed to create JavaTypeInfo for class: {0} due to {1}
	//					XA4210 "You need to add a reference to Mono.Android.Export.dll when you use ExportAttribute or ExportFieldAttribute."
	//					XA4211  AndroidManifest.xml //uses-sdk/@android:targetSdkVersion '{0}' is less than $(TargetFrameworkVersion) '{1}'. Using API-{1} for ACW compilation.
	// XA5xxx	GCC and toolchain
	//			XA32xx	.apk generation
	//					XA4300  Unsupported $(AndroidSupportedAbis) value '{0}'; ignoring.
	//					XA4301  Apk already contains the item {0}; ignoring.
	//			XA51xx	compilation
	//					XA5101	Missing '{0}' compiler. Please install Android NDK.
	//					XA5102	Conversion from assembly to native code failed. Please file a bug report at http://bugzilla.xamarin.com
	//					XA5103	Failed to compile the file '{0}'. Please file a bug report at http://bugzilla.xamarin.com
	//			XA52xx	linking
	//					XA5201	Native linking failed. Please review user flags provided to gcc: {0}
	//					XA5202	Native linking failed. Please review the build log.
	//					XA5203	Failed to generate the debug symbols (dSYM directory). Please review the build log.
	//					XA5204	Failed to strip the final binary. Please review the build log.
	//			XA52xx	other tools
	//					XA5205	Missing 'aapt' tool. Please install the Android SDK Build-tools package.
	//					XA5206	{0}. Android resource directory {1} doesn't exist.
	//					XA5207  {0}. Java library file {1} doesn't exist.
	//					XA5208  Download failed. Please download {0} and put it to the {1} directory.
	//					XA5209  Unzipping failed. Please download {0} and extract it to the {1} directory.
	//					XA5210  {0}. Native library file {1} doesn't exist.
	//					XA5211 Embedded wear app package name differs from handheld app package name ({0} != {1}).
	//					XA5212 The Minimum Sdk Version ({0}) in AndroidManifest is invalid.
	//					XA5213 Java.Lang.OutOfMemory Excption. Consider increasing the value of $(JavaMaximumHeapSize).
	//					XA5214	Duplicate resource file.
	//					XA5215	Duplicate "values" Resource found
	//					XA5216	Duplicate Resource found for
	//			XA53xx	linking
	//					XA5303	Native linking warning: {0}
	//			XA53xx	other tools
	//					XA5300  Andorid SDK not found or not fully installed.
	//					XA5301	Missing 'strip' tool. Please install Xcode 'Command-Line Tools' component
	//					XA5302	Missing 'dsymutil' tool. Please install Xcode 'Command-Line Tools' component
	// XA6xxx	mtouch internal tools
	//			XA600x	Stripper
	//					XA6001	Running version of Cecil doesn't support assembly stripping
	//					XA6002	Could not strip assembly `{0}`.
	//					XA6003  [UnauthorizedAccessException message]
	// XA7xxx	reserved
	// XA8xxx	reserved
	// XA9xxx	Licensing
	//					--- these are listed in activation/src/utils/activation.cs ---
	//

	class XamarinAndroidException : Exception {
		
		public XamarinAndroidException (int code, string message, params object[] args)
			: this (code, null, message, args)
		{
		}

		// http://blogs.msdn.com/b/msbuild/archive/2006/11/03/msbuild-visual-studio-aware-error-messages-and-message-formats.aspx
		static string GetMessage (int code, string message, object[] args)
		{
			var m = new StringBuilder ();
			m.Append ("error ");
			m.AppendFormat ("XA{0:0000}", code);
			m.Append (": ");
			m.AppendFormat (message, args);
			return m.ToString ();
		}

		public XamarinAndroidException (int code, Exception innerException, string message, params object[] args)
			: base (GetMessage (code, message, args), innerException)
		{
			Code = code;
			MessageWithoutCode  = string.Format (message, args);
		}

		public string MessageWithoutCode {get; private set;}

		public int Code { get; private set; }

		public SequencePoint Location { get; set; }

		public string SourceFile {
			get { return Location == null ? null : Location.Document.Url; }
		}

		public int SourceLine {
			get { return Location == null ? 0 : Location.StartLine; }
		}
	}

	static class Diagnostic {
		public static void Error (int code, SequencePoint location, string message, params object[] args)
		{
			throw new XamarinAndroidException (code, message, args) {
				Location = location,
			};
		}


		public static void Error (int code, string message, params object[] args)
		{
			throw new XamarinAndroidException (code, message, args);
		}

		public static void Error (int code, Exception innerException, string message, params object[] args)
		{
			throw new XamarinAndroidException (code, innerException, message, args);
		}

		public static void Warning (int code, string message, params object[] args)
		{
			Console.Error.Write ("mandroid: warning XA{0:0000}: ", code);
			Console.Error.WriteLine (message, args);
		}

		public static void WriteTo (System.IO.TextWriter destination, Exception message, bool verbose = false)
		{
			var cfe = message as MonoDroid.Utils.CommandFailedException;
			if (cfe != null) {
				// We don't want this to end up in the VS error pane
				if (verbose)
					destination.WriteLine (cfe.ToString ());

				// We *do* want this to show in VS
				destination.WriteLine (cfe.VSFormattedErrorLog);
				return;
			}

			var xae = message as XamarinAndroidException;
			if (xae != null) {
				destination.WriteLine ("monodroid: {0}", xae.Message);
				if (verbose && xae.Code < 9000)
					destination.WriteLine ("monodroid: {0}", xae.ToString ());
				return;
			}

			destination.WriteLine ("monodroid: error XA0000: Unexpected error - Please file a bug report at http://bugzilla.xamarin.com. Reason: {0}",
					verbose ? message.Message : message.ToString ());
		}
	}
}

