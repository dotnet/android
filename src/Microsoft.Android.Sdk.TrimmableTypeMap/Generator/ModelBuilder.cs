using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Builds a <see cref="TypeMapAssemblyData"/> from scanned <see cref="JavaPeerInfo"/> records.
/// All decision logic (deduplication, alias detection, ACW filtering, 2-arg vs 3-arg attribute
/// selection, callback resolution, proxy naming) lives here.
/// The output model is a plain data structure that the emitter writes directly into a PE assembly.
/// </summary>
static class ModelBuilder
{
	const string ProxyTypeSuffix = "_Proxy";

	// Workaround for https://github.com/dotnet/runtime/issues/127004
	// When true, all TypeMap entries are emitted as 2-arg (unconditional) to avoid the
	// trimmer bug that strips TypeMapAssociation attributes when a TypeMap attribute
	// references the same type. Set to false once the runtime bug is fixed to re-enable
	// 3-arg conditional entries that allow unused framework bindings to be trimmed away.
	const bool ForceUnconditionalEntries = true;

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
			peers.Select (p => p.InvokerTypeName).OfType<string> (),
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

		var usedProxyNames = new HashSet<string> (StringComparer.Ordinal);

		foreach (var kvp in groups) {
			string jniName = kvp.Key;
			var peersForName = kvp.Value;

			// Sort aliases by managed type name for deterministic proxy naming
			if (peersForName.Count > 1) {
				peersForName.Sort ((a, b) => StringComparer.Ordinal.Compare (a.ManagedTypeName, b.ManagedTypeName));
			}

			EmitPeers (model, jniName, peersForName, assemblyName, usedProxyNames);
		}

		// Compute IgnoresAccessChecksTo from cross-assembly references
		var referencedAssemblies = new SortedSet<string> (StringComparer.Ordinal);
		foreach (var proxy in model.ProxyTypes) {
			AddIfCrossAssembly (referencedAssemblies, proxy.TargetType?.AssemblyName, assemblyName);
			foreach (var uco in proxy.UcoMethods) {
				AddIfCrossAssembly (referencedAssemblies, uco.CallbackType.AssemblyName, assemblyName);
			}
			if (proxy.ActivationCtor != null && !proxy.ActivationCtor.IsOnLeafType) {
				AddIfCrossAssembly (referencedAssemblies, proxy.ActivationCtor.DeclaringType.AssemblyName, assemblyName);
			}
		}

		// Always include Mono.Android — the emitter calls internal JNIEnv.DeleteRef
		// for JI-style activation cleanup (matching legacy TypeManager.CreateProxy behavior).
		referencedAssemblies.Add ("Mono.Android");

		model.IgnoresAccessChecksTo.AddRange (referencedAssemblies);

		return model;
	}

	static void EmitPeers (TypeMapAssemblyData model, string jniName,
		List<JavaPeerInfo> peersForName, string assemblyName, HashSet<string> usedProxyNames)
	{
		bool isAliasGroup = peersForName.Count > 1;

		if (!isAliasGroup) {
			// Single peer — no aliases needed, emit directly with the base JNI name
			var peer = peersForName [0];
			bool hasProxy = peer.ActivationCtor != null || peer.InvokerTypeName != null;
			bool isAcw = !peer.DoNotGenerateAcw && !peer.IsInterface && peer.MarshalMethods.Count > 0;

			JavaPeerProxyData? proxy = null;
			if (hasProxy) {
				proxy = BuildProxyType (peer, jniName, usedProxyNames, isAcw);
				model.ProxyTypes.Add (proxy);
			}

			var entry = BuildEntry (peer, proxy, assemblyName, jniName);
			model.Entries.Add (entry);

			// Emit a TypeMapAssociation for every entry that has a proxy.
			// The runtime's _proxyTypeMap (GetOrCreateProxyTypeMapping) is populated from
			// TypeMapAssociationAttribute — NOT from TypeMapAttribute's 3rd arg.
			// Without this, the proxy type map is empty and CreatePeer fails for
			// interface types like IIterator where targetType-based lookup is needed.
			if (proxy != null) {
				model.Associations.Add (new TypeMapAssociationData {
					SourceTypeReference = AssemblyQualify (peer.ManagedTypeName, peer.AssemblyName),
					AliasProxyTypeReference = AssemblyQualify ($"{proxy.Namespace}.{proxy.TypeName}", assemblyName),
				});
			}
			return;
		}

		// Alias group: generate an alias holder and indexed entries for each peer.
		// The base JNI name maps to the alias holder; each peer gets "[0]", "[1]", etc.
		var aliasKeys = new List<string> ();
		string holderTypeName = jniName.Replace ('/', '_').Replace ('$', '_') + "_Aliases";
		var holderNamespace = "_TypeMap.Aliases";
		string holderRef = AssemblyQualify ($"{holderNamespace}.{holderTypeName}", assemblyName);

		for (int i = 0; i < peersForName.Count; i++) {
			var peer = peersForName [i];
			string entryJniName = $"{jniName}[{i}]";
			aliasKeys.Add (entryJniName);

			bool hasProxy = peer.ActivationCtor != null || peer.InvokerTypeName != null;
			bool isAcw = !peer.DoNotGenerateAcw && !peer.IsInterface && peer.MarshalMethods.Count > 0;

			JavaPeerProxyData? proxy = null;
			if (hasProxy) {
				proxy = BuildProxyType (peer, jniName, usedProxyNames, isAcw);
				model.ProxyTypes.Add (proxy);
			}

			model.Entries.Add (BuildEntry (peer, proxy, assemblyName, entryJniName));

			// Link each alias type to the alias holder for trimming
			model.Associations.Add (new TypeMapAssociationData {
				SourceTypeReference = AssemblyQualify (peer.ManagedTypeName, peer.AssemblyName),
				AliasProxyTypeReference = holderRef,
			});
		}

		// Base JNI name entry → alias holder (self-referencing trim target, kept alive by associations)
		// When ForceUnconditionalEntries is true we MUST emit this as 2-arg (unconditional) just
		// like BuildEntry does: dotnet/runtime#127004 strips the TypeMapAssociation that keeps the
		// holder alive when a TypeMap entry references the same type, leaving the dictionary key
		// missing at runtime and breaking hierarchy lookups for essential types like
		// java/lang/String and java/lang/Object.
		bool aliasBaseUnconditional = ForceUnconditionalEntries
			|| EssentialRuntimeTypes.Contains (jniName)
			|| peersForName.Any (IsUnconditionalEntry);
		model.Entries.Add (new TypeMapAttributeData {
			JniName = jniName,
			ProxyTypeReference = holderRef,
			TargetTypeReference = aliasBaseUnconditional ? null : holderRef,
		});

		model.AliasHolders.Add (new AliasHolderData {
			TypeName = holderTypeName,
			Namespace = holderNamespace,
			AliasKeys = aliasKeys,
		});
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

	static void AddIfCrossAssembly (SortedSet<string> set, string? asmName, string outputAssemblyName)
	{
		if (asmName != null && !string.Equals (asmName, outputAssemblyName, StringComparison.Ordinal)) {
			set.Add (asmName);
		}
	}

	static string ManagedTypeNameToProxyTypeName (string managedTypeName)
	{
		var builder = new StringBuilder (managedTypeName.Length + ProxyTypeSuffix.Length);
		for (int i = 0; i < managedTypeName.Length; i++) {
			char c = managedTypeName [i];
			builder.Append (c == '.' || c == '+' || c == '`' ? '_' : c);
		}

		builder.Append (ProxyTypeSuffix);
		return builder.ToString ();
	}

	static JavaPeerProxyData BuildProxyType (JavaPeerInfo peer, string jniName, HashSet<string> usedProxyNames, bool isAcw)
	{
		// Use managed type name for proxy naming to guarantee uniqueness across aliases
		// (two types with the same JNI name will have different managed names).
		// Replace generic arity markers too, because backticks would make the emitted
		// proxy type itself look generic even though we don't emit generic parameters.
		var proxyTypeName = ManagedTypeNameToProxyTypeName (peer.ManagedTypeName);

		// Guard against name collisions (e.g., "My.Type" and "My_Type" both map to "My_Type_Proxy")
		if (!usedProxyNames.Add (proxyTypeName)) {
			int suffix = 2;
			string candidate;
			do {
				candidate = $"{proxyTypeName}_{suffix}";
				suffix++;
			} while (!usedProxyNames.Add (candidate));
			proxyTypeName = candidate;
		}

		var proxy = new JavaPeerProxyData {
			TypeName = proxyTypeName,
			JniName = jniName,
			TargetType = new TypeRefData {
				ManagedTypeName = peer.ManagedTypeName,
				AssemblyName = peer.AssemblyName,
			},
			IsAcw = isAcw,
			IsGenericDefinition = peer.IsGenericDefinition,
		};

		if (peer.InvokerTypeName != null) {
			proxy.InvokerType = new TypeRefData {
				ManagedTypeName = peer.InvokerTypeName,
				AssemblyName = peer.AssemblyName,
			};
			proxy.InvokerActivationCtorStyle = peer.InvokerActivationCtorStyle ?? ActivationCtorStyle.XamarinAndroid;
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

		if (isAcw) {
			BuildUcoMethods (peer, proxy);
			BuildUcoConstructors (peer, proxy);
			BuildNativeRegistrations (proxy);
		}

		return proxy;
	}

	static void BuildUcoMethods (JavaPeerInfo peer, JavaPeerProxyData proxy)
	{
		int ucoIndex = 0;
		for (int i = 0; i < peer.MarshalMethods.Count; i++) {
			var mm = peer.MarshalMethods [i];
			if (mm.IsConstructor) {
				continue;
			}

			proxy.UcoMethods.Add (new UcoMethodData {
				WrapperName = $"n_{mm.JniName}_uco_{ucoIndex}",
				CallbackMethodName = mm.NativeCallbackName,
				CallbackType = new TypeRefData {
					ManagedTypeName = !string.IsNullOrEmpty (mm.DeclaringTypeName) ? mm.DeclaringTypeName : peer.ManagedTypeName,
					AssemblyName = !string.IsNullOrEmpty (mm.DeclaringAssemblyName) ? mm.DeclaringAssemblyName : peer.AssemblyName,
				},
				JniSignature = mm.JniSignature,
			});
			ucoIndex++;
		}
	}

	static void BuildUcoConstructors (JavaPeerInfo peer, JavaPeerProxyData proxy)
	{
		if (peer.ActivationCtor == null || peer.JavaConstructors.Count == 0) {
			return;
		}

		foreach (var ctor in peer.JavaConstructors) {
			proxy.UcoConstructors.Add (new UcoConstructorData {
				WrapperName = $"nctor_{ctor.ConstructorIndex}_uco",
				JniSignature = ctor.JniSignature,
				TargetType = new TypeRefData {
					ManagedTypeName = peer.ManagedTypeName,
					AssemblyName = peer.AssemblyName,
				},
			});
		}
	}

	static void BuildNativeRegistrations (JavaPeerProxyData proxy)
	{
		foreach (var uco in proxy.UcoMethods) {
			proxy.NativeRegistrations.Add (new NativeRegistrationData {
				JniMethodName = uco.CallbackMethodName,
				JniSignature = uco.JniSignature,
				WrapperMethodName = uco.WrapperName,
			});
		}

		foreach (var uco in proxy.UcoConstructors) {
			string jniName = uco.WrapperName;
			int ucoSuffix = jniName.LastIndexOf ("_uco", StringComparison.Ordinal);
			if (ucoSuffix >= 0) {
				jniName = jniName.Substring (0, ucoSuffix);
			}

			proxy.NativeRegistrations.Add (new NativeRegistrationData {
				JniMethodName = jniName,
				JniSignature = uco.JniSignature,
				WrapperMethodName = uco.WrapperName,
			});
		}
	}

	static TypeMapAttributeData BuildEntry (JavaPeerInfo peer, JavaPeerProxyData? proxy,
		string outputAssemblyName, string jniName)
	{
		string proxyRef;
		if (proxy != null) {
			proxyRef = AssemblyQualify ($"{proxy.Namespace}.{proxy.TypeName}", outputAssemblyName);
		} else {
			proxyRef = AssemblyQualify (peer.ManagedTypeName, peer.AssemblyName);
		}

		// When ForceUnconditionalEntries is true, always emit 2-arg (unconditional) TypeMap
		// attributes to work around https://github.com/dotnet/runtime/issues/127004.
		bool isUnconditional = ForceUnconditionalEntries || IsUnconditionalEntry (peer);
		string? targetRef = null;
		if (!isUnconditional) {
			targetRef = AssemblyQualify (peer.ManagedTypeName, peer.AssemblyName);
		}

		return new TypeMapAttributeData {
			JniName = jniName,
			ProxyTypeReference = proxyRef,
			TargetTypeReference = targetRef,
		};
	}

	static string AssemblyQualify (string typeName, string assemblyName)
		=> $"{typeName}, {assemblyName}";
}
