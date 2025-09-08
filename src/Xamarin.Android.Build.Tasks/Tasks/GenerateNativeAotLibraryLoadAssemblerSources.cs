using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Build.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

public class GenerateNativeAotLibraryLoadAssemblerSources : AndroidTask
{
	static readonly HashSet<string> KnownLibraryExtensions = new (StringComparer.OrdinalIgnoreCase) {
		".so",
		".dll",
		".dylib",
	};

	static readonly HashSet<string> LibraryNamesToIgnore = new (StringComparer.OrdinalIgnoreCase) {
		"*",
		"android",
		"c",
		"libandroid",
		"libandroid.so",
		"libc",
		"libc.dylib",
		"libc.so",
		"liblog",
		"liblog.so",
		"log",
		"xa-internal-api",
	};

	public override string TaskPrefix => "GNALLAS";

	[Required]
	public ITaskItem[] ResolvedAssemblies { get; set; } = [];

	[Required]
	public string SourcesOutputDirectory { get; set; } = "";

	// Names of JNI initialization functions in 3rd party libraries. The
	// functions are REQUIRED to use the `JNI_OnLoad(JNIEnv*, void* reserved)` signature.
	// TODO: document it in `Documentation/`
	public ITaskItem[] CustomJniInitFunctions { get; set; } = [];

	public override bool RunTask ()
	{
		if (ResolvedAssemblies.Length == 0) {
			return true;
		}

		// We run in the inner build, there's going to be just a single RID
		string rid = MonoAndroidHelper.GetAssemblyRid (ResolvedAssemblies[0]);
		AndroidTargetArch targetArch = MonoAndroidHelper.RidToArch (rid);

		var assemblies = new Dictionary<string, ITaskItem> (StringComparer.OrdinalIgnoreCase);
		foreach (ITaskItem item in ResolvedAssemblies) {
			string name = MonoAndroidHelper.GetAssemblyNameWithCulture (item);
			if (assemblies.ContainsKey (name)) {
				continue;
			}

			assemblies.Add (name, item);
		}

		XAAssemblyResolver resolver = MonoAndroidHelper.MakeResolver (Log, useMarshalMethods: false, targetArch, assemblies, loadDebugSymbols: false);
		var pinvokeScanner = new PinvokeScanner (Log, debugLogging: false);
		List<PinvokeScanner.PinvokeEntryInfo> pinfos = pinvokeScanner.Scan (targetArch, resolver, ResolvedAssemblies);

		var seen = new HashSet<string> (LibraryNamesToIgnore, StringComparer.OrdinalIgnoreCase);
		var pinvokeLibraries = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		foreach (PinvokeScanner.PinvokeEntryInfo pinfo in pinfos) {
			if (seen.Contains (pinfo.LibraryName)) {
				continue;
			}

			seen.Add (pinfo.LibraryName);

			// All the BCL libraries follow the standard naming pattern of `lib<NAME>`, make sure that's what we have
			pinvokeLibraries.Add (MakeCanonicalLibraryName (pinfo.LibraryName));
		}

		// Take library names, match against NativeRuntimeComponents to see whether a
		// component has an init function associated with it.
		var bclComponents = new NativeRuntimeComponents (monoComponents: null);
		var bclInitFunctions = new List<string> ();

		seen = new HashSet<string> (StringComparer.Ordinal);
		foreach (string lib in pinvokeLibraries) {
			foreach (NativeRuntimeComponents.Archive archive in bclComponents.KnownArchives) {
				if (lib != archive.Name) {
					continue;
				}

				if (String.IsNullOrEmpty (archive.JniOnLoadName)) {
					continue;
				}

				if (seen.Contains (archive.JniOnLoadName!)) {
					continue;
				}

				seen.Add (archive.JniOnLoadName!);
				bclInitFunctions.Add (archive.JniOnLoadName!);
			}
		}

		if (bclInitFunctions.Count > 0) {
			Log.LogDebugMessage ("Found BCL JNI init functions to call on application init:");
			foreach (string func in bclInitFunctions) {
				Log.LogDebugMessage ($"  {func}");
			}
		}

		List<string>? customInitFunctions = null;
		if (CustomJniInitFunctions != null && CustomJniInitFunctions.Length > 0) {
			customInitFunctions = new List<string> ();
			seen.Clear ();
			Log.LogDebugMessage ("Custom JNI init functions to call on application init:");
			foreach (ITaskItem func in CustomJniInitFunctions) {
				string name = func.ItemSpec;
				if (seen.Contains (name)) {
					continue;
				}
				seen.Add (name);
				customInitFunctions.Add (name);
				Log.LogDebugMessage ($"  {name}");
			}
		}

		var jniInitFuncsLlFilePath = Path.Combine (SourcesOutputDirectory, $"jni_init_funcs.{MonoAndroidHelper.RidToAbi (rid)}.ll");
		var generator = new NativeAotDsoLoadNativeAssemblyGenerator (Log, bclInitFunctions, customInitFunctions);
		LLVMIR.LlvmIrModule jniInitFuncsModule = generator.Construct ();
		using var jniInitFuncsWriter = MemoryStreamPool.Shared.CreateStreamWriter ();
		bool fileFullyWritten = false;
		try {
			generator.Generate (jniInitFuncsModule, targetArch, jniInitFuncsWriter, jniInitFuncsLlFilePath!);
			jniInitFuncsWriter.Flush ();
			Files.CopyIfStreamChanged (jniInitFuncsWriter.BaseStream, jniInitFuncsLlFilePath!);
			fileFullyWritten = true;
		} finally {
			// Log partial contents for debugging if generation failed
			if (!fileFullyWritten) {
				MonoAndroidHelper.LogTextStreamContents (Log, $"Partial contents of file '{jniInitFuncsLlFilePath}'", jniInitFuncsWriter.BaseStream);
			}
		}
		return !Log.HasLoggedErrors;
	}

	static string MakeCanonicalLibraryName (string libName)
	{
		string? ext = Path.GetExtension (libName);
		if (!String.IsNullOrEmpty (ext) && KnownLibraryExtensions.Contains (ext)) {
			libName = Path.GetFileNameWithoutExtension (libName);
		}

		if (!libName.StartsWith ("lib", StringComparison.OrdinalIgnoreCase)) {
			libName = $"lib{libName}";
		}

		// We will be matching against list of static archives, add the correct extension
		return $"{libName}.a";
	}
}
