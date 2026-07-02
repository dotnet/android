using System;
using System.Collections.Generic;

using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Build.Tasks;

class NativeAotJniInitNativeAssemblyGenerator : LlvmIrComposer
{
	readonly List<string>? runtimeComponentsJniOnLoadHandlers;
	readonly List<string>? customJniOnLoadHandlers;

	public NativeAotJniInitNativeAssemblyGenerator (TaskLoggingHelper log, List<string>? runtimeComponentsJniOnLoadHandlers, List<string>? customJniOnLoadHandlers)
		: base (log)
	{
		this.runtimeComponentsJniOnLoadHandlers = runtimeComponentsJniOnLoadHandlers;
		this.customJniOnLoadHandlers = customJniOnLoadHandlers;
	}

	protected override void Construct (LlvmIrModule module)
	{
		var jniOnLoadNames = new List<string> ();
		var seenNames = new HashSet<string> (StringComparer.Ordinal);

		// We call BCL/runtime handlers first, to make sure user libraries can rely on them being initialized (just in case)
		CollectHandlers (runtimeComponentsJniOnLoadHandlers);
		CollectHandlers (customJniOnLoadHandlers);
		JniOnLoadNativeAssemblerHelper.GenerateJniOnLoadHandlerCode (jniOnLoadNames, module);

		DefineNoOpXamarinAppInit (module);

		void CollectHandlers (List<string>? handlers)
		{
			if (handlers == null || handlers.Count == 0) {
				return;
			}

			foreach (string name in handlers) {
				if (seenNames.Contains (name)) {
					continue;
				}
				seenNames.Add (name);
				jniOnLoadNames.Add (name);
			}
		}
	}

	// The managed method `Android.Runtime.JNIEnvInit.Initialize` (used only by MonoVM and CoreCLR) has a
	// `[LibraryImport]` p/invoke to the native `xamarin_app_init` function, which becomes a direct native
	// reference because the `xa-internal-api` "library" is registered as a `DirectPInvoke` for NativeAOT.
	// That symbol is only ever defined by the MonoVM/CoreCLR marshal methods native code, which is never
	// generated for NativeAOT. `Initialize` is unreachable under NativeAOT (which uses
	// `InitializeNativeAotRuntime` instead), but it is force-preserved by the ILLink descriptor
	// (`PreserveLists/Mono.Android.xml`), so the unresolved reference reaches the linker and fails with
	// XA3007 "undefined symbol: xamarin_app_init". Emit an empty definition to satisfy the linker; it is
	// never actually called under NativeAOT.
	static void DefineNoOpXamarinAppInit (LlvmIrModule module)
	{
		var parameters = new List<LlvmIrFunctionParameter> {
			new LlvmIrFunctionParameter (typeof (IntPtr), "env"),
			new LlvmIrFunctionParameter (typeof (IntPtr), "fn"),
		};
		var func = new LlvmIrFunction ("xamarin_app_init", typeof (void), parameters);
		func.Body.Ret (typeof (void));
		module.Add (func);
	}
}
