### Breaking change: obsolete expression-marshaling APIs removed

.NET for Android 12 removes the remaining runtime expression-marshaling contracts
after retirement of the experimental JavaInterop1 backend. This is an intentional
source and binary compatibility break; deprecated compatibility stubs are not
provided.

The removed APIs are:

- `Java.Interop.JniRuntime.JniMarshalMemberBuilder`, all its members, the runtime
  `MarshalMemberBuilder` property, and the `CreationOptions.MarshalMemberBuilder`
  and `CreationOptions.UseMarshalMemberBuilder` properties.
- `Java.Interop.Expressions.JniValueMarshalerContext` and all its members.
- `JniValueMarshaler.CreateParameterToManagedExpression`,
  `CreateParameterFromManagedExpression`, `CreateReturnValueFromManagedExpression`,
  and the protected expression helpers `ReturnObjectReferenceToJni` and
  `DisposeObjectReference`.
- `JniRuntime.JniValueManager.ActivatePeer`.
- `Android.Graphics.ColorValueMarshaler`. The expression-only marshaler
  attributes on `Android.Graphics.Color` and `Android.Runtime.IJavaObject`
  are also removed, along with the internal `IJavaObjectValueMarshaler`.

Recompile libraries that reference these contracts. Remove obsolete creation
options and `ActivatePeer` overrides from custom runtimes, and remove expression
overrides from custom `JniValueMarshaler` subclasses. Non-expression marshalers
still use `CreateValue`/`CreateGenericValue`, argument-state creation, and
`DestroyArgumentState`/`DestroyGenericArgumentState`; pair argument-state creation
with cleanup in a `finally` block.

Use supported Android binding generation, Java callable wrappers, and `[Export]`
methods for Java-to-managed calls rather than a runtime expression builder.
There is no replacement runtime expression-generation API. Color values still
convert explicitly with `new Color (argb)` and `Color.ToArgb ()`, as supported
generated bindings do.

Generated callbacks and trimmable typemap constructor activation do not call the
removed `ActivatePeer` method. This change does not remove `ConstructPeer`,
`CreatePeer`, `CreateLocalObjectReferenceArgument`, primitive/string/array
conversion, or `JavaObject`/`JavaException` reference ownership.
