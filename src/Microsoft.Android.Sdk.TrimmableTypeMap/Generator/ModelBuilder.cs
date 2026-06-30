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

	static readonly PrimitiveArrayProxyInfo [] PrimitiveArrayProxies = [
		new ("Z", "Boolean", "System.Boolean", "Java.Interop.JavaBooleanArray"),
		new ("B", "SByte", "System.SByte", "Java.Interop.JavaSByteArray"),
		new ("C", "Char", "System.Char", "Java.Interop.JavaCharArray"),
		new ("S", "Int16", "System.Int16", "Java.Interop.JavaInt16Array"),
		new ("I", "Int32", "System.Int32", "Java.Interop.JavaInt32Array"),
		new ("J", "Int64", "System.Int64", "Java.Interop.JavaInt64Array"),
		new ("F", "Single", "System.Single", "Java.Interop.JavaSingleArray"),
		new ("D", "Double", "System.Double", "Java.Interop.JavaDoubleArray"),
	];

	static readonly HashSet<string> EssentialRuntimeTypes = new (StringComparer.Ordinal) {
		"java/lang/Object",
		"java/lang/Class",
		"java/lang/String",
		"java/lang/Throwable",
		"java/lang/Exception",
		"java/lang/RuntimeException",
		"java/lang/Error",
		"java/lang/Thread",
		// Queried during NativeAOT JavaInteropRuntime.init before user code can
		// reference the managed interface, so the managed→JNI mapping must survive.
		"java/lang/Thread$UncaughtExceptionHandler",
	};

	/// <summary>
	/// Builds a TypeMap assembly model for the given peers.
	/// </summary>
	/// <param name="peers">Scanned Java peer types (typically from a single input assembly).</param>
	/// <param name="outputPath">Output .dll path — used to derive assembly/module names if not specified.</param>
	/// <param name="assemblyName">Explicit assembly name. If null, derived from <paramref name="outputPath"/>.</param>
	/// <param name="maxArrayRank">
	/// Emit per-rank array <c>TypeMap</c> entries + <c>__ArrayMapRank{N}</c> sentinels
	/// for ranks 1..<paramref name="maxArrayRank"/>. 0 disables array entry emission.
	/// </param>
	public static TypeMapAssemblyData Build (IReadOnlyList<JavaPeerInfo> peers, string outputPath, string? assemblyName = null, int maxArrayRank = 0)
	{
		if (peers is null) {
			throw new ArgumentNullException (nameof (peers));
		}
		if (outputPath is null) {
			throw new ArgumentNullException (nameof (outputPath));
		}
		if (maxArrayRank < 0) {
			throw new ArgumentOutOfRangeException (nameof (maxArrayRank), maxArrayRank, "Must be >= 0.");
		}

		assemblyName ??= Path.GetFileNameWithoutExtension (outputPath);

		var model = new TypeMapAssemblyData {
			AssemblyName = assemblyName,
			ModuleName = Path.GetFileName (outputPath),
			MaxArrayRank = maxArrayRank,
		};

		// Invoker types are NOT emitted as separate proxies or TypeMap entries.
		// They are associated with their interface/abstract proxy so JniPeerMembers
		// can resolve the invoker type's registered JNI name.
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

			if (maxArrayRank > 0) {
				EmitArrayEntries (model, jniName, peersForName, maxArrayRank);
			}
		}

		if (maxArrayRank > 0 && string.Equals (assemblyName, "_Java.Interop.TypeMap", StringComparison.Ordinal)) {
			EmitPrimitiveArrayEntries (model, maxArrayRank);
		}

		BuildNativeRegistrations (model);

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
			// A concrete type that supplies its own Java peer ([JniTypeSignature(GenerateJavaPeer=false)]
			// or an MCW binding without an activation ctor) is constructed managed-side via `new`, so its
			// managed→Java JNI name must still be resolvable in order to instantiate the correct Java class.
			// Such types have neither an activation ctor nor an invoker; without a proxy + association they
			// fall back to the generic mono.android.runtime.JavaObject peer and throw ArrayStoreException
			// when stored into a typed Java array (e.g. CrossReferenceBridge[]).
			bool needsManagedToJavaName = peer.DoNotGenerateAcw && !peer.IsInterface && !peer.IsAbstract;
			bool hasProxy = peer.ActivationCtor != null || peer.InvokerTypeName != null || needsManagedToJavaName;
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
				AddProxyAssociation (model, peer, proxy, assemblyName);
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
			if (proxy != null && peer.InvokerTypeName != null) {
				AddProxyAssociation (model, peer.InvokerTypeName, peer.AssemblyName, proxy, assemblyName);
			}
		}

		// Base JNI name entry → alias holder (self-referencing trim target, kept alive by associations)
		bool aliasBaseUnconditional = EssentialRuntimeTypes.Contains (jniName)
			|| peersForName.Any (IsUnconditionalEntry);
		model.Entries.Add (new TypeMapAttributeData {
			MapKey = jniName,
			ProxyTypeReference = holderRef,
			TargetTypeReference = aliasBaseUnconditional ? null : holderRef,
		});

		model.AliasHolders.Add (new AliasHolderData {
			TypeName = holderTypeName,
			Namespace = holderNamespace,
			AliasKeys = aliasKeys,
		});
	}

	static void AddProxyAssociation (TypeMapAssemblyData model, JavaPeerInfo peer, JavaPeerProxyData proxy, string assemblyName)
	{
		AddProxyAssociation (model, peer.ManagedTypeName, peer.AssemblyName, proxy, assemblyName);
		if (peer.InvokerTypeName != null) {
			AddProxyAssociation (model, peer.InvokerTypeName, peer.AssemblyName, proxy, assemblyName);
		}
	}

	static void AddProxyAssociation (TypeMapAssemblyData model, string managedTypeName, string sourceAssemblyName, JavaPeerProxyData proxy, string outputAssemblyName)
	{
		model.Associations.Add (new TypeMapAssociationData {
			SourceTypeReference = AssemblyQualify (managedTypeName, sourceAssemblyName),
			AliasProxyTypeReference = AssemblyQualify ($"{proxy.Namespace}.{proxy.TypeName}", outputAssemblyName),
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
		if (!peer.IsFrameworkAssembly && !peer.DoNotGenerateAcw && !peer.IsInterface) {
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
		AppendSafeManagedTypeName (builder, managedTypeName);
		builder.Append (ProxyTypeSuffix);
		return builder.ToString ();
	}

	static string ManagedTypeNameToArrayProxyTypeName (string managedTypeName, int rank)
	{
		var builder = new StringBuilder (managedTypeName.Length + 20);
		AppendSafeManagedTypeName (builder, managedTypeName);
		builder.Append ("_ArrayProxy");
		builder.Append (rank);
		return builder.ToString ();
	}

	static void AppendSafeManagedTypeName (StringBuilder builder, string managedTypeName)
	{
		for (int i = 0; i < managedTypeName.Length; i++) {
			char c = managedTypeName [i];
			builder.Append (c == '.' || c == '+' || c == '`' ? '_' : c);
		}
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
			IsInterface = peer.IsInterface,
			CannotRegisterInStaticConstructor = peer.CannotRegisterInStaticConstructor,
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
				DeclaringType = peer.ActivationCtor.DeclaringType,
				IsOnLeafType = isOnLeaf,
				Style = peer.ActivationCtor.Style,
			};
		}

		if (isAcw) {
			BuildUcoMethods (peer, proxy);
			BuildUcoConstructors (peer, proxy);
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
				CallbackType = mm.DeclaringType ?? new TypeRefData {
					ManagedTypeName = !mm.DeclaringTypeName.IsNullOrEmpty () ? mm.DeclaringTypeName : peer.ManagedTypeName,
					AssemblyName = !mm.DeclaringAssemblyName.IsNullOrEmpty () ? mm.DeclaringAssemblyName : peer.AssemblyName,
				},
				JniSignature = mm.JniSignature,
				ExportMethodDispatch = (mm.IsExport || mm.CallManagedMethodDirectly) ? new ExportMethodDispatchData {
					ManagedMethodName = mm.ManagedMethodName,
					ParameterTypes = mm.ManagedParameterTypes,
					ParameterKinds = mm.ManagedParameterExportKinds,
					ReturnType = mm.ManagedReturnType,
					ReturnKind = mm.ManagedReturnExportKind,
					IsStatic = mm.IsStatic,
				} : null,
			});
			ucoIndex++;
		}
	}

	static void BuildUcoConstructors (JavaPeerInfo peer, JavaPeerProxyData proxy)
	{
		if (peer.ActivationCtor == null || peer.JavaConstructors.Count == 0) {
			return;
		}

		// Abstract types are never directly instantiated from Java — the ACW
		// constructor's getClass() guard prevents activation. Skip generating
		// UCO constructor wrappers for them.
		if (peer.IsAbstract) {
			return;
		}

		foreach (var ctor in peer.JavaConstructors) {
			if (ctor.SuperArgumentsString != null && !ctor.HasMatchingManagedCtor) {
				throw new InvalidOperationException (
					$"Trimmable typemap cannot generate Java constructor wrapper '{ctor.JniSignature}' for '{peer.ManagedTypeName}' because no matching user-visible managed constructor was found.");
			}
			proxy.UcoConstructors.Add (new UcoConstructorData {
				WrapperName = $"nctor_{ctor.ConstructorIndex}_uco",
				JniSignature = ctor.JniSignature,
				TargetType = new TypeRefData {
					ManagedTypeName = peer.ManagedTypeName,
					AssemblyName = peer.AssemblyName,
				},
				ManagedParameterTypes = ctor.ManagedParameterTypes,
				HasMatchingManagedCtor = ctor.HasMatchingManagedCtor,
			});
		}
	}

	static void BuildNativeRegistrations (TypeMapAssemblyData model)
	{
		var sharedWrapperTargets = new Dictionary<UcoWrapperReuseKey, UcoWrapperTargetData> ();
		foreach (var proxy in model.ProxyTypes) {
			foreach (var uco in proxy.UcoMethods) {
				if (!CanShareUcoWrapper (proxy, uco)) {
					continue;
				}

				var reuseKey = CreateUcoWrapperReuseKey (uco);
				if (!sharedWrapperTargets.ContainsKey (reuseKey)) {
					sharedWrapperTargets.Add (reuseKey, UcoWrapperTargetData.From (proxy, uco.WrapperName));
				}
			}
		}

		foreach (var proxy in model.ProxyTypes) {
			var reusedUcoMethods = new HashSet<UcoMethodData> ();

			foreach (var uco in proxy.UcoMethods) {
				var wrapperTarget = UcoWrapperTargetData.From (proxy, uco.WrapperName);
				if (CanReuseUcoWrapper (proxy, uco) &&
				    sharedWrapperTargets.TryGetValue (CreateUcoWrapperReuseKey (uco), out var sharedWrapperTarget)) {
					wrapperTarget = sharedWrapperTarget;
					reusedUcoMethods.Add (uco);
				}
				proxy.NativeRegistrations.Add (new NativeRegistrationData {
					JniMethodName = uco.CallbackMethodName,
					JniSignature = uco.JniSignature,
					WrapperMethodName = wrapperTarget.MethodName,
					WrapperTarget = wrapperTarget,
				});
			}

			if (reusedUcoMethods.Count > 0) {
				proxy.UcoMethods.RemoveAll (uco => reusedUcoMethods.Contains (uco));
			}

			foreach (var uco in proxy.UcoConstructors) {
				string jniName = uco.WrapperName;
				int ucoSuffix = jniName.LastIndexOf ("_uco", StringComparison.Ordinal);
				if (ucoSuffix >= 0) {
					jniName = jniName.Substring (0, ucoSuffix);
				}

				var wrapperTarget = UcoWrapperTargetData.From (proxy, uco.WrapperName);
				proxy.NativeRegistrations.Add (new NativeRegistrationData {
					JniMethodName = jniName,
					JniSignature = uco.JniSignature,
					WrapperMethodName = wrapperTarget.MethodName,
					WrapperTarget = wrapperTarget,
				});
			}
		}
	}

	static bool CanShareUcoWrapper (JavaPeerProxyData proxy, UcoMethodData uco)
	{
		return IsUcoWrapperReuseCandidate (proxy, uco) && IsCallbackOwnedByProxy (proxy, uco);
	}

	static bool CanReuseUcoWrapper (JavaPeerProxyData proxy, UcoMethodData uco)
	{
		return IsUcoWrapperReuseCandidate (proxy, uco) && !IsCallbackOwnedByProxy (proxy, uco);
	}

	static bool IsUcoWrapperReuseCandidate (JavaPeerProxyData proxy, UcoMethodData uco)
	{
		return !uco.UsesExportMethodDispatch &&
			!proxy.IsGenericDefinition &&
			!uco.CallbackType.ManagedTypeName.Contains ('`');
	}

	static bool IsCallbackOwnedByProxy (JavaPeerProxyData proxy, UcoMethodData uco)
	{
		return string.Equals (uco.CallbackType.ManagedTypeName, proxy.TargetType.ManagedTypeName, StringComparison.Ordinal) &&
			string.Equals (uco.CallbackType.AssemblyName, proxy.TargetType.AssemblyName, StringComparison.Ordinal);
	}

	static UcoWrapperReuseKey CreateUcoWrapperReuseKey (UcoMethodData uco)
	{
		return new UcoWrapperReuseKey (
			uco.CallbackType.ManagedTypeName,
			uco.CallbackType.AssemblyName,
			uco.CallbackMethodName,
			uco.JniSignature);
	}

	readonly record struct UcoWrapperReuseKey (
		string CallbackTypeName,
		string CallbackAssemblyName,
		string CallbackMethodName,
		string JniSignature);

	static TypeMapAttributeData BuildEntry (JavaPeerInfo peer, JavaPeerProxyData? proxy,
		string outputAssemblyName, string jniName)
	{
		string proxyRef;
		if (proxy != null) {
			proxyRef = AssemblyQualify ($"{proxy.Namespace}.{proxy.TypeName}", outputAssemblyName);
		} else {
			proxyRef = AssemblyQualify (peer.ManagedTypeName, peer.AssemblyName);
		}

		bool isUnconditional = IsUnconditionalEntry (peer);
		string? targetRef = null;
		if (!isUnconditional) {
			targetRef = AssemblyQualify (peer.ManagedTypeName, peer.AssemblyName);
		}

		return new TypeMapAttributeData {
			MapKey = jniName,
			ProxyTypeReference = proxyRef,
			TargetTypeReference = targetRef,
		};
	}

	static string AssemblyQualify (string typeName, string assemblyName)
		=> $"{typeName}, {assemblyName}";

	static string AddArrayRank (string typeReference, int rank)
	{
		if (rank == 0) {
			return typeReference;
		}

		int assemblySeparator = typeReference.LastIndexOf (", ", StringComparison.Ordinal);
		if (assemblySeparator < 0) {
			throw new InvalidOperationException ($"Assembly-qualified type reference '{typeReference}' does not contain an assembly name.");
		}

		return typeReference.Substring (0, assemblySeparator) + Brackets (rank) + typeReference.Substring (assemblySeparator);
	}

	static string MakeGenericTypeReference (string openTypeName, string openTypeAssembly, string argumentTypeReference)
		=> $"{openTypeName}[[{argumentTypeReference}]], {openTypeAssembly}";

	static string MakeNestedJavaObjectArrayTypeReference (string elementTypeReference, int rank)
	{
		var result = elementTypeReference;
		for (int i = 0; i < rank; i++) {
			result = MakeGenericTypeReference ("Java.Interop.JavaObjectArray`1", "Java.Interop", result);
		}
		return result;
	}

	static IReadOnlyList<string> GetArrayTypeReferences (ArrayProxyData proxy)
	{
		var elementType = AssemblyQualify (proxy.ElementType.ManagedTypeName, proxy.ElementType.AssemblyName);
		if (proxy.Primitive is null) {
			var rankOneTypes = new [] {
				MakeGenericTypeReference ("Java.Interop.JavaObjectArray`1", "Java.Interop", elementType),
				MakeGenericTypeReference ("Java.Interop.JavaArray`1", "Java.Interop", elementType),
				AddArrayRank (elementType, 1),
			};
			return ExpandRankOneTypes (rankOneTypes, proxy.Rank);
		}

		var rankOnePrimitiveTypes = new [] {
			AddArrayRank (elementType, 1),
			MakeGenericTypeReference ("Java.Interop.JavaArray`1", "Java.Interop", elementType),
			MakeGenericTypeReference ("Java.Interop.JavaPrimitiveArray`1", "Java.Interop", elementType),
			AssemblyQualify (proxy.Primitive.ConcreteArrayType.ManagedTypeName, proxy.Primitive.ConcreteArrayType.AssemblyName),
		};
		return ExpandRankOneTypes (rankOnePrimitiveTypes, proxy.Rank);
	}

	static IReadOnlyList<string> ExpandRankOneTypes (IReadOnlyList<string> rankOneTypes, int rank)
	{
		if (rank == 1) {
			return rankOneTypes;
		}

		var result = new List<string> (rankOneTypes.Count * 2);
		foreach (var type in rankOneTypes) {
			result.Add (MakeNestedJavaObjectArrayTypeReference (type, rank - 1));
			result.Add (AddArrayRank (type, rank - 1));
		}
		return result;
	}

	static void AddArrayProxyAssociations (TypeMapAssemblyData model, ArrayProxyData proxy, string proxyReference)
	{
		foreach (var typeReference in GetArrayTypeReferences (proxy)) {
			model.Associations.Add (new TypeMapAssociationData {
				SourceTypeReference = typeReference,
				AliasProxyTypeReference = proxyReference,
				AnchorRank = proxy.Rank,
			});
		}
	}

	static string GetArrayProxyMapKey (TypeRefData elementType)
		=> AssemblyQualify (elementType.ManagedTypeName, elementType.AssemblyName);

	/// <summary>
	/// Emits per-rank array TypeMap entries for one peer, anchored to the per-assembly
	/// <c>__ArrayMapRank{N}</c> sentinels. Keys are managed element type names (rank is encoded
	/// by the sentinel anchor, not by JNI array prefixes). Skips open generics and alias groups.
	/// </summary>
	static void EmitArrayEntries (TypeMapAssemblyData model, string jniName, List<JavaPeerInfo> peersForName, int maxArrayRank)
	{
		if (peersForName.Count != 1) {
			return;
		}

		var peer = peersForName [0];
		if (!peer.GenerateArrayEntries) {
			return;
		}
		if (peer.IsGenericDefinition) {
			return;
		}
		if (jniName.Length == 1 && IsJniPrimitiveKeyword (jniName [0])) {
			return;
		}

		for (int rank = 1; rank <= maxArrayRank; rank++) {
			var proxy = new ArrayProxyData {
				TypeName = ManagedTypeNameToArrayProxyTypeName (peer.ManagedTypeName, rank),
				ElementType = new TypeRefData {
					ManagedTypeName = peer.ManagedTypeName,
					AssemblyName = peer.AssemblyName,
				},
				Rank = rank,
			};
			model.ArrayProxyTypes.Add (proxy);

			var proxyReference = AssemblyQualify ($"{proxy.Namespace}.{proxy.TypeName}", model.AssemblyName);
			model.Entries.Add (new TypeMapAttributeData {
				MapKey = GetArrayProxyMapKey (proxy.ElementType),
				ProxyTypeReference = proxyReference,
				TargetTypeReference = proxyReference,
				AnchorRank = rank,
			});
			AddArrayProxyAssociations (model, proxy, proxyReference);
		}
	}

	static void EmitPrimitiveArrayEntries (TypeMapAssemblyData model, int maxArrayRank)
	{
		foreach (var primitive in PrimitiveArrayProxies) {
			for (int rank = 1; rank <= maxArrayRank; rank++) {
				var proxy = new ArrayProxyData {
					TypeName = $"Primitive_{primitive.Name}_ArrayProxy{rank}",
					ElementType = new TypeRefData {
						ManagedTypeName = primitive.ManagedTypeName,
						AssemblyName = "System.Runtime",
					},
					Rank = rank,
					Primitive = new PrimitiveArrayProxyData {
						ConcreteArrayType = new TypeRefData {
							ManagedTypeName = primitive.ConcreteArrayTypeName,
							AssemblyName = "Java.Interop",
						},
					},
				};
				model.ArrayProxyTypes.Add (proxy);
				var proxyReference = AssemblyQualify ($"{proxy.Namespace}.{proxy.TypeName}", model.AssemblyName);
				model.Entries.Add (new TypeMapAttributeData {
					MapKey = GetArrayProxyMapKey (proxy.ElementType),
					ProxyTypeReference = proxyReference,
					TargetTypeReference = proxyReference,
					AnchorRank = rank,
				});
				AddArrayProxyAssociations (model, proxy, proxyReference);
			}
		}
	}

	static string Brackets (int rank) => rank switch {
		1 => "[]",
		2 => "[][]",
		3 => "[][][]",
		_ => BuildBrackets (rank),
	};

	static string BuildBrackets (int rank)
	{
		var sb = new StringBuilder (rank * 2);
		for (int i = 0; i < rank; i++) {
			sb.Append ("[]");
		}
		return sb.ToString ();
	}

	static bool IsJniPrimitiveKeyword (char c)
		=> c == 'Z' || c == 'B' || c == 'C' || c == 'S' || c == 'I'
			|| c == 'J' || c == 'F' || c == 'D' || c == 'V';

	readonly record struct PrimitiveArrayProxyInfo (
		string JniName,
		string Name,
		string ManagedTypeName,
		string ConcreteArrayTypeName);
}
