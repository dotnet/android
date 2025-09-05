using System;

using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks;

class LlvmIrHelpers
{
	public static void DeclareDummyFunction (LlvmIrModule module, LlvmIrGlobalVariableReference symref)
	{
		if (symref.Name.IsNullOrEmpty ()) {
			throw new InvalidOperationException ("Internal error: variable reference must have a name");
		}

		// Just a dummy declaration, we don't care about the arguments
		var funcSig = new LlvmIrFunctionSignature (symref.Name!, returnType: typeof(void));
		var _ = module.DeclareExternalFunction (funcSig);
	}
}
