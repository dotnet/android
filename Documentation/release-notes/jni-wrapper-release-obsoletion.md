### Bindings projects

- In .NET 11, _Android.Runtime.JNINativeWrapper.CreateDelegate(Delegate)_ is
  marked with _ObsoleteAttribute_ with _error: true_. Compiling source that
  calls this method produces C# error CS0619. The public class, method signature,
  and implementation remain available for existing compiled bindings.
  Removal is planned for .NET 12.

  Rebuild older bindings with the current .NET for Android binding generator
  and use its generated JNI callbacks. These callbacks use
  _Java.Interop.JniMarshal.SafeInvokeAction_ or _SafeInvokeFunc_ to handle JNI
  transitions and exceptions. Replacing _CreateDelegate_ with a delegate
  constructor alone does not preserve that handling.
