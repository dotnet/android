#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tasks;

class NativeAotJniInitNativeAssemblyGenerator
{
	readonly List<string> jniOnLoadNames = new ();

	/// <summary>
	/// Whether to write additional descriptive comments into the generated LLVM IR.  Defaults to <c>false</c>.
	/// </summary>
	public bool EmitComments { get; set; }

	public NativeAotJniInitNativeAssemblyGenerator (TaskLoggingHelper log, List<string>? runtimeComponentsJniOnLoadHandlers, List<string>? customJniOnLoadHandlers)
	{
		if (log == null) {
			throw new ArgumentNullException (nameof (log));
		}

		var seenNames = new HashSet<string> (StringComparer.Ordinal);

		// We call BCL/runtime handlers first, to make sure user libraries can rely on them being initialized (just in case)
		CollectHandlers (runtimeComponentsJniOnLoadHandlers);
		CollectHandlers (customJniOnLoadHandlers);

		void CollectHandlers (List<string>? handlers)
		{
			if (handlers == null || handlers.Count == 0) {
				return;
			}

			foreach (string name in handlers) {
				if (seenNames.Add (name)) {
					jniOnLoadNames.Add (name);
				}
			}
		}
	}

	public void Generate (AndroidTargetArch arch, TextWriter output, string fileName)
	{
		using var w = new LlvmIrWriter (output, LlvmIrTarget.Get (arch), EmitComments);
		var strings = new LlvmIrStringPool ();

		w.WriteHeader (fileName);
		w.WriteGlobal ("__jni_on_load_handler_count", LlvmIrWriter.GlobalConstant, "i32", jniOnLoadNames.Count.ToString (), 4);

		var handlers = new List<string> (jniOnLoadNames.Count);
		var names = new List<string> (jniOnLoadNames.Count);
		foreach (string name in jniOnLoadNames) {
			handlers.Add ($"\tptr @{name}");
			names.Add ($"\tptr {strings.GetPointer (name)}");
		}

		string type = $"[{handlers.Count} x ptr]";
		ulong alignment = w.GetPointerArrayAlignment (handlers.Count);
		w.WriteGlobal ("__jni_on_load_handlers", LlvmIrWriter.GlobalConstant, type, w.ArrayValue (handlers, i => $" {i}"), alignment);
		w.WriteGlobal ("__jni_on_load_handler_names", LlvmIrWriter.GlobalConstant, type, w.ArrayValue (names, i => $" {i} ('{jniOnLoadNames [i]}')"), alignment);

		strings.Write (w);

		// All the handlers are declared with the same dummy signature, we only need to take their address
		w.WriteExternalFunctionDeclarations (jniOnLoadNames);
		w.WriteMetadata ();
		output.Flush ();
	}
}
