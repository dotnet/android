# `[Export]` / `[ExportField]` — Legacy LLVM-IR Typemap vs Trimmable Typemap

Comparison of how `[Export]` and `[ExportField]` are wired in the **legacy**
codepath (used with `_AndroidTypeMapImplementation=llvm-ir` or `=managed`,
backed by `Mono.Android.Export.dll`) versus the new **trimmable typemap**
codepath (`_AndroidTypeMapImplementation=trimmable`, backed by
`Microsoft.Android.Sdk.TrimmableTypeMap` build-time codegen).

The goal is to capture the contract that the trimmable typemap is preserving,
identify behavioural differences, and inventory the unit / device tests that
cover (or fail to cover) each aspect.

> **Scope**: this document covers `[Export]` on **methods**, `[ExportField]`,
> and `[ExportParameter]`. It does **not** cover registered (non-`[Export]`)
> JCW methods or `[Register]` constructors except where their codegen overlaps
> with `[Export]`.

---

## 1. High-level architecture

| Aspect | Legacy (`Mono.Android.Export`) | Trimmable typemap |
| --- | --- | --- |
| When the JNI thunk is created | **At runtime**, the first time the type is registered, via `System.Reflection.Emit` (`DynamicMethod`) | **At build time**, as IL emitted into a generated assembly via `System.Reflection.Metadata` |
| Trim-safety | **Not trim-safe** — gated by `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` (`MonoAndroidExport.DynamicFeatures`) | Trim-safe — generated assembly is marked `IsTrimmable=True` |
| Marshalling AST | `Mono.CodeGeneration.CodeMethodCall` AST translated to IL by `Mono.CodeGeneration` | Direct ECMA-335 IL via `System.Reflection.Metadata.Ecma335.InstructionEncoder` |
| Reflection surface required | `Type.GetMethod`, `MethodBase.Invoke`, dynamic `Delegate.CreateDelegate` from JNI registration string | None at runtime — registration uses `[UnmanagedCallersOnly]` function pointers |
| Entry point at registration | `AndroidRuntime.RegisterNativeMembers` sees `__export__` connector → `CreateDynamicCallback (MethodInfo)` → loads `Mono.Android.Export.dll` reflectively → `DynamicCallbackCodeGenerator.Create (MethodInfo)` → returns `Delegate` | UCO wrapper is already a static `[UnmanagedCallersOnly]` method on the generated typemap assembly; direct function-pointer registration. `__export__` connector is unused at runtime. |
| Assembly load on first `[Export]` | `Assembly.Load ("Mono.Android.Export")` — application **must reference** Mono.Android.Export.dll or registration throws `InvalidOperationException` | No additional assembly load |
| Delegate GC pinning | Manual: `prevent_delegate_gc` `List<Delegate>` (otherwise GC collects callback between registration and first call on CoreCLR) | Not needed — UCOs are static methods |
| Per-callback delegate type | Cached/deduped by signature key (`EncodeMethodSignature`) in a single SRE `ModuleBuilder` named `__callback_factory__` | No delegate types — UCOs use `IntPtr` JNI ABI directly |

**Source of truth**:
- Legacy: `src/Mono.Android.Export/CallbackCode.cs` + `src/Mono.Android/Android.Runtime/AndroidRuntime.cs::CreateDynamicCallback` (line 467) + `RegisterNativeMembers` (line 571, the `__export__` branch at line 612).
- Trimmable: `src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/ExportMethodDispatchEmitter.cs` + `ExportMethodDispatchEmitterContext.cs`.

---

## 2. Side-by-side per-symbol-kind feature matrix

The legacy `DynamicInvokeTypeInfo.GetKind (Type)` (CallbackCode.cs:301) classifies every
parameter / return type into one of 11 `SymbolKind` values and dispatches per-kind in
`FromNative` (line 331), `ToNative` (line 421), `GetCallbackPrep` (line 173), and
`GetCallbackCleanup` (line 218). The trimmable side has `LoadManagedArgument`
(ExportMethodDispatchEmitter.cs:225) and `ConvertManagedReturnValue` (line 257).

| `SymbolKind` (legacy) | Detected by | Legacy `FromNative` (JNI → managed) | Legacy `ToNative` (managed → JNI) | Trimmable equivalent | Test coverage |
| --- | --- | --- | --- | --- | --- |
| `Array` | `type.IsArray` | `JNIEnv.GetArray<T> (jniHandle, DoNotTransfer, typeof(T[]))` then cast to `T[]` | `JNIEnv.NewArray (managedArr)` (with null-fold to `IntPtr.Zero`) | `LoadManagedArgument` → `JniEnvGetArrayRef` + castclass; `EmitManagedArrayReturn` (line 384) does the null-fold and `JniEnvNewArrayRef` | ✅ unit: `TypeMapAssemblyGeneratorTests.Generate_UcoMethod_*ArrayParam*`, `*ArrayReturn*`. Legacy: implicit only via `MonoAndroidExportTest`. |
| `Array` (in/out copy-back) | same | `GetCallbackCleanup` emits `JNIEnv.CopyArray (managedArr, jniHandle)` for non-immutable element types (string copy-back is suppressed) | n/a (input only) | `EmitManagedArrayCopyBacks` (line 191) does the same per-element-kind copy-back via `JniEnvCopyArrayRef` | ✅ unit: trimmable side has `Generate_UcoMethod_ArrayParam_EmitsCopyBack`. **Gap**: no device test verifies array mutations propagate back to Java in either codepath. |
| `CharSequence` | `type == ICharSequence` | `Java.Lang.Object.GetObject<ICharSequence> (h, DoNotTransfer)` | `CharSequence.ToLocalJniHandle (cs)` | ✅ **Landed in this PR** — return path now dispatches through `Android.Runtime.CharSequence.ToLocalJniHandle (ICharSequence)` (commit `86e94d777`). Scanner emits `Ljava/lang/CharSequence;`. Input path still uses the generic `EmitManagedObjectArgument` (acceptable: legacy did the same with `Java.Lang.Object.GetObject<ICharSequence>`). | ✅ unit: `Scan_ExportMethod_CharSequenceMapsToCanonicalJavaType`. ❌ no device test (blocked by JCW emitter — see §7). |
| `Class` (concrete `Java.Lang.Object` subclass) | `Type.GetTypeCode == Object`, not interface, not generic | `Java.Lang.Object.GetObject<T> (h, DoNotTransfer)` | `JNIEnv.ToLocalJniHandle (obj)` | `EmitManagedObjectArgument` (line 366): `Java.Lang.Object.GetObject (h, DoNotTransfer, typeof(T))` + castclass; return: castclass `IJavaObject` + `JniEnvToLocalJniHandleRef` | ✅ unit (UCO ctor and method object-ref tests); ✅ device: `CreateTypeWithExportedMethods` exercises self-typed instance via JNI |
| `Collection` (`IList` / `IDictionary` / `ICollection` exactly) | reference-equality on those 3 types | `FromNative` falls through → throws `InvalidOperationException` (no case!) | `type.GetMethod("ToLocalJniHandle")` reflective dispatch (depends on `JavaList`/`JavaDictionary`/`JavaCollection` siblings) | ✅ **Return path landed in this PR** (commit `86e94d777`) — strongly-typed calls to `JavaList.ToLocalJniHandle (IList)` / `JavaDictionary.ToLocalJniHandle (IDictionary)` / `JavaCollection.ToLocalJniHandle (ICollection)`; scanner emits canonical `Ljava/util/{List,Map,Collection};`. Input path: trimmable falls through to `EmitManagedObjectArgument` (legacy threw `InvalidOperationException`; trimmable's wrapper is best-effort and matches the JavaList wrapping behavior on input). | ✅ unit: `Scan_ExportMethod_NonGenericCollectionsMapToCanonicalJavaTypes`. ❌ no device test (blocked by JCW emitter — see §7). |
| `Enum` | `type.IsEnum` | `(EnumType) jniInt` (cast) | `(int) enumValue` (cast) | ✅ **Landed in this PR** (commit `634af359d`) — scanner emits the underlying primitive descriptor (`I` / `B` / `S` / `J`); `TypeRefData.IsEnum` flag flows to the emitter, which encodes the type as `ELEMENT_TYPE_VALUETYPE` so callback signatures resolve at runtime. | ✅ unit: `Scan_ExportMethod_EnumParametersUseUnderlyingPrimitiveJniDescriptor` (3 cases) + `Scan_ExportMethod_EnumParametersFlagTypeRefAsEnum`. ❌ no device test (blocked by JCW emitter — see §7). |
| `SimpleFormat` (primitive) | `Type.GetTypeCode != Object` and not enum | pass-through | pass-through | `TryEmitPrimitiveManagedArgument` (line 334) for all `System.{Boolean, Byte, SByte, Char, Int16, UInt16, Int32, UInt32, Int64, UInt64, Single, Double, IntPtr}`. **`Boolean` is converted from JNI byte (0/1) to managed bool via `ldc.i4.0; cgt.un`**; legacy passes JNI `bool` as-is. | ✅ unit (`Generate_UcoCtor_LoadsPrimitiveParam_*`). ❌ device: no test exercises a primitive-arg `[Export]` method through JNI from Java |
| `GenericTypeParameter` (`T` parameter on a generic method) | `type.IsGenericParameter` | `((T)) Java.Lang.Object.GetObject<T> (h, DoNotTransfer)` | `JNIEnv.ToLocalJniHandle (val)` | **❌ Not handled** — `LoadManagedArgument`'s `ThrowIfUnsupportedManagedType` rejects names containing `<` (line 301), but `T` would actually look like a real parameter name in the type-ref. Most likely **build-time `NotSupportedException`** for any open generic. | ❌ no test on either side |
| `Interface` (any other Java interface) | `type.IsInterface`, after CharSequence/Collection | `Java.Lang.Object.GetObject<I> (h, DoNotTransfer)` | `JNIEnv.ToLocalJniHandle (obj)` | Same as `Class` — generic `EmitManagedObjectArgument` / `JniEnvToLocalJniHandleRef` | ⚠️ partial unit (treated as object peer, no interface-specific test) |
| `Stream` (`System.IO.Stream`) | `type == Stream` | `[ExportParameter]` required: `InputStreamInvoker.FromJniHandle` / `OutputStreamInvoker.FromJniHandle`; otherwise `NotSupportedException` at runtime | `[ExportParameter]` required: `InputStreamAdapter.ToLocalJniHandle` / `OutputStreamAdapter.ToLocalJniHandle`; otherwise `NotSupportedException` at runtime | `TryEmitExportParameterArgument` / `TryEmitExportParameterReturn` cover the same 4 `ExportParameterKindInfo` cases. **Difference**: trimmable scanner produces **default** JNI descriptor based on `ExportParameterKindInfo` (`Ljava/io/InputStream;` etc.) at build time; an unspecified kind on a `Stream` parameter would silently produce `Ljava/lang/Object;` rather than a clean error. | ❌ no test on either side |
| `String` (`System.String`) | `type == string` | `JNIEnv.GetString (h, DoNotTransfer)` | `JNIEnv.NewString (s)` | `TryEmitPrimitiveManagedArgument` `case "System.String"` → `JniEnvGetStringRef`; return → `JniEnvNewStringRef` | ✅ unit (`*StringParam*`); ❌ no device-level Export test with a string param |
| `XmlReader` (`System.Xml.XmlReader`) | `type == XmlReader` | `[ExportParameter]` required: `XmlPullParserReader.FromJniHandle` / `XmlResourceParserReader.FromJniHandle`; else `NotSupportedException` | `XmlReaderPullParser.ToLocalJniHandle` / `XmlReaderResourceParser.ToLocalJniHandle` | `TryEmitExportParameterArgument` / `TryEmitExportParameterReturn` for `XmlPullParser` / `XmlResourceParser`. Same default-JNI-descriptor caveat as `Stream`. | ❌ no test on either side |

### Quirks worth flagging

- **Open generic type definition** (legacy `CallbackCode.cs:323`) throws
  `NotSupportedException ("Dynamic method generation is not supported for
  generic type definition")`. Trimmable rejects strings containing `<` in
  `ThrowIfUnsupportedManagedType` — the rejection is broader / blunter
  but covers the same intent.
- **By-ref / pointer parameters**: legacy has no specific handling — would
  fall through `GetKind` and likely crash at IL emit. Trimmable explicitly
  rejects with `NotSupportedException` (CallbackCode.cs vs
  ExportMethodDispatchEmitter.cs:297).
- **`Java.Lang.Object` `__this` for instance methods**: legacy passes `__this`
  via the **2-arg** overload `Java.Lang.Object.GetObject<T> (jnienv, native_this,
  DoNotTransfer)` (CallbackCode.cs:539, `object_getobject_with_handle`).
  Trimmable uses the 3-arg `GetObject (h, JniHandleOwnership, Type)` overload
  uniformly for both `__this` and reference parameters
  (ExportMethodDispatchEmitter.cs:155-162).

---

## 3. Method registration / invocation flow

```
                      Java side calls native method
                                  │
                                  ▼
   ┌────────────────────────────────────────────────────────┐
   │  AndroidRuntime.RegisterNativeMembers (jniType, type,  │
   │     "name:sig:__export__\n…")  — only legacy path      │
   └────────────────────────────────────────────────────────┘
                  │ (legacy)                       │ (trimmable)
                  ▼                                ▼
   CreateDynamicCallback(MethodInfo)       Generated UCO at build time;
   → DynamicCallbackCodeGenerator.Create   already registered via
   → SRE DynamicMethod IL                  [UnmanagedCallersOnly] fnptr
                  │                                │
                  ▼                                ▼
   JniEnvironment.Types.RegisterNatives           (no runtime delegate)
```

| Step | Legacy | Trimmable |
| --- | --- | --- |
| JCW emission | `CallableWrapperGenerator` emits per-`[Export]` method line: `JniName:JniSig:__export__`. The `__export__` connector tells the runtime "use Mono.Android.Export". | Same JCW emission (Java side is identical). The `__export__` connector lives in the JCW's `__md_methods` string but the trimmable runtime doesn't follow that path — registration is wired via the typemap's UCO fnptr, not via the connector string. |
| Build dependency | App must reference `Mono.Android.Export.dll`; otherwise fail at runtime | Generated typemap assembly references core JNI types only; `Mono.Android.Export.dll` is **not required** |
| Throws (`[Export(Throws = …)]`) | Method called normally; uncaught managed exceptions propagate via `JniEnvironment.Runtime.RaisePendingException` | ✅ **Landed in this PR**: UCO body now emits `BeginMarshalMethod` / `try` / `catch` (route via `JniRuntime.OnUserUnhandledException`) / `finally (EndMarshalMethod)` — see `ExportMethodDispatchEmitter.EmitWrappedExportMethodDispatch` (mirrors trimmable UCO ctor wrapper). **Behavioural difference vs legacy**: `OnUserUnhandledException` calls `JniTransition.SetPendingException`, which preserves the original managed exception when re-raised on the calling thread, instead of translating to `Java.Lang.Throwable` like legacy `AndroidEnvironment.UnhandledException` did. JCW-side `throws` clauses (from `ThrownNames`) are emitted equivalently. |
| Caching | First registration emits + caches a delegate type by signature key (`EncodeMethodSignature`). | No caching needed. |
| GC pinning | Manual `prevent_delegate_gc` list rooted forever | n/a |

---

## 4. `[ExportField]` codepath

`[ExportField]` is sugar for "static field initialiser implemented in C#":
the JCW declares a Java field whose value is supplied by a managed method.

| Aspect | Legacy | Trimmable |
| --- | --- | --- |
| JCW emission | Field declaration + `static {}` clinit calling the marshal method | **Same** — `JcwJavaSourceGenerator` emits identical clinit + field decl |
| Marshal method registration | Treated as a regular `[Export]` method with connector `"__export__"` and the **method name as the JNI name** | Treated identically: `ParseExportFieldAsMethod` (JavaPeerScanner.cs:1162) returns `Connector = "__export__"`, `JniName = managedName`. UCO emission is the same as for any `[Export]` method. |
| Runtime invocation | Through `RegisterNativeMembers` `__export__` branch (line 612) → `CreateDynamicCallback` | Direct UCO call (build-time IL) |
| Multiple `[ExportField]` | Each gets its own marshal method | Same |
| Test coverage | ❌ **no legacy unit tests** in this repo for `[ExportField]`; only indirect coverage via `MonoAndroidExportTest` referenced-asm probe | ✅ unit: `Generator/ExportFieldTests.cs` (3 Facts: scanner detects `[ExportField]`, scanner produces `__export__` connector, JCW generator emits field+clinit). ❌ no device test asserts the field is actually visible from Java code. |

**Behavioural risk**: `[ExportField]` methods that return a non-trivial type
(e.g. an `int[]` constant array) hit the same `SymbolKind` matrix above.
The `Collection`/`CharSequence`/`Enum`/`GenericTypeParameter` gaps therefore
also affect `[ExportField]`, but the canonical use case (`int`/`string`/peer
return) works on both paths.

---

## 5. JNI ABI encoding differences

`JniSignatureHelper.cs` (trimmable) and `CallbackCode.cs::GetNativeType`
(legacy) both translate JNI types into the actual P/Invoke / UCO signature
seen by the runtime.

| JNI type | Legacy `GetNativeType` (CallbackCode.cs:505) | Trimmable `EncodeClrType` | Trimmable `EncodeClrTypeForCallback` (n_* MCW signature) | Notes |
| --- | --- | --- | --- | --- |
| `boolean (Z)` | `bool` | **`byte`** | **`sbyte`** | Largest divergence. Legacy passes the `bool` directly to SRE; trimmable uses byte at the JNI ABI boundary and converts via `cgt.un`, then calls into MCW callbacks whose generated signature uses `sbyte`. The asymmetry is deliberate — see `subject: "trimmable typemap"` memory. |
| `byte (B)` | `sbyte` | `sbyte` | `sbyte` | aligned |
| `short (S)` | `short` | `short` | `short` | aligned |
| `char (C)` | `char` | `char` | `char` | aligned |
| `int (I)` | `int` | `int` | `int` | aligned |
| `long (J)` | `long` | `long` | `long` | aligned |
| `float (F)` | `float` | `float` | `float` | aligned |
| `double (D)` | `double` | `double` | `double` | aligned |
| `void (V)` | `void` | `void` | `void` | aligned |
| object (`L…;` / `[…`) | `IntPtr` | `IntPtr` | `IntPtr` | aligned |
| enum | `int` (legacy widens enum at the ABI boundary) | ✅ **underlying primitive** (`I` / `B` / `S` / `J`) — landed in commit `634af359d`. Scanner walks the assembly cache to detect `System.Enum`-derived types and emits the underlying primitive JNI descriptor; `TypeRefData.IsEnum` triggers `ELEMENT_TYPE_VALUETYPE` in metadata signatures. | ✅ same | aligned |

---

## 6. Tests inventory

### Trimmable unit tests (`tests/Microsoft.Android.Sdk.TrimmableTypeMap.Tests/Generator/`)

| File | Tests | Covers |
| --- | --- | --- |
| `TypeMapAssemblyGeneratorTests.cs` | 63 Facts | Per-signature-shape UCO IL emission incl. primitives, strings, arrays, mixed object peer + primitives, parameterless+parameterized ctor activation, fallback to `()V` when no managed match, copy-back loops |
| `ExportFieldTests.cs` | 3 Facts | `[ExportField]` scanner detection + JCW emission |
| `ExportAccessModifierTests.cs` | 3 Facts | UCO emitted regardless of access modifier (private/internal `[Export]` methods) |
| `JcwJavaSourceGeneratorTests.cs` | 25 Facts | JCW Java source includes the `[Export]` line / `__export__` connector |
| `ConstructorSuperArgsTests.cs` | 3 Facts | `[Export(SuperArgumentsString = …)]` on ctor → JCW emits `super(…)` (not directly UCO-related but adjacent) |

### Trimmable integration tests
- `tests/Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests/UserTypesFixture/UserTypes.cs` — full-pipeline assembly with real `[Export]`/`[ExportField]` methods.

### Device tests (`tests/Mono.Android-Tests/Mono.Android-Tests/Java.Interop/JnienvTest.cs`)

| Test | Category | Exercises |
| --- | --- | --- |
| `CreateTypeWithExportedMethods` | `Export` | Calls `[Export] void Exported()` (no args) on `ContainsExportedMethods` from C# **and** through JNI. Verifies counter increments twice. |
| `ActivatedDirectObjectSubclassesShouldBeRegistered` | `Export` | `()V` ctor activation through `JNIEnv.StartCreateInstance` / `FinishCreateInstance` — tests the trivial UCO ctor path. |
| `ActivatedDirectThrowableSubclassesShouldBeRegistered` | (none) | Same as above for a `Throwable` subclass. |

### Build / device tests for the legacy path
- `tests/MSBuildDeviceIntegration/Tests/MonoAndroidExportTest.cs` — verifies an app
  with `[Export]` requires `Mono.Android.Export.dll` to be referenced;
  exercises the runtime SRE-based codegen end-to-end.

### Coverage gaps (apply to **both** codepaths unless noted)

1. **No device test exercises an `[Export]` method that takes a non-trivial argument** (string, primitive, peer, array, stream, etc.) and is invoked from the Java side. `CreateTypeWithExportedMethods` only covers `()V`. ❗ This is the highest-value gap to close.
2. **No test covers an `[Export]` method that returns** anything except `void` (legacy) or a peer/primitive (trimmable). The marshalling-back path is therefore lightly exercised.
3. **No test covers enums** as `[Export]` parameters or return — and §2 notes a real bug in trimmable here.
4. **No test covers `ICharSequence` / `IList` / `IDictionary` / `ICollection`** as `[Export]` parameter or return types. These are real divergences that production code may rely on.
5. **No test covers `[ExportParameter]` (Stream / XmlPullParser / XmlResourceParser)** end-to-end on either path.
6. **No test verifies array-mutation copy-back** is observed on the Java side after returning from an `[Export]` managed method.
7. **No test covers an `[Export]` method declared on a generic type** (legacy throws, trimmable also throws — but neither is asserted).
8. **No device test for `[ExportField]`** confirms the Java-side field is initialised correctly under the trimmable path.
9. **`SuperArgumentsString` on `[Export]` ctors** is exercised at JCW-generation level (`ConstructorSuperArgsTests.cs`) but not in a device run.

---

## 7. Summary of behavioural differences (= things that could regress when switching to trimmable)

Ranked by risk:

1. **Enum parameters / return values** — ✅ **Fixed in this PR** (commit `634af359d`). Scanner emits underlying primitive JNI descriptor; emitter encodes `ELEMENT_TYPE_VALUETYPE`. Covered by unit tests.
2. **`ICharSequence` / `IList` / `IDictionary` / `ICollection`** — ✅ **Fixed in this PR** for return path (commit `86e94d777`). Strongly-typed dispatch through `CharSequence.ToLocalJniHandle` / `JavaList.ToLocalJniHandle` / `JavaDictionary.ToLocalJniHandle` / `JavaCollection.ToLocalJniHandle`. Covered by unit tests.
3. **`bool` JNI ABI** — bytewise on trimmable, raw `bool` on legacy. Both work but the conversion path differs; covered by unit tests.
4. **Exception type observed by Java callers** — ✅ Wrapper landed in this PR. Process no longer aborts. Divergence remains: legacy translated to `Java.Lang.Throwable`; trimmable preserves the original managed exception type via `JniTransition.SetPendingException`. Open question whether to align with legacy.
5. **`Mono.Android.Export.dll` reference requirement** — gone with trimmable. This is an *improvement*, not a regression.
6. **`__this` resolution** — different `Java.Lang.Object.GetObject` overload; functionally equivalent.
7. **Parameterized `[Export]` ctors with generic / by-ref / pointer parameter types** — ✅ Scanner now skips these and falls back to the activation-ctor path (`JavaPeerScanner.TryFindMatchingManagedCtorParams`), matching legacy behaviour. Fixed pre-existing `Xamarin.Android.NUnitLite.TestDataAdapter` build break.

### New finding — JCW emitter blocks device-level exercise of items 1 and 2

The Java callable wrapper emitter (`Xamarin.Android.Build.Tasks` / `CecilImporter.GetJniSignature`) is **shared between the legacy and trimmable codepaths**. It returns `null` for managed enums, non-bound `IList`/`IDictionary`/`ICollection`, and certain `ICharSequence` shapes — when an `[Export]` method uses one of these types, the build fails before either runtime path can be exercised. The trimmable typemap fixes above are correct on the IL/marshalling side, but a real Java-side caller cannot reach them until the JCW emitter is taught to widen these types (e.g. enums to `int`, non-generic collections to `java/util/{List,Map,Collection}`, `ICharSequence` to `java/lang/CharSequence`).

This is a separate, larger change in the legacy codegen pipeline that lives outside the trimmable typemap project — recommended as a follow-up PR.

## 8. Recommended next steps

### Done in this PR
- ✅ UCO marshal-method exception wrapper (item 4 above) — Group B `ExportTests` now run unignored on trimmable; 11/11 pass.
- ✅ Primitive marshalling reused in parameterized UCO ctor activation (covered by new unit tests).
- ✅ Scanner filter for unsupported parameterized `[Export]` ctor parameter types.
- ✅ **Enum marshalling** — scanner emits underlying primitive descriptor; emitter flags as value-type. Unit tests added.
- ✅ **`ICharSequence` return marshalling** — strongly-typed call to `CharSequence.ToLocalJniHandle (ICharSequence)`. Unit tests added.
- ✅ **Non-generic collection return marshalling** — strongly-typed calls to `JavaList`/`JavaDictionary`/`JavaCollection.ToLocalJniHandle`. Unit tests added.

### Still open (suggested follow-ups)
- **JCW-emitter widening** (`CecilImporter.GetJniSignature`) — teach the legacy Java callable wrapper to accept managed enums (widen to `int`), non-generic collections (`java/util/{List,Map,Collection}`), and `ICharSequence` (widen to `java/lang/CharSequence`). Without this, end-to-end device tests for the Phase 1 marshalling fixes cannot be authored. Likely requires its own PR against the legacy codegen pipeline.
- **`OnUserUnhandledException` exception-type translation** — decide whether to keep current managed-exception-preserved behaviour or translate to `Java.Lang.Throwable` to match legacy. Open question for product owners (file as issue).
- **`__md_methods` / `__export__` removal under TrimmableTypeMap** — JCW currently still emits `name:sig:__export__` lines into `__md_methods`. Under TrimmableTypeMap this string is not consumed (registration happens via the typemap's UCO fnptr). Plan: emit `static { registerNatives(X.class); }` and ignore `__export__` at runtime entirely. Track as separate cleanup PR.
- **Device-level coverage** — `[ExportField]` device test; `[ExportParameter]` (Stream / XmlPullParser / XmlResourceParser); `[Export]` method on a generic type (negative test); array copy-back observed from Java. Most of these depend on the JCW-emitter follow-up above.

---

*Last updated: this branch (`dev/simonrozsival/trimmable-typemap-export-attribute`).*
