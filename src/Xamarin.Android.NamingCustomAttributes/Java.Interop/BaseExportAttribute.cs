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
        dynamic_callback_gen ??= Type.GetType ("Java.Interop.DynamicCallbackCodeGenerator, Mono.Android.Export")?.GetMethod ("Create")
            ?? throw new InvalidOperationException ("To use methods marked with ExportAttribute, Mono.Android.Export.dll needs to be referenced in the application");

        return (Delegate?)dynamic_callback_gen.Invoke (null, [method])
            ?? throw new InvalidOperationException (FormattableString.Invariant ($"Unable to create dynamic callback for method '{method.Name}' on type '{method.DeclaringType?.FullName}'"));
    }
}
