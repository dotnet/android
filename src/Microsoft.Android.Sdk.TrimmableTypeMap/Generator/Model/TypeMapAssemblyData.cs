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

	/// <summary>TypeMapAssociation entries for alias groups (multiple managed types → same JNI name).</summary>
	public List<TypeMapAssociationData> Associations { get; } = new ();

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

	/// <summary>Whether this proxy has a CreateInstance that can actually create instances.</summary>
	public bool HasActivation => ActivationCtor != null || InvokerType != null;

	/// <summary>
	/// Activation constructor details. Determines how CreateInstance instantiates the managed peer.
	/// </summary>
	public ActivationCtorData? ActivationCtor { get; set; }

	/// <summary>True if this is an open generic type definition. CreateInstance throws NotSupportedException.</summary>
	public bool IsGenericDefinition { get; set; }

	/// <summary>Whether this proxy needs ACW support (RegisterNatives + UCO wrappers + IAndroidCallableWrapper).</summary>
	public bool IsAcw { get; set; }

	/// <summary>UCO method wrappers for [Register] methods and constructors.</summary>
	public List<UcoMethodData> UcoMethods { get; } = new ();

	/// <summary>Export marshal method wrappers — full marshal body for [Export] methods and constructors.</summary>
	public List<ExportMarshalMethodData> ExportMarshalMethods { get; } = new ();

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
/// An [UnmanagedCallersOnly] static wrapper for an [Export] method or constructor.
/// Unlike <see cref="UcoMethodData"/> which just forwards to an existing n_* callback,
/// this generates the full marshal method body: BeginMarshalMethod, GetObject, param
/// unmarshaling, managed method call, return marshaling, exception handling, EndMarshalMethod.
/// </summary>
sealed class ExportMarshalMethodData
{
	/// <summary>Name of the generated wrapper method, e.g., "n_myMethod_uco_0" or "nctor_0_uco".</summary>
	public string WrapperName { get; set; } = "";

	/// <summary>
	/// JNI method name for RegisterNatives, e.g., "n_DoWork" or "nctor_0".
	/// Must match the native method declaration in the Java JCW.
	/// </summary>
	public string NativeCallbackName { get; set; } = "";

	/// <summary>Name of the managed method to call, e.g., "MyMethod" or ".ctor".</summary>
	public string ManagedMethodName { get; set; } = "";

	/// <summary>Type containing the managed method (the user's type).</summary>
	public TypeRefData DeclaringType { get; set; } = new ();

	/// <summary>JNI method signature, e.g., "(Ljava/lang/String;I)V".</summary>
	public string JniSignature { get; set; } = "";

	/// <summary>True if this is a constructor.</summary>
	public bool IsConstructor { get; set; }

	/// <summary>True if this is a static method.</summary>
	public bool IsStatic { get; set; }

	/// <summary>
	/// Managed parameter types for the managed method call.
	/// Each entry is the assembly-qualified managed type name.
	/// </summary>
	public List<ExportParamData> ManagedParameters { get; } = new ();

	/// <summary>Managed return type (assembly-qualified). Null/empty for void or constructors.</summary>
	public string? ManagedReturnType { get; set; }
}

/// <summary>
/// Describes a parameter for an [Export] marshal method, with both JNI and managed type info.
/// </summary>
sealed class ExportParamData
{
	/// <summary>JNI type descriptor, e.g., "Ljava/lang/String;", "I".</summary>
	public string JniType { get; set; } = "";

	/// <summary>Managed type name (assembly-qualified), e.g., "System.String, System.Private.CoreLib".</summary>
	public string ManagedTypeName { get; set; } = "";

	/// <summary>Assembly containing the managed type.</summary>
	public string AssemblyName { get; set; } = "";
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

/// <summary>
/// Describes how the proxy's CreateInstance should construct the managed peer.
/// </summary>
sealed class ActivationCtorData
{
	/// <summary>Type that declares the activation constructor (may be a base type).</summary>
	public TypeRefData DeclaringType { get; set; } = new ();

	/// <summary>True when the leaf type itself declares the activation ctor.</summary>
	public bool IsOnLeafType { get; set; }

	/// <summary>The style of activation ctor (XamarinAndroid or JavaInterop).</summary>
	public ActivationCtorStyle Style { get; set; }
}

/// <summary>
/// One [assembly: TypeMapAssociation(typeof(Source), typeof(AliasProxy))] entry.
/// Links a managed type to the proxy that holds its alias TypeMap entry.
/// </summary>
sealed class TypeMapAssociationData
{
	/// <summary>Assembly-qualified source type reference (the managed alias type).</summary>
	public string SourceTypeReference { get; set; } = "";

	/// <summary>Assembly-qualified proxy type reference (the alias holder proxy).</summary>
	public string AliasProxyTypeReference { get; set; } = "";
}
