using System;
using System.Reflection;

namespace Java.Interop;

#if !JCW_ONLY_TYPE_NAMES
public
#endif  // !JCW_ONLY_TYPE_NAMES
abstract class BaseExportAttribute : Attribute
{
	internal abstract Delegate CreateDynamicCallback (MethodInfo method);

	static MethodInfo? dynamic_callback_gen;

	private protected static Delegate CreateDynamicCallbackCore (MethodInfo method)
	{
		// We're loading the Mono.Android.Export assembly dynamically to avoid problems with circular dependencies.
		// The Type.GetType (...)?.GetMethod (...) call is chained intentionally: the trimmer only recognizes the
		// GetMethod intrinsic when the constant Type value flows directly into it, so routing it through a local breaks dataflow.
		dynamic_callback_gen ??= Type.GetType ("Java.Interop.DynamicCallbackCodeGenerator, Mono.Android.Export")?.GetMethod ("Create")
			?? throw new InvalidOperationException ("To use methods marked with [Export] or [ExportField], Mono.Android.Export.dll must be referenced by the application and contain a matching Java.Interop.DynamicCallbackCodeGenerator.Create method.");

		return (Delegate?)dynamic_callback_gen.Invoke (null, [method])
			?? throw new InvalidOperationException (FormattableString.Invariant ($"Unable to create dynamic callback for method '{method.Name}' on type '{method.DeclaringType?.FullName}'"));
	}
}
