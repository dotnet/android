using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class PreservePinvokesNativeAssemblyGenerator : LlvmIrComposer
{
	sealed class PInvoke
	{
		public readonly LlvmIrFunction NativeFunction;
		public readonly PinvokeScanner.PinvokeEntryInfo Info;

		public PInvoke (LlvmIrModule module, PinvokeScanner.PinvokeEntryInfo pinfo)
		{
			Info = pinfo;

			// All the p/invoke functions use the same dummy signature.  The only thing we care about is
			// a way to reference to the symbol at build time so that we can return pointer to it.  For
			// that all we need is a known name, signature doesn't matter to us.
			var funcSig = new LlvmIrFunctionSignature (name: pinfo.EntryName, returnType: typeof(void));
			NativeFunction = module.DeclareExternalFunction (funcSig);
		}
	}

	sealed class Component
	{
		public readonly string Name;
		public readonly ulong NameHash;
		public readonly List<PInvoke> PInvokes;
		public bool Is64Bit;

		public Component (string name, ulong nameHash, List<PInvoke> pinvokes, bool is64Bit)
		{
			Name = name;
			NameHash = nameHash;
			PInvokes = pinvokes;
			Is64Bit = is64Bit;
		}
	}

	// Maps a component name after ridding it of the `lib` prefix and the extension to a "canonical"
	// name of a library, as used in `[DllImport]` attributes.
	readonly Dictionary<string, string> libraryNameMap = new (StringComparer.Ordinal) {
		{ "xa-java-interop",             "java-interop" },
		{ "mono-android.release-static", String.Empty },
		{ "mono-android.release",        String.Empty },
	};

	readonly NativeCodeGenState state;
	readonly ITaskItem[] monoComponents;

	public PreservePinvokesNativeAssemblyGenerator (TaskLoggingHelper log, NativeCodeGenState codeGenState, ITaskItem[] monoComponents)
		: base (log)
	{
		if (codeGenState.PinvokeInfos == null) {
			throw new InvalidOperationException ($"Internal error: {nameof (codeGenState)} `{nameof (codeGenState.PinvokeInfos)}` property is `null`");
		}

		this.state = codeGenState;
		this.monoComponents = monoComponents;
	}

	protected override void Construct (LlvmIrModule module)
	{
		Log.LogDebugMessage ($"[{state.TargetArch}] Constructing p/invoke preserve code");
		List<PinvokeScanner.PinvokeEntryInfo> pinvokeInfos = state.PinvokeInfos!;
		if (pinvokeInfos.Count == 0) {
			// This is a very unlikely scenario, but we will work just fine.  The module that this generator produces will merely result
			// in an empty (but valid) .ll file and an "empty" object file to link into the shared library.
			return;
		}

		Log.LogDebugMessage ("  Looking for enabled native components");
		var componentNames = new List<string> ();
		var nativeComponents = new NativeRuntimeComponents (monoComponents);
		foreach (NativeRuntimeComponents.Archive archiveItem in nativeComponents.KnownArchives) {
			if (!archiveItem.Include) {
				continue;
			}

			Log.LogDebugMessage ($"    {archiveItem.Name}");
			componentNames.Add (archiveItem.Name);
		}

		if (componentNames.Count == 0) {
			Log.LogDebugMessage ("No native framework components are included in the build, not scanning for p/invoke usage");
			return;
		}

		bool is64Bit = state.TargetArch switch {
			AndroidTargetArch.Arm64  => true,
			AndroidTargetArch.X86_64 => true,
			AndroidTargetArch.Arm    => false,
			AndroidTargetArch.X86    => false,
			_                        => throw new NotSupportedException ($"Architecture {state.TargetArch} is not supported here")
		};

		Log.LogDebugMessage ("  Checking discovered p/invokes against the list of components");
		var preservedPerComponent = new Dictionary<string, Component> (StringComparer.OrdinalIgnoreCase);
		var processedCache = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

		foreach (PinvokeScanner.PinvokeEntryInfo pinfo in pinvokeInfos) {
			Log.LogDebugMessage ($"    p/invoke: {pinfo.EntryName} in {pinfo.LibraryName}");
			string key = $"{pinfo.LibraryName}/${pinfo.EntryName}";
			if (processedCache.Contains (key)) {
				Log.LogDebugMessage ($"      already processed");
				continue;
			}

			processedCache.Add (key);
			if (!MustPreserve (pinfo, componentNames)) {
				Log.LogDebugMessage ("      no need to preserve");
				continue;
			}
			Log.LogDebugMessage ("      must be preserved");

			if (!preservedPerComponent.TryGetValue (pinfo.LibraryName, out Component? component)) {
				component = new Component (
					pinfo.LibraryName,
					MonoAndroidHelper.GetXxHash (pinfo.LibraryName, is64Bit),
					new List<PInvoke> (),
					is64Bit
				);
				preservedPerComponent.Add (pinfo.LibraryName, component);
			}
			component.PInvokes.Add (new PInvoke (module, pinfo));
		}

		Log.LogDebugMessage ("  Components to be preserved:");

		foreach (var kvp in preservedPerComponent) {
			var component = kvp.Value;
			Log.LogDebugMessage ($"    {component.Name} (hash: 0x{component.NameHash:x}; {component.PInvokes.Count} p/invoke(s))");
		}
	}

	// Returns `true` for all p/invokes that we know are part of our set of components, otherwise returns `false`.
	// Returning `false` merely means that the p/invoke isn't in any of BCL or our code and therefore we shouldn't
	// care.  It doesn't mean the p/invoke will be removed in any way.
	bool MustPreserve (PinvokeScanner.PinvokeEntryInfo pinfo, List<string> components)
	{
		if (String.Compare ("xa-internal-api", pinfo.LibraryName, StringComparison.Ordinal) == 0) {
			return true;
		}

		foreach (string component in components) {
			// The most common pattern for the BCL - file name without extension
			string componentName = Path.GetFileNameWithoutExtension (component);
			if (Matches (pinfo.LibraryName, componentName)) {
				return true;
			}

			// If it starts with `lib`, drop the prefix
			if (componentName.StartsWith ("lib", StringComparison.Ordinal)) {
				if (Matches (pinfo.LibraryName, componentName.Substring (3))) {
					return true;
				}
			}

			// Might require mapping of component name to a canonical one
			if (libraryNameMap.TryGetValue (componentName, out string? mappedComponentName) && !String.IsNullOrEmpty (mappedComponentName)) {
				if (Matches (pinfo.LibraryName, mappedComponentName)) {
					return true;
				}
			}

			// Try full file name, as the last resort
			if (Matches (pinfo.LibraryName, Path.GetFileName (component))) {
				return true;
			}
		}

		return false;

		bool Matches (string libraryName, string componentName)
		{
			return String.Compare (libraryName, componentName, StringComparison.Ordinal) == 0;
		}
	}
}
