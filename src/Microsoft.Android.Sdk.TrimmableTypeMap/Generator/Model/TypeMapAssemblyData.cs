using System;
using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Data model for a single TypeMap output assembly.
/// Describes what to emit — the emitter writes this directly into a PE assembly.
/// Built by <see cref="ModelBuilder"/>, consumed by <see cref="TypeMapAssemblyGenerator"/>.
/// </summary>
sealed class TypeMapAssemblyData
{
	/// <summary>Assembly name (e.g., "_MyApp.TypeMap").</summary>
	public string AssemblyName { get; set; } = "";

	/// <summary>Module file name (e.g., "_MyApp.TypeMap.dll").</summary>
	public string ModuleName { get; set; } = "";

	/// <summary>TypeMap entries — one per unique JNI name.</summary>
	public List<TypeMapAttributeData> Entries { get; } = new ();

	/// <summary>Proxy types to emit in the assembly.</summary>
	public List<JavaPeerProxyData> ProxyTypes { get; } = new ();

	/// <summary>Assembly names that need [IgnoresAccessChecksTo] for cross-assembly n_* calls.</summary>
	public List<string> IgnoresAccessChecksTo { get; } = new ();
}

/// <summary>
/// One [assembly: TypeMap("jni/name", typeof(Proxy))] or
/// [assembly: TypeMap("jni/name", typeof(Proxy), typeof(Target))] entry.
///
/// 2-arg (unconditional): proxy is always preserved — used for ACW types and essential runtime types.
/// 3-arg (trimmable): proxy is preserved only if Target type is referenced by the app.
/// </summary>
sealed class TypeMapAttributeData
{
	/// <summary>JNI type name, e.g., "android/app/Activity".</summary>
	public string JniName { get; set; } = "";

	/// <summary>
	/// Assembly-qualified proxy type reference string.
	/// Either points to a generated proxy or to the original managed type.
	/// </summary>
	public string ProxyTypeReference { get; set; } = "";

	/// <summary>
	/// Assembly-qualified target type reference for the trimmable (3-arg) variant.
	/// Null for unconditional (2-arg) entries.
	/// The trimmer preserves the proxy only if this target type is used by the app.
	/// </summary>
	public string? TargetTypeReference { get; set; }

	/// <summary>True for 2-arg unconditional entries (ACW types, essential runtime types).</summary>
	public bool IsUnconditional => TargetTypeReference == null;
}

/// <summary>
/// A proxy type to generate in the TypeMap assembly (subclass of JavaPeerProxy).
/// </summary>
sealed class JavaPeerProxyData
{
	/// <summary>Simple type name, e.g., "Java_Lang_Object_Proxy".</summary>
	public string TypeName { get; set; } = "";

	/// <summary>Namespace for all proxy types.</summary>
	public string Namespace { get; set; } = "_TypeMap.Proxies";

	/// <summary>Reference to the managed type this proxy wraps (for ldtoken in TargetType property).</summary>
	public TypeRefData TargetType { get; set; } = new ();

	/// <summary>Reference to the invoker type (for interfaces/abstract types). Null if not applicable.</summary>
	public TypeRefData? InvokerType { get; set; }

	/// <summary>Whether this proxy has a CreateInstance that can actually create instances (has activation ctor).</summary>
	public bool HasActivation { get; set; }

	/// <summary>Whether this proxy needs ACW support (RegisterNatives + UCO wrappers).</summary>
	public bool IsAcw { get; set; }

	/// <summary>Implements IAndroidCallableWrapper when IsAcw is true.</summary>
	public bool ImplementsIAndroidCallableWrapper => IsAcw;

	/// <summary>UCO method wrappers for marshal methods (non-constructor).</summary>
	public List<UcoMethodData> UcoMethods { get; } = new ();

	/// <summary>UCO constructor wrappers.</summary>
	public List<UcoConstructorData> UcoConstructors { get; } = new ();

	/// <summary>RegisterNatives registrations (method name, JNI signature, wrapper name).</summary>
	public List<NativeRegistrationData> NativeRegistrations { get; } = new ();
}

/// <summary>
/// A cross-assembly type reference (assembly name + full managed type name).
/// </summary>
sealed class TypeRefData
{
	/// <summary>Full managed type name, e.g., "Android.App.Activity" or "MyApp.Outer+Inner".</summary>
	public string ManagedTypeName { get; set; } = "";

	/// <summary>Assembly containing the type, e.g., "Mono.Android".</summary>
	public string AssemblyName { get; set; } = "";
}

/// <summary>
/// An [UnmanagedCallersOnly] static wrapper for a marshal method.
/// Body: load all args → call n_* callback → ret.
/// </summary>
sealed class UcoMethodData
{
	/// <summary>Name of the generated wrapper method, e.g., "n_onCreate_uco_0".</summary>
	public string WrapperName { get; set; } = "";

	/// <summary>Name of the n_* callback to call, e.g., "n_OnCreate".</summary>
	public string CallbackMethodName { get; set; } = "";

	/// <summary>Type containing the callback method.</summary>
	public TypeRefData CallbackType { get; set; } = new ();

	/// <summary>JNI method signature, e.g., "(Landroid/os/Bundle;)V". Used to determine CLR parameter types.</summary>
	public string JniSignature { get; set; } = "";
}

/// <summary>
/// An [UnmanagedCallersOnly] static wrapper for a constructor callback.
/// Body: TrimmableNativeRegistration.ActivateInstance(self, typeof(TargetType)).
/// </summary>
sealed class UcoConstructorData
{
	/// <summary>Name of the generated wrapper, e.g., "nctor_0_uco".</summary>
	public string WrapperName { get; set; } = "";

	/// <summary>Target type to pass to ActivateInstance.</summary>
	public TypeRefData TargetType { get; set; } = new ();

	/// <summary>JNI constructor signature, e.g., "(Landroid/content/Context;)V". Used for RegisterNatives registration.</summary>
	public string JniSignature { get; set; } = "()V";
}

/// <summary>
/// One JNI native method registration in RegisterNatives.
/// </summary>
sealed class NativeRegistrationData
{
	/// <summary>JNI method name to register, e.g., "n_onCreate" or "nctor_0".</summary>
	public string JniMethodName { get; set; } = "";

	/// <summary>JNI method signature, e.g., "(Landroid/os/Bundle;)V".</summary>
	public string JniSignature { get; set; } = "";

	/// <summary>Name of the UCO wrapper method whose function pointer to register.</summary>
	public string WrapperMethodName { get; set; } = "";
}
