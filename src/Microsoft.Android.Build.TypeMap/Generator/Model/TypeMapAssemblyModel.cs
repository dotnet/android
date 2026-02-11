using System;
using System.Collections.Generic;

namespace Microsoft.Android.Build.TypeMap;

/// <summary>
/// Intermediate representation of a single TypeMap output assembly.
/// This is the "AST" that describes what to emit — the PE emitter translates it 1:1 into IL.
/// Built by <see cref="TypeMapModelBuilder"/>, consumed by <see cref="TypeMapAssemblyGenerator"/>.
/// </summary>
sealed class TypeMapAssemblyModel
{
	/// <summary>Assembly name (e.g., "_MyApp.TypeMap").</summary>
	public string AssemblyName { get; set; } = "";

	/// <summary>Module file name (e.g., "_MyApp.TypeMap.dll").</summary>
	public string ModuleName { get; set; } = "";

	/// <summary>TypeMap entries — one per unique JNI name.</summary>
	public List<TypeMapEntryModel> Entries { get; } = new ();

	/// <summary>Proxy types to emit in the assembly.</summary>
	public List<ProxyTypeModel> ProxyTypes { get; } = new ();

	/// <summary>Assembly names that need [IgnoresAccessChecksTo] for cross-assembly n_* calls.</summary>
	public List<string> IgnoresAccessChecksTo { get; } = new () { "Mono.Android", "Java.Interop" };
}

/// <summary>
/// One [assembly: TypeMap("jni/name", typeof(TargetOrProxy))] entry.
/// </summary>
sealed class TypeMapEntryModel
{
	/// <summary>JNI type name, e.g., "android/app/Activity".</summary>
	public string JniName { get; set; } = "";

	/// <summary>
	/// Assembly-qualified type reference for the attribute's Type argument.
	/// Either points to a generated proxy or to the original managed type.
	/// </summary>
	public string TypeReference { get; set; } = "";
}

/// <summary>
/// A proxy type to generate in the TypeMap assembly (subclass of JavaPeerProxy).
/// </summary>
sealed class ProxyTypeModel
{
	/// <summary>Simple type name, e.g., "java_lang_Object_Proxy".</summary>
	public string TypeName { get; set; } = "";

	/// <summary>Namespace for all proxy types.</summary>
	public string Namespace { get; set; } = "_TypeMap.Proxies";

	/// <summary>Reference to the managed type this proxy wraps (for ldtoken in TargetType property).</summary>
	public TypeRefModel TargetType { get; set; } = new ();

	/// <summary>Reference to the invoker type (for interfaces/abstract types). Null if not applicable.</summary>
	public TypeRefModel? InvokerType { get; set; }

	/// <summary>Whether this proxy has a CreateInstance that can actually create instances (has activation ctor).</summary>
	public bool HasActivation { get; set; }

	/// <summary>Whether this proxy needs ACW support (RegisterNatives + UCO wrappers).</summary>
	public bool IsAcw { get; set; }

	/// <summary>Implements IAndroidCallableWrapper when IsAcw is true.</summary>
	public bool ImplementsIAndroidCallableWrapper => IsAcw;

	/// <summary>UCO method wrappers for marshal methods (non-constructor).</summary>
	public List<UcoMethodModel> UcoMethods { get; } = new ();

	/// <summary>UCO constructor wrappers.</summary>
	public List<UcoConstructorModel> UcoConstructors { get; } = new ();

	/// <summary>RegisterNatives registrations (method name, JNI signature, wrapper name).</summary>
	public List<NativeRegistrationModel> NativeRegistrations { get; } = new ();
}

/// <summary>
/// A cross-assembly type reference (assembly name + full managed type name).
/// </summary>
sealed class TypeRefModel
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
sealed class UcoMethodModel
{
	/// <summary>Name of the generated wrapper method, e.g., "n_onCreate_uco_0".</summary>
	public string WrapperName { get; set; } = "";

	/// <summary>Name of the n_* callback to call, e.g., "n_OnCreate".</summary>
	public string CallbackMethodName { get; set; } = "";

	/// <summary>Type containing the callback method.</summary>
	public TypeRefModel CallbackType { get; set; } = new ();

	/// <summary>JNI method signature, e.g., "(Landroid/os/Bundle;)V". Used to determine CLR parameter types.</summary>
	public string JniSignature { get; set; } = "";
}

/// <summary>
/// An [UnmanagedCallersOnly] static wrapper for a constructor callback.
/// Body: TrimmableNativeRegistration.ActivateInstance(self, typeof(TargetType)).
/// </summary>
sealed class UcoConstructorModel
{
	/// <summary>Name of the generated wrapper, e.g., "nctor_0_uco".</summary>
	public string WrapperName { get; set; } = "";

	/// <summary>Target type to pass to ActivateInstance.</summary>
	public TypeRefModel TargetType { get; set; } = new ();
}

/// <summary>
/// One JNI native method registration in RegisterNatives.
/// </summary>
sealed class NativeRegistrationModel
{
	/// <summary>JNI method name to register, e.g., "n_onCreate" or "nctor_0".</summary>
	public string JniMethodName { get; set; } = "";

	/// <summary>JNI method signature, e.g., "(Landroid/os/Bundle;)V".</summary>
	public string JniSignature { get; set; } = "";

	/// <summary>Name of the UCO wrapper method whose function pointer to register.</summary>
	public string WrapperMethodName { get; set; } = "";
}
