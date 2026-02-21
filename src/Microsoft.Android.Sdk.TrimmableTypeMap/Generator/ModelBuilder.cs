using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Builds a <see cref="TypeMapAssemblyData"/> from scanned <see cref="JavaPeerInfo"/> records.
/// All decision logic (deduplication, alias detection, ACW filtering, 2-arg vs 3-arg attribute
/// selection, callback resolution, proxy naming) lives here.
/// The output model is a plain data structure that the emitter writes directly into a PE assembly.
/// </summary>
static class ModelBuilder
{
	static readonly HashSet<string> EssentialRuntimeTypes = new (StringComparer.Ordinal) {
		"java/lang/Object",
		"java/lang/Class",
		"java/lang/String",
		"java/lang/Throwable",
		"java/lang/Exception",
		"java/lang/RuntimeException",
		"java/lang/Error",
		"java/lang/Thread",
	};

	/// <summary>
	/// Builds a TypeMap assembly model for the given peers.
	/// </summary>
	/// <param name="peers">Scanned Java peer types (typically from a single input assembly).</param>
	/// <param name="outputPath">Output .dll path — used to derive assembly/module names if not specified.</param>
	/// <param name="assemblyName">Explicit assembly name. If null, derived from <paramref name="outputPath"/>.</param>
	public static TypeMapAssemblyData Build (IReadOnlyList<JavaPeerInfo> peers, string outputPath, string? assemblyName = null)
	{
		if (peers is null) {
			throw new ArgumentNullException (nameof (peers));
		}
		if (outputPath is null) {
			throw new ArgumentNullException (nameof (outputPath));
		}

		assemblyName ??= Path.GetFileNameWithoutExtension (outputPath);
		string moduleName = Path.GetFileName (outputPath);

		var model = new TypeMapAssemblyData {
			AssemblyName = assemblyName,
			ModuleName = moduleName,
		};

		// Invoker types are NOT emitted as separate proxies or TypeMap entries —
		// they only appear as a TypeRef in the interface proxy's get_InvokerType property.
		var invokerTypeNames = new HashSet<string> (
			peers.Where (p => p.InvokerTypeName != null).Select (p => p.InvokerTypeName!),
			StringComparer.Ordinal);

		// Group non-invoker peers by JNI name to detect aliases (multiple .NET types → same Java class).
		// Use an ordered dictionary to ensure deterministic output across runs.
		var groups = new SortedDictionary<string, List<JavaPeerInfo>> (StringComparer.Ordinal);
		foreach (var peer in peers) {
			if (invokerTypeNames.Contains (peer.ManagedTypeName)) {
				continue;
			}
			if (!groups.TryGetValue (peer.JavaName, out var list)) {
				list = new List<JavaPeerInfo> ();
				groups [peer.JavaName] = list;
			}
			list.Add (peer);
		}

		foreach (var kvp in groups) {
			string jniName = kvp.Key;
			var peersForName = kvp.Value;

			// Sort aliases by managed type name for deterministic proxy naming
			if (peersForName.Count > 1) {
				peersForName.Sort ((a, b) => StringComparer.Ordinal.Compare (a.ManagedTypeName, b.ManagedTypeName));
			}

			EmitPeers (model, jniName, peersForName, assemblyName);
		}

		// Compute IgnoresAccessChecksTo from cross-assembly references
		var referencedAssemblies = new SortedSet<string> (StringComparer.Ordinal);
		foreach (var proxy in model.ProxyTypes) {
			AddIfCrossAssembly (referencedAssemblies, proxy.TargetType?.AssemblyName, assemblyName);
			if (proxy.ActivationCtor != null && !proxy.ActivationCtor.IsOnLeafType) {
				AddIfCrossAssembly (referencedAssemblies, proxy.ActivationCtor.DeclaringType.AssemblyName, assemblyName);
			}
		}
		model.IgnoresAccessChecksTo.AddRange (referencedAssemblies);

		return model;
	}

	static void EmitPeers (TypeMapAssemblyData model, string jniName,
		List<JavaPeerInfo> peersForName, string assemblyName)
	{
		// First peer is the "primary" — it gets the base JNI name entry.
		// Remaining peers get indexed alias entries: "jni/name[1]", "jni/name[2]", ...
		JavaPeerProxyData? primaryProxy = null;
		for (int i = 0; i < peersForName.Count; i++) {
			var peer = peersForName [i];
			string entryJniName = i == 0 ? jniName : $"{jniName}[{i}]";

			bool hasProxy = peer.ActivationCtor != null || peer.InvokerTypeName != null;

			JavaPeerProxyData? proxy = null;
			if (hasProxy) {
				proxy = BuildProxyType (peer);
				model.ProxyTypes.Add (proxy);
			}

			if (i == 0) {
				primaryProxy = proxy;
			}

			model.Entries.Add (BuildEntry (peer, proxy, assemblyName, entryJniName));

			// Emit TypeMapAssociation linking alias types to the primary proxy
			if (i > 0 && primaryProxy != null) {
				model.Associations.Add (new TypeMapAssociationData {
					SourceTypeReference = $"{peer.ManagedTypeName}, {peer.AssemblyName}",
					AliasProxyTypeReference = $"{primaryProxy.Namespace}.{primaryProxy.TypeName}, {assemblyName}",
				});
			}
		}
	}

	/// <summary>
	/// Determines whether a type should use the unconditional (2-arg) TypeMap attribute.
	/// Unconditional types are always preserved by the trimmer.
	/// </summary>
	static bool IsUnconditionalEntry (JavaPeerInfo peer)
	{
		// Essential runtime types needed by the Java interop runtime
		if (EssentialRuntimeTypes.Contains (peer.JavaName)) {
			return true;
		}

		// Implementor/EventDispatcher types are only created from .NET (e.g., when a C# event
		// is subscribed). They should NOT be unconditional — they're trimmable.
		if (IsImplementorOrEventDispatcher (peer)) {
			return false;
		}

		// User-defined ACW types (not MCW bindings, not interfaces) are unconditional
		// because Android can instantiate them from Java at any time.
		if (!peer.DoNotGenerateAcw && !peer.IsInterface) {
			return true;
		}

		// Types marked unconditional by the scanner (component attributes: Activity, Service, etc.)
		if (peer.IsUnconditional) {
			return true;
		}

		return false;
	}

	/// <summary>
	/// Implementor and EventDispatcher types are generated by the binding generator
	/// and are only instantiated from .NET. They should be trimmable.
	/// NOTE: This is a name-based heuristic. Ideally the scanner would provide a dedicated flag.
	/// User types whose names happen to end in "Implementor" or "EventDispatcher" would be
	/// misclassified as trimmable. This is acceptable for now since such naming in user code
	/// is unlikely and would only affect trimming behavior, not correctness.
	/// </summary>
	static bool IsImplementorOrEventDispatcher (JavaPeerInfo peer)
	{
		return peer.ManagedTypeName.EndsWith ("Implementor", StringComparison.Ordinal) ||
			peer.ManagedTypeName.EndsWith ("EventDispatcher", StringComparison.Ordinal);
	}

	static void AddIfCrossAssembly (SortedSet<string> set, string? asmName, string outputAssemblyName)
	{
		if (asmName != null && !string.Equals (asmName, outputAssemblyName, StringComparison.Ordinal)) {
			set.Add (asmName);
		}
	}

	static JavaPeerProxyData BuildProxyType (JavaPeerInfo peer)
	{
		// Use managed type name for proxy naming to guarantee uniqueness across aliases
		// (two types with the same JNI name will have different managed names).
		var proxyTypeName = peer.ManagedTypeName.Replace ('.', '_').Replace ('+', '_') + "_Proxy";

		var proxy = new JavaPeerProxyData {
			TypeName = proxyTypeName,
			TargetType = new TypeRefData {
				ManagedTypeName = peer.ManagedTypeName,
				AssemblyName = peer.AssemblyName,
			},
			IsGenericDefinition = peer.IsGenericDefinition,
		};

		if (peer.InvokerTypeName != null) {
			proxy.InvokerType = new TypeRefData {
				ManagedTypeName = peer.InvokerTypeName,
				AssemblyName = peer.AssemblyName,
			};
		}

		if (peer.ActivationCtor != null) {
			bool isOnLeaf = string.Equals (peer.ActivationCtor.DeclaringTypeName, peer.ManagedTypeName, StringComparison.Ordinal);
			proxy.ActivationCtor = new ActivationCtorData {
				DeclaringType = new TypeRefData {
					ManagedTypeName = peer.ActivationCtor.DeclaringTypeName,
					AssemblyName = peer.ActivationCtor.DeclaringAssemblyName,
				},
				IsOnLeafType = isOnLeaf,
				Style = peer.ActivationCtor.Style,
			};
		}

		return proxy;
	}

	static TypeMapAttributeData BuildEntry (JavaPeerInfo peer, JavaPeerProxyData? proxy,
		string outputAssemblyName, string jniName)
	{
		string proxyRef;
		if (proxy != null) {
			proxyRef = $"{proxy.Namespace}.{proxy.TypeName}, {outputAssemblyName}";
		} else {
			proxyRef = $"{peer.ManagedTypeName}, {peer.AssemblyName}";
		}

		bool isUnconditional = IsUnconditionalEntry (peer);
		string? targetRef = null;
		if (!isUnconditional) {
			targetRef = $"{peer.ManagedTypeName}, {peer.AssemblyName}";
		}

		return new TypeMapAttributeData {
			JniName = jniName,
			ProxyTypeReference = proxyRef,
			TargetTypeReference = targetRef,
		};
	}
}
