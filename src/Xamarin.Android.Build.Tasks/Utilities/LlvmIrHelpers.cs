using System;
using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks;

static class LlvmIrHelpers
{
	public static void DeclareDummyFunction (LlvmIrModule module, LlvmIrGlobalVariableReference symref)
	{
		string? name = symref.Name;
		if (name == null || name.Length == 0) {
			throw new InvalidOperationException ("Internal error: variable reference must have a name");
		}
		// Only the symbol reference is needed to keep the native entry point.
		module.DeclareExternalFunction (new LlvmIrFunctionSignature (name, returnType: typeof (void)));
	}
}
