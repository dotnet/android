using System;
using System.Collections.Generic;

using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Build.Tasks;

class NativeAotDsoLoadNativeAssemblyGenerator : LlvmIrComposer
{
	readonly List<string>? runtimeComponentsJniOnLoadHandlers;
	readonly List<string>? customJniOnLoadHandlers;

	public NativeAotDsoLoadNativeAssemblyGenerator (List<string>? runtimeComponentsJniOnLoadHandlers, List<string>? customJniOnLoadHandlers, TaskLoggingHelper log)
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
}
