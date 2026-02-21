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
	/// <summary>
	/// Assembly name (e.g., "_MyApp.TypeMap").
	/// </summary>
	public required string AssemblyName { get; init; }

	/// <summary>

	/// Module file name (e.g., "_MyApp.TypeMap.dll").

	/// </summary>
	public required string ModuleName { get; init; }

	/// <summary>

	/// TypeMap entries — one per unique JNI name.

	/// </summary>
	public List<TypeMapAttributeData> Entries { get; } = new ();

	/// <summary>

	/// Proxy types to emit in the assembly.

	/// </summary>
	public List<JavaPeerProxyData> ProxyTypes { get; } = new ();

	/// <summary>

	/// TypeMapAssociation entries for alias groups (multiple managed types → same JNI name).

	/// </summary>
	public List<TypeMapAssociationData> Associations { get; } = new ();

	/// <summary>

	/// Assembly names that need [IgnoresAccessChecksTo] for cross-assembly n_* calls.

	/// </summary>
	public List<string> IgnoresAccessChecksTo { get; } = new ();
}

/// <summary>
/// One [assembly: TypeMap("jni/name", typeof(Proxy))] or
/// [assembly: TypeMap("jni/name", typeof(Proxy), typeof(Target))] entry.
///
/// 2-arg (unconditional): proxy is always preserved — used for ACW types and essential runtime types.
/// 3-arg (trimmable): proxy is preserved only if Target type is referenced by the app.
/// </summary>
sealed record TypeMapAttributeData
{
	/// <summary>
	/// JNI type name, e.g., "android/app/Activity".
	/// </summary>
	public required string JniName { get; init; }

	/// <summary>
	/// Assembly-qualified proxy type reference string.
	/// Either points to a generated proxy or to the original managed type.
	/// </summary>
	public required string ProxyTypeReference { get; init; }

	/// <summary>
	/// Assembly-qualified target type reference for the trimmable (3-arg) variant.
	/// Null for unconditional (2-arg) entries.
	/// The trimmer preserves the proxy only if this target type is used by the app.
	/// </summary>
	public string? TargetTypeReference { get; init; }

	/// <summary>

	/// True for 2-arg unconditional entries (ACW types, essential runtime types).

	/// </summary>
	public bool IsUnconditional => TargetTypeReference == null;
}

/// <summary>
/// A proxy type to generate in the TypeMap assembly (subclass of JavaPeerProxy).
/// </summary>
sealed class JavaPeerProxyData
{
	/// <summary>
	/// Simple type name, e.g., "Java_Lang_Object_Proxy".
	/// </summary>
	public required string TypeName { get; init; }

	/// <summary>

	/// Namespace for all proxy types.

	/// </summary>
	public string Namespace { get; init; } = "_TypeMap.Proxies";

	/// <summary>

	/// Reference to the managed type this proxy wraps (for ldtoken in TargetType property).

	/// </summary>
	public required TypeRefData TargetType { get; init; }

	/// <summary>

	/// Reference to the invoker type (for interfaces/abstract types). Null if not applicable.

	/// </summary>
	public TypeRefData? InvokerType { get; set; }

	/// <summary>

	/// Whether this proxy has a CreateInstance that can actually create instances.

	/// </summary>
	public bool HasActivation => ActivationCtor != null || InvokerType != null;

	/// <summary>
	/// Activation constructor details. Determines how CreateInstance instantiates the managed peer.
	/// </summary>
	public ActivationCtorData? ActivationCtor { get; set; }

	/// <summary>

	/// True if this is an open generic type definition. CreateInstance throws NotSupportedException.

	/// </summary>
	public bool IsGenericDefinition { get; init; }

}


/// <summary>
/// A cross-assembly type reference (assembly name + full managed type name).
/// </summary>
sealed record TypeRefData
{
	/// <summary>
	/// Full managed type name, e.g., "Android.App.Activity" or "MyApp.Outer+Inner".
	/// </summary>
	public required string ManagedTypeName { get; init; }

	/// <summary>

	/// Assembly containing the type, e.g., "Mono.Android".

	/// </summary>
	public required string AssemblyName { get; init; }
}

/// <summary>
/// Describes how the proxy's CreateInstance should construct the managed peer.
/// </summary>
sealed record ActivationCtorData
{
	/// <summary>
	/// Type that declares the activation constructor (may be a base type).
	/// </summary>
	public required TypeRefData DeclaringType { get; init; }

	/// <summary>

	/// True when the leaf type itself declares the activation ctor.

	/// </summary>
	public required bool IsOnLeafType { get; init; }

	/// <summary>

	/// The style of activation ctor (XamarinAndroid or JavaInterop).

	/// </summary>
	public required ActivationCtorStyle Style { get; init; }
}

/// <summary>
/// One [assembly: TypeMapAssociation(typeof(Source), typeof(AliasProxy))] entry.
/// Links a managed type to the proxy that holds its alias TypeMap entry.
/// </summary>
sealed record TypeMapAssociationData
{
	/// <summary>
	/// Assembly-qualified source type reference (the managed alias type).
	/// </summary>
	public required string SourceTypeReference { get; init; }

	/// <summary>

	/// Assembly-qualified proxy type reference (the alias holder proxy).

	/// </summary>
	public required string AliasProxyTypeReference { get; init; }
}
