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
		public readonly ulong Hash;

		public PInvoke (LlvmIrModule module, PinvokeScanner.PinvokeEntryInfo pinfo, bool is64Bit)
		{
			Info = pinfo;
			Hash = MonoAndroidHelper.GetXxHash (pinfo.EntryName, is64Bit);

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

		public Component (string name, bool is64Bit)
		{
			Name = name;
			NameHash = MonoAndroidHelper.GetXxHash (name, is64Bit);
			PInvokes = new ();
			Is64Bit = is64Bit;
		}

		public void Add (LlvmIrModule module, PinvokeScanner.PinvokeEntryInfo pinfo)
		{
			PInvokes.Add (new PInvoke (module, pinfo, Is64Bit));
		}

		public void Sort ()
		{
			PInvokes.Sort ((PInvoke a, PInvoke b) => a.Hash.CompareTo (b.Hash));
		}
	}

	sealed class ConstructionState
	{
		public LlvmIrFunction Func;
		public LlvmIrFunctionLabelItem ReturnLabel;
		public LlvmIrFunctionParameter EntryPointHashParam;
		public LlvmIrInstructions.Phi Phi;
		public bool Is64Bit;
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
		var componentNames = new HashSet<string> (StringComparer.Ordinal);
		var componentLoadHandlers = new Dictionary<string, string> (StringComparer.Ordinal);
		var componentPreservedSymbols = new Dictionary<string, HashSet<LlvmIrGlobalVariableReference>> (StringComparer.Ordinal);
		var nativeComponents = new NativeRuntimeComponents (monoComponents);
		foreach (NativeRuntimeComponents.Archive archiveItem in nativeComponents.KnownArchives) {
			if (!archiveItem.Include) {
				continue;
			}

			Log.LogDebugMessage ($"    {archiveItem.Name}");
			componentNames.Add (archiveItem.Name);
			if (!String.IsNullOrEmpty (archiveItem.JniOnLoadName)) {
				componentLoadHandlers.Add (archiveItem.Name, archiveItem.JniOnLoadName);
			}

			if (archiveItem.SymbolsToPreserve == null || archiveItem.SymbolsToPreserve.Count == 0) {
				continue;
			}

			var preservedSymbols = new HashSet<LlvmIrGlobalVariableReference> ();
			foreach (string symbolName in archiveItem.SymbolsToPreserve) {
				preservedSymbols.Add (new LlvmIrGlobalVariableReference (symbolName));
			}
			componentPreservedSymbols.Add (archiveItem.Name, preservedSymbols);
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
		var jniOnLoadNames = new HashSet<string> (StringComparer.Ordinal);
		bool haveLoadHandlers = componentLoadHandlers.Count > 0;
		bool havePreservedSymbols = componentPreservedSymbols.Count > 0;

		var symbolsToExplicitlyPreserve = new HashSet<LlvmIrGlobalVariableReference> ();

		foreach (PinvokeScanner.PinvokeEntryInfo pinfo in pinvokeInfos) {
			Log.LogDebugMessage ($"    p/invoke: {pinfo.EntryName} in {pinfo.LibraryName}");
			string key = $"{pinfo.LibraryName}/${pinfo.EntryName}";
			if (processedCache.Contains (key)) {
				Log.LogDebugMessage ($"      already processed");
				continue;
			}

			processedCache.Add (key);
			(bool preserve, string? componentName) = MustPreserve (pinfo, componentNames);
			if (!preserve) {
				Log.LogDebugMessage ("      no need to preserve");
				continue;
			}
			Log.LogDebugMessage ("      must be preserved");

			if (!String.IsNullOrEmpty (componentName)) {
				if (haveLoadHandlers  && componentLoadHandlers.TryGetValue (componentName, out string jniOnLoadName)) {
					if (jniOnLoadNames.Add (jniOnLoadName)) {
						Log.LogDebugMessage ($"      component '{componentName}' registers a load handler '{jniOnLoadName}'");
					}
				}

				if (havePreservedSymbols && componentPreservedSymbols.TryGetValue (componentName, out HashSet<LlvmIrGlobalVariableReference> preservedSymbols)) {
					foreach (LlvmIrGlobalVariableReference vref in preservedSymbols) {
						DeclareDummyFunction (module, vref);
						symbolsToExplicitlyPreserve.Add (vref);
					}
					componentPreservedSymbols.Remove (componentName);
				}
			}

			if (!preservedPerComponent.TryGetValue (pinfo.LibraryName, out Component? component)) {
				component = new Component (pinfo.LibraryName, is64Bit);
				preservedPerComponent.Add (component.Name, component);
			}
			component.Add (module, pinfo);
		}

		module.AddGlobalVariable ("__jni_on_load_handler_count", (uint)jniOnLoadNames.Count, LlvmIrVariableOptions.GlobalConstant);
		var jniOnLoadPointers = new List<LlvmIrVariableReference> ();
		foreach (string name in jniOnLoadNames) {
			var symref = new LlvmIrGlobalVariableReference (name);
			jniOnLoadPointers.Add (symref);
			DeclareDummyFunction (module, symref);
		}
		module.AddGlobalVariable ("__jni_on_load_handlers", jniOnLoadPointers, LlvmIrVariableOptions.GlobalConstant);
		module.AddGlobalVariable ("__jni_on_load_handler_names", jniOnLoadNames, LlvmIrVariableOptions.GlobalConstant);
		module.AddGlobalVariable ("__explicitly_preserved_symbols", symbolsToExplicitlyPreserve, LlvmIrVariableOptions.GlobalConstant);

		var components = new List<Component> (preservedPerComponent.Values);
		if (is64Bit) {
			AddFindPinvoke<ulong> (module, components, is64Bit);
		} else {
			AddFindPinvoke<uint> (module, components, is64Bit);
		}
	}

	void AddFindPinvoke<T> (LlvmIrModule module, List<Component> components, bool is64Bit) where T: struct
	{
		var hashType = is64Bit ? typeof (ulong) : typeof (uint);
		var parameters = new List<LlvmIrFunctionParameter> {
			new LlvmIrFunctionParameter (hashType, "library_name_hash") {
				NoUndef = true,
			},

			new LlvmIrFunctionParameter (hashType, "entrypoint_hash") {
				NoUndef = true,
			},

			new LlvmIrFunctionParameter (typeof(IntPtr), "known_library") {
				Align = 1, // it's a reference to C++ `bool`
				Dereferenceable = 1,
				IsCplusPlusReference = true,
				NoCapture = true,
				NonNull = true,
				NoUndef = true,
				WriteOnly = true,
			},
		};

		var sig = new LlvmIrFunctionSignature (
			name: "find_pinvoke",
			returnType: typeof(IntPtr),
			parameters: parameters,
			new LlvmIrFunctionSignature.ReturnTypeAttributes {
				NoUndef = true,
			}
		);

		var func = new LlvmIrFunction (sig, MakeFindPinvokeAttributeSet (module)) {
			CallingConvention = LlvmIrCallingConvention.Fastcc,
			Visibility = LlvmIrVisibility.Hidden,
		};
		LlvmIrLocalVariable retval = func.CreateLocalVariable (typeof(IntPtr), "retval");
		var state = new ConstructionState {
			Func = func,
			ReturnLabel = new LlvmIrFunctionLabelItem ("return"),
			EntryPointHashParam = parameters[1],
			Phi = new LlvmIrInstructions.Phi (retval),
			Is64Bit = is64Bit,
		};
		module.Add (state.Func);
		state.Func.Body.Add (new LlvmIrFunctionLabelItem ("entry"));

		var libraryNameSwitchEpilog = new LlvmIrFunctionLabelItem ("libNameSW.epilog");
		var componentSwitch = new LlvmIrInstructions.Switch<T> (parameters[0], libraryNameSwitchEpilog, "sw.libname");

		state.Func.Body.Add (componentSwitch);
		state.Phi.AddNode (libraryNameSwitchEpilog, null);

		components.Sort ((Component a, Component b) => a.NameHash.CompareTo (b.NameHash));
		Log.LogDebugMessage ("  Components to be preserved:");
		uint componentID = 1;

		foreach (Component component in components) {
			Log.LogDebugMessage ($"    {component.Name} (hash: 0x{component.NameHash:x}; {component.PInvokes.Count} p/invoke(s))");

			string comment = $" {component.Name} (p/invoke count: {component.PInvokes.Count})";
			LlvmIrFunctionLabelItem componentLabel = AddSwitchItem<T> (componentSwitch, component.NameHash, is64Bit, comment, null);

			func.Body.Add (componentLabel, comment);
			AddPInvokeSwitch<T> (state, componentLabel, component, componentID++);
		}

		func.Body.Add (libraryNameSwitchEpilog);

		var setKnownLib = new LlvmIrInstructions.Store (false, parameters[2]);
		func.Body.Add (setKnownLib);
		AddReturnBranch (func, state.ReturnLabel);

		func.Body.Add (state.ReturnLabel);
		func.Body.Add (state.Phi);
		func.Body.Add (new LlvmIrInstructions.Ret (typeof (IntPtr), retval));
	}

	void AddPInvokeSwitch<T> (ConstructionState state, LlvmIrFunctionLabelItem componentLabel, Component component, uint id) where T: struct
	{
		var pinvokeSwitchEpilog = new LlvmIrFunctionLabelItem ($"pinvokeSW.epilog.{id}");
		state.Phi.AddNode (pinvokeSwitchEpilog, null);

		var pinvokeSwitch = new LlvmIrInstructions.Switch<T> (state.EntryPointHashParam, pinvokeSwitchEpilog, $"sw.pinvoke.{id}");
		state.Func.Body.Add (pinvokeSwitch);

		component.Sort ();
		bool first = true;
		foreach (PInvoke pi in component.PInvokes) {
			string pinvokeName = pi.NativeFunction.Signature.Name;
			string comment = $" {pinvokeName}";
			LlvmIrFunctionLabelItem pinvokeLabel = AddSwitchItem<T> (pinvokeSwitch, pi.Hash, state.Is64Bit, comment, first ? state.ReturnLabel : null);

			// First item of every component switch block "reuses" the block's label
			if (first) {
				first = false;
			} else {
				state.Func.Body.Add (pinvokeLabel, comment);
				AddReturnBranch (state.Func, state.ReturnLabel);
			}

			state.Phi.AddNode (pinvokeLabel == state.ReturnLabel ? componentLabel : pinvokeLabel, new LlvmIrGlobalVariableReference (pinvokeName));
		}

		state.Func.Body.Add (pinvokeSwitchEpilog);
		AddReturnBranch (state.Func, state.ReturnLabel);
	}

	void AddReturnBranch (LlvmIrFunction func, LlvmIrFunctionLabelItem returnLabel)
	{
		var branch = new LlvmIrInstructions.Br (returnLabel);
		func.Body.Add (branch);
	}

	LlvmIrFunctionLabelItem AddSwitchItem<T> (LlvmIrInstructions.Switch<T> sw, ulong hash, bool is64Bit, string? comment, LlvmIrFunctionLabelItem? label) where T: struct
	{
		if (is64Bit) {
			return sw.Add ((T)(object)hash, dest: label, comment: comment);
		}
		return sw.Add ((T)(object)(uint)hash, dest: label, comment: comment);
	}

	LlvmIrFunctionAttributeSet MakeFindPinvokeAttributeSet (LlvmIrModule module)
	{
		var attrSet = new LlvmIrFunctionAttributeSet {
			new MustprogressFunctionAttribute (),
			new NofreeFunctionAttribute (),
			new NorecurseFunctionAttribute (),
			new NosyncFunctionAttribute (),
			new NounwindFunctionAttribute (),
			new WillreturnFunctionAttribute (),
			new MemoryFunctionAttribute {
				Default = MemoryAttributeAccessKind.Write,
				Argmem = MemoryAttributeAccessKind.None,
				InaccessibleMem = MemoryAttributeAccessKind.None,
			},
			new UwtableFunctionAttribute (),
			new NoTrappingMathFunctionAttribute (true),
		};

		return module.AddAttributeSet (attrSet);
	}

	// Returns `true` for all p/invokes that we know are part of our set of components, otherwise returns `false`.
	// Returning `false` merely means that the p/invoke isn't in any of BCL or our code and therefore we shouldn't
	// care.  It doesn't mean the p/invoke will be removed in any way.
	(bool preserve, string? componentName) MustPreserve (PinvokeScanner.PinvokeEntryInfo pinfo, ICollection<string> components)
	{
		if (String.Compare ("xa-internal-api", pinfo.LibraryName, StringComparison.Ordinal) == 0) {
			return (true, null);
		}

		foreach (string component in components) {
			// The most common pattern for the BCL - file name without extension
			string componentName = Path.GetFileNameWithoutExtension (component);
			if (Matches (pinfo.LibraryName, componentName)) {
				return (true, componentName);
			}

			// If it starts with `lib`, drop the prefix
			if (componentName.StartsWith ("lib", StringComparison.Ordinal)) {
				if (Matches (pinfo.LibraryName, componentName.Substring (3))) {
					return (true, componentName);
				}
			}

			// Might require mapping of component name to a canonical one
			if (libraryNameMap.TryGetValue (componentName, out string? mappedComponentName) && !String.IsNullOrEmpty (mappedComponentName)) {
				if (Matches (pinfo.LibraryName, mappedComponentName)) {
					return (true, componentName);
				}
			}

			// Try full file name, as the last resort
			if (Matches (pinfo.LibraryName, Path.GetFileName (component))) {
				return (true, componentName);
			}
		}

		return (false, null);

		bool Matches (string libraryName, string componentName)
		{
			return String.Compare (libraryName, componentName, StringComparison.Ordinal) == 0;
		}
	}

	static void DeclareDummyFunction (LlvmIrModule module, LlvmIrGlobalVariableReference symref)
	{
		// Just a dummy declaration, we don't care about the arguments
		var funcSig = new LlvmIrFunctionSignature (symref.Name, returnType: typeof(void));
		var _ = module.DeclareExternalFunction (funcSig);
	}
}
