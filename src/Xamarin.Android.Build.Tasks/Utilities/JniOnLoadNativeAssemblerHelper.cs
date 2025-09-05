using System.Collections.Generic;

using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks;

class JniOnLoadNativeAssemblerHelper
{
	public static void GenerateJniOnLoadHandlerCode (ICollection<string> jniOnLoadNames, LlvmIrModule module)
	{
		module.AddGlobalVariable ("__jni_on_load_handler_count", (uint)jniOnLoadNames.Count, LlvmIrVariableOptions.GlobalConstant);
		var jniOnLoadPointers = new List<LlvmIrVariableReference> ();
		foreach (string name in jniOnLoadNames) {
			var symref = new LlvmIrGlobalVariableReference (name);
			jniOnLoadPointers.Add (symref);
			LlvmIrHelpers.DeclareDummyFunction (module, symref);
		}
		module.AddGlobalVariable ("__jni_on_load_handlers", jniOnLoadPointers, LlvmIrVariableOptions.GlobalConstant);
		module.AddGlobalVariable ("__jni_on_load_handler_names", jniOnLoadNames, LlvmIrVariableOptions.GlobalConstant);
	}
}
