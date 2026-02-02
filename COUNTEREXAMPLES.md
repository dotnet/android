# TypeMap V4: Counterexamples and Challenging Scenarios

This document identifies questions and scenarios from existing tests and documentation that may be challenging for the TypeMap V4 implementation. These serve as the "real spec" for the legacy typemap that V4 must match or explicitly document as breaking changes.

**Cross-Reference:** All items in this document are now addressed in Section 8 of `type-mapping-api-v4-spec.md` (Edge Cases and Special Handling).

---

## Part 1: Questions

### Q1: Thread-Safety During Type Registration

From `JnienvTest.cs:RegisterTypeOnNewNativeThread`, `RegisterTypeOnNewJavaThread`, `RegisterTypeOnNewManagedThread`:

**Question:** How does V4 handle type registration from multiple threads simultaneously? The legacy system supports registering types from native threads, Java threads, and managed threads. Does the pre-generated typemap work correctly when accessed from threads that weren't attached when the app started?

---

### Q2: Virtual Method Dispatch During Construction

From `AdapterTests.cs:CanOverrideAbsListView_Adapter`:

**Question:** When a Java constructor virtually calls a method that requires .NET type resolution (e.g., `getAdapter()` during `AbsListView.<init>`), how does V4 handle the case where the managed object isn't fully constructed yet? The legacy system requires an activation constructor `(IntPtr, JniHandleOwnership)`.

---

### Q3: Open Generic Types

From `JnienvTest.cs:NewOpenGenericTypeThrows`:

**Question:** The test expects `JNIEnv.StartCreateInstance(typeof(GenericHolder<>))` to throw `NotSupportedException`. Does V4 generate proxies for closed generics like `GenericHolder<int>` but correctly reject open generics?

---

### Q4: GetObject Returns Most Derived Type

From `ObjectTest.cs:GetObject_ReturnsMostDerivedType`:

**Question:** When calling `GetObject<Java.Lang.Object>(stringHandle)`, the result should be `Java.Lang.String`, not `Java.Lang.Object`. Does V4's type resolution walk the Java class hierarchy to find the most specific .NET type?

---

### Q5: Instance Identity Preservation

From `ObjectTest.cs:JavaConvert_FromJavaObject_ShouldNotBreakExistingReferences`:

**Question:** If a managed object already exists for a JNI handle, does V4 return the same instance? The test verifies `Object.ReferenceEquals(instance, GetObject<T>(handle))`. How does V4 integrate with the surfaced objects tracking?

---

### Q6: Duplicate JNI Names Across Assemblies

From `Documentation/docs-mobile/messages/xa4214.md`:

**Question:** What happens when two managed types in different assemblies map to the same JNI name? V4 generates assembly-level attributes - how does it handle or warn about duplicates?

---

### Q7: Export Attribute Support

From `JnienvTest.cs:CreateTypeWithExportedMethods` (marked as `[Ignore]`):

**Question:** The test is currently ignored with note "[Export] is not supported with TypeMap v3". Is this still the case for V4? What's the migration path for apps using `[Export]`? Should they use `[JavaCallable]` source generators instead?

---

### Q8: Throwable Subclass Activation

From `JnienvTest.cs:ActivatedDirectThrowableSubclassesShouldBeRegistered`:

**Question:** Does V4 correctly handle types that extend `Java.Lang.Throwable`? These have special semantics for exception marshaling.

---

### Q9: JavaCast to Interface

From `JavaObjectExtensionsTests.cs:JavaCast_InterfaceCast`:

**Question:** When casting to an interface type, V4 needs to create an invoker (e.g., `IValueProviderInvoker`). Does the generated typemap include invoker mappings for all interfaces?

---

### Q10: JavaCast to Managed Subclass

From `JavaObjectExtensionsTests.cs:JavaCast_CheckForManagedSubclasses`:

**Question:** The test expects `InvalidCastException` when trying to cast a `java.lang.Object` instance to a managed subclass like `MyObject`. Does V4 correctly detect this impossible cast?

---

### Q11: Array of Custom JCW Types

From `JnienvArrayMarshaling.cs:NewArray_UseJcwTypeWhenRenamed`:

**Question:** When creating an array of types with `[Register("custom/jni/Name")]`, does V4 use the correct JNI array type signature?

---

### Q12: JavaList Generic vs Non-Generic

From `JavaListTest.cs`:

**Question:** Tests run with both `JavaList` and `JavaList<string>`. Does V4 generate correct mappings for both? How does it handle the relationship between the non-generic and generic variants?

---

### Q13: JavaCollection Created from JNI Handle

From `JavaCollectionTest.cs:CopyTo`:

**Question:** `new JavaCollection(handle, JniHandleOwnership.DoNotTransfer)` creates a wrapper around an existing Java object. Does V4 support this pattern where no factory method is called?

---

### Q14: Invoker Wrapper Disposal

From `InputStreamInvokerTest.cs:Disposing_Shared_Data_Does_Not_Throw_IOE`:

**Question:** When an invoker wraps a Java object that's disposed separately, the invoker's disposal shouldn't throw. Does V4's generated invokers handle this gracefully?

---

### Q15: JavaProxyThrowable Creation

From `ExceptionTest.cs:InnerExceptionIsSet`:

**Question:** `JavaProxyThrowable.Create(Exception)` wraps a .NET exception in a Java Throwable. Does V4 support this scenario? Is `JavaProxyThrowable` in the typemap?

---

### Q16: Java-Side Type Activation

From `BindingTests.cs:JavaSideActivation`:

**Question:** When Java code calls `newInstance()` on a managed type's class, V4 must handle the activation. The test uses `CallMethodFromCtor.NewInstance(class)`. Does V4 register the necessary JNI callbacks for this?

---

### Q17: Non-Virtual Method Dispatch

From `BindingTests.cs` (lines 117-150 comment):

**Question:** When a derived Java type overrides a method but the binding doesn't emit the override, calling `base.Method()` must dispatch correctly. Does V4's marshal method registration handle this virtual/non-virtual distinction?

---

### Q18: GC Bridge and Weak References

From `JnienvTest.cs:DoNotLeakWeakReferences`:

**Question:** The GC bridge tracks surfaced objects. When an object is collected, it must be removed from tracking. Does V4's activation path integrate correctly with `Runtime.GetSurfacedObjects()`?

---

### Q19: Mono.Android.Export Dependency

From `MonoAndroidExportTest.cs`:

**Question:** The test shows NativeAOT doesn't support `Mono.Android.Export`. What's the V4 story for export methods? Is there an alternative for NativeAOT users?

---

### Q20: java/lang/Object Mapping Priority

From `ObjectTest.cs:java_lang_Object_Is_Java_Lang_Object`:

**Question:** Multiple types might try to register for `java/lang/Object`. V4 must ensure `Java.Lang.Object` from `Mono.Android` wins. How does V4 handle mapping priority?

---

## Part 2: Challenging Scenarios

### S1: Virtual Callback During Constructor

**Source:** `AdapterTests.cs:CanOverrideAbsListView_Adapter`

**Scenario:** `AbsListView` constructor calls `getAdapter()` which triggers managed callback before the managed object is fully constructed.

**Challenge for V4:** The activation constructor must be registered and working before any other method can be called. V4's lazy registration won't work here.

---

### S2: Multiple Threads Registering Same Type

**Source:** `JnienvTest.cs:ConversionsAndThreadsAndInstanceMappingsOhMy`, `MoarThreadingTests`

**Scenario:** Two threads simultaneously access the same Java array and convert elements to managed types.

**Challenge for V4:** The type map caches (`ConcurrentDictionary`) must handle concurrent access without corruption or duplicate registration.

---

### S3: Cross-Thread JNI Handle Usage

**Source:** `JnienvTest.cs:ThreadReuse`, `DeleteLrefOnWrongThread`

**Scenario:** JNI handles created on one thread used on another.

**Challenge for V4:** The JNI class cache (`_jniClassCache`) stores global refs. Thread affinity issues could cause problems.

---

### S4: Generic Type with Value Type Parameter

**Source:** `JnienvTest.cs:NewClosedGenericTypeWorks`

**Scenario:** `GenericHolder<int>` is a valid type that should work.

**Challenge for V4:** Value type parameters can't be directly wrapped. V4 must generate correct boxing/unboxing code.

---

### S5: Array of Arrays (Jagged Arrays)

**Source:** `JnienvArrayMarshaling.cs:GetArray_ByteArrayArray`, `NewArray_Int32ArrayArray`

**Scenario:** Creating and marshaling `byte[][]` or `int[][]` arrays.

**Challenge for V4:** `CreateArray` must handle rank > 1. Current PoC only supports rank 1 and 2.

---

### S6: Object Array with Mixed Types

**Source:** `JnienvArrayMarshaling.cs:GetObjectArray`

**Scenario:** An Object[] containing Context, Long, and String must be marshaled with correct types.

**Challenge for V4:** Each element may map to a different .NET type. V4 must resolve each element's type individually.

---

### S7: Interface with Default Methods

**Source:** Java 8+ interfaces can have default methods.

**Scenario:** A .NET type implements a Java interface but doesn't override a default method.

**Challenge for V4:** The JCW must correctly delegate to the Java default implementation.

---

### S8: Renamed JCW Type

**Source:** `JnienvArrayMarshaling.cs:NewArray_UseJcwTypeWhenRenamed`

**Scenario:** Type with `[Register("custom/package/Name")]` must use that name in JNI operations.

**Challenge for V4:** Generated proxy must use the Register name, not the .NET namespace.

---

### S9: Instance Registered During Construction

**Source:** `ObjectTest.cs:JnienvCreateInstance_RegistersMultipleInstances`

**Scenario:** `JNIEnv.CreateInstance` may create a temporary instance before the "real" constructor runs.

**Challenge for V4:** Handle tracking must correctly identify which instance to return.

---

### S10: Nested Dispose Calls

**Source:** `ObjectTest.cs:NestedDisposeInvocations`

**Scenario:** Dispose called multiple times, including recursively.

**Challenge for V4:** Generated proxies must be idempotent for dispose.

---

### S11: InputStreamInvoker Wrapping Java Stream

**Source:** `InputStreamInvokerTest.cs`

**Scenario:** Managed `InputStreamInvoker` wraps a Java `InputStream`.

**Challenge for V4:** Invoker types must be in the typemap and correctly instantiated.

---

### S12: Binding with Color Enum Marshaling

**Source:** `BindingTests.cs:TestBxc4288`

**Scenario:** Method takes/returns `Android.Graphics.Color` which is a struct.

**Challenge for V4:** Struct marshaling across JNI boundary.

---

### S13: Event Handler with Array Payload

**Source:** `BindingTests.cs:Arrays`

**Scenario:** Event args contain `byte[][]` payload that's modified and read back.

**Challenge for V4:** Array marshaling in both directions within event callbacks.

---

### S14: Timing Factory Method Returns Subtype

**Source:** `BindingTests.cs:TestTimingCreateTimingIsCorrectType`

**Scenario:** `Timing.CreateTiming()` returns a Timing instance, not the static return type.

**Challenge for V4:** Factory methods must return correctly typed instances.

---

### S15: JavaCast Obtains Original Instance

**Source:** `JavaObjectExtensionsTests.cs:JavaCast_ObtainOriginalInstance`

**Scenario:** Casting should return the original managed instance if one exists.

**Challenge for V4:** Must integrate with instance tracking, not create new wrapper.

---

### S16: AppDomain/AssemblyLoadContext Isolation

**Source:** Potential plugin scenarios

**Scenario:** Third-party library loaded in separate context needs type mapping.

**Challenge for V4:** Pre-generated typemap is in main context. Dynamically loaded assemblies won't have proxies.

---

### S17: Hot Reload Type Changes

**Source:** Debug scenarios with MAUI Hot Reload

**Scenario:** Type is modified during debugging session.

**Challenge for V4:** Pre-generated typemap becomes stale. Needs re-generation or fallback.

---

### S18: Trimmed App Missing Expected Type

**Source:** Aggressive trimming scenarios

**Scenario:** Trimmer removes type that's in the JNI typemap but never directly referenced.

**Challenge for V4:** Must ensure proxy types are rooted, or handle missing types gracefully.

---

### S19: Abstract Class with Package-Private Constructor

**Source:** Some Android SDK classes

**Scenario:** Abstract class can't be directly instantiated from .NET.

**Challenge for V4:** Invoker must be generated but activation constructor skipped.

---

### S20: Kotlin Sealed Classes

**Source:** Modern Android libraries

**Scenario:** Kotlin sealed class hierarchy may have unusual JNI representations.

**Challenge for V4:** Must correctly map Kotlin's nested class naming conventions.

---

### S21: Multiple [Register] on Same Type

**Source:** Edge case

**Scenario:** Type registered under multiple JNI names (if this is even valid).

**Challenge for V4:** Spec should clarify if this is supported or rejected.

---

### S22: Circular Type References

**Source:** `A` references `B`, `B` references `A`

**Scenario:** Two types that reference each other in constructors or static initializers.

**Challenge for V4:** Type loading order must not cause deadlock or stack overflow.

---

### S23: Very Large Number of Types

**Source:** Apps with many dependencies

**Scenario:** 10,000+ types in the typemap.

**Challenge for V4:** Dictionary lookup performance, startup time, memory usage.

---

### S24: JNI Name with Special Characters

**Source:** Obfuscated code

**Scenario:** JNI name contains characters that are unusual but valid in JNI.

**Challenge for V4:** Must correctly parse and handle all valid JNI names.

---

### S25: Managed Type with No Default Constructor

**Source:** Types requiring parameters

**Scenario:** Java-side activation of a type that has no parameterless constructor.

**Challenge for V4:** Activation constructor must exist, or error must be clear.

---

## Part 3: Test Coverage Requirements

Based on the above, V4 must pass tests for:

| Category | Existing Tests | V4 Status |
|----------|----------------|-----------|
| Thread-safe registration | 4+ tests | Needs verification |
| GetObject identity | 3+ tests | Needs verification |
| Array marshaling | 20+ tests | Needs verification |
| Interface invokers | 5+ tests | Needs verification |
| Generic types | 2+ tests | Needs verification |
| Throwable handling | 2+ tests | Needs verification |
| Constructor callbacks | 3+ tests | Needs verification |
| Export methods | 2 tests (ignored) | Documented gap |
| GC bridge | 1+ tests | Needs verification |

---

## Part 4: Recommended Actions

1. **Create V4-specific test suite** that runs all scenarios above
2. **Document breaking changes** for unsupported scenarios (generics, exports)
3. **Add thread-safety tests** for ConcurrentDictionary caches
4. **Benchmark large typemaps** (5K, 10K, 20K types)
5. **Test trimmed Release builds** to verify proxy rooting
6. **Verify NativeAOT compatibility** for all scenarios

---

*Generated: 2026-02-02*
*Source: Existing tests in tests/Mono.Android-Tests/, tests/CodeGen-Binding/, and Documentation/*
