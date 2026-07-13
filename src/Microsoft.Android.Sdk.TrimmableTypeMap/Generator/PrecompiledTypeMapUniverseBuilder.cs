using System;
using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// A reference to a generated proxy type (assembly + full managed name) whose metadata token is
/// embedded in the precompiled blob.
/// </summary>
/// <param name="AssemblyName">Assembly containing the proxy, e.g. <c>"_App.TypeMap"</c>.</param>
/// <param name="FullTypeName">Full managed type name, e.g. <c>"_TypeMap.Proxies.Foo_Proxy"</c>.</param>
sealed record PrecompiledProxyRef (string AssemblyName, string FullTypeName);

/// <summary>
/// One precompiled typemap universe: the flattened JNI-name → proxies map (<c>GetProxyTypes</c>), the
/// managed-type → proxy map (<c>TryGetProxyType</c>), and the managed element-type → array-proxy map
/// (<c>TryGetArrayProxyType</c>), independent of any PE/token concerns.
/// </summary>
sealed class PrecompiledUniverse
{
	public List<KeyValuePair<string, List<PrecompiledProxyRef>>> External { get; } = new ();
	public List<KeyValuePair<string, PrecompiledProxyRef>> Proxy { get; } = new ();

	/// <summary>
	/// Managed element-type key → list of (0-based array rank index, array-proxy reference), one pair
	/// per rank emitted for that element type.
	/// </summary>
	public List<KeyValuePair<string, List<(int RankIndex, PrecompiledProxyRef Proxy)>>> Array { get; } = new ();
}

/// <summary>
/// Builds <see cref="PrecompiledUniverse"/> instances from the scanned <see cref="TypeMapAssemblyData"/>
/// models. The maps are derived from the model:
/// <list type="bullet">
/// <item><b>External</b> (<c>GetProxyTypes</c>): proxies grouped by <c>JniName</c>. Alias groups (2+
/// proxies sharing a JNI name) flatten naturally into a multi-value entry.</item>
/// <item><b>Proxy</b> (<c>TryGetProxyType</c>): each proxy keyed by the simplified assembly-qualified
/// name of its <c>TargetType</c> and, when present, its <c>InvokerType</c> — mirroring the runtime's
/// <c>TypeMapAssociation</c>-populated proxy map (including invoker associations).</item>
/// <item><b>Array</b> (<c>TryGetArrayProxyType</c>): each <see cref="TypeMapAssemblyData.ArrayProxyTypes"/>
/// entry keyed by the simplified assembly-qualified name of its element type, carrying the
/// (rank − 1, proxy) pair — mirroring the runtime's per-rank array maps.</item>
/// </list>
/// </summary>
static class PrecompiledTypeMapUniverseBuilder
{
	public static IReadOnlyList<PrecompiledUniverse> Build (
		IReadOnlyList<TypeMapAssemblyData> models,
		bool useSharedTypemapUniverse,
		IReadOnlyDictionary<string, HashSet<string>>? survivingProxyTypes = null)
	{
		_ = models ?? throw new ArgumentNullException (nameof (models));

		if (useSharedTypemapUniverse) {
			var universe = new UniverseAccumulator ();
			foreach (var model in models) {
				Accumulate (universe, model, survivingProxyTypes);
			}
			return new [] { universe.ToUniverse () };
		}

		var result = new List<PrecompiledUniverse> (models.Count);
		foreach (var model in models) {
			var universe = new UniverseAccumulator ();
			Accumulate (universe, model, survivingProxyTypes);
			result.Add (universe.ToUniverse ());
		}
		return result;
	}

	static void Accumulate (UniverseAccumulator universe, TypeMapAssemblyData model, IReadOnlyDictionary<string, HashSet<string>>? survivingProxyTypes)
	{
		foreach (var proxy in model.ProxyTypes) {
			string fullTypeName = $"{proxy.Namespace}.{proxy.TypeName}";

			// In the precompiled post-ILLink pass, ILLink has already removed proxy types that are
			// unreachable after linking. When we have the linked typemap assembly to check against, skip
			// any proxy that no longer exists in it so the blob never emits a TypeRef token that dangles
			// (TypeLoadException) at runtime — mirroring what TypeMapping sees, i.e. only the surviving
			// [TypeMap] attributes. If the typemap assembly isn't among the inputs (e.g. host unit tests
			// that haven't materialized it), fall back to including the proxy.
			if (survivingProxyTypes is not null &&
			    survivingProxyTypes.TryGetValue (model.AssemblyName, out var survivors) &&
			    !survivors.Contains (fullTypeName)) {
				continue;
			}

			var proxyRef = new PrecompiledProxyRef (model.AssemblyName, fullTypeName);

			universe.AddExternal (proxy.JniName, proxyRef);

			universe.AddProxy (SimplifiedKey (proxy.TargetType), proxyRef);
			if (proxy.InvokerType is { } invoker) {
				universe.AddProxy (SimplifiedKey (invoker), proxyRef);
			}
		}

		foreach (var arrayProxy in model.ArrayProxyTypes) {
			string fullTypeName = $"{arrayProxy.Namespace}.{arrayProxy.TypeName}";

			// Same post-ILLink survival filter as the peer proxies above: skip array proxies whose
			// generated type was trimmed away, so the blob never references a dangling TypeRef token.
			if (survivingProxyTypes is not null &&
			    survivingProxyTypes.TryGetValue (model.AssemblyName, out var survivors) &&
			    !survivors.Contains (fullTypeName)) {
				continue;
			}

			var proxyRef = new PrecompiledProxyRef (model.AssemblyName, fullTypeName);
			// Runtime looks up by 0-based rank index; the model stores 1-based Rank.
			universe.AddArray (SimplifiedKey (arrayProxy.ElementType), arrayProxy.Rank - 1, proxyRef);
		}
	}

	// Matches the runtime key produced from Type.AssemblyQualifiedName: "Namespace.Type, AssemblyName".
	static string SimplifiedKey (TypeRefData type) => $"{type.ManagedTypeName}, {type.AssemblyName}";

	sealed class UniverseAccumulator
	{
		readonly Dictionary<string, List<PrecompiledProxyRef>> _external = new (StringComparer.Ordinal);
		readonly List<string> _externalOrder = new ();
		readonly Dictionary<string, PrecompiledProxyRef> _proxy = new (StringComparer.Ordinal);
		readonly List<string> _proxyOrder = new ();
		readonly Dictionary<string, List<(int RankIndex, PrecompiledProxyRef Proxy)>> _array = new (StringComparer.Ordinal);
		readonly List<string> _arrayOrder = new ();

		public void AddExternal (string jniName, PrecompiledProxyRef proxyRef)
		{
			if (!_external.TryGetValue (jniName, out var list)) {
				list = new List<PrecompiledProxyRef> ();
				_external [jniName] = list;
				_externalOrder.Add (jniName);
			}
			if (!list.Contains (proxyRef)) {
				list.Add (proxyRef);
			}
		}

		public void AddProxy (string managedTypeKey, PrecompiledProxyRef proxyRef)
		{
			// First association wins; a managed type resolves to exactly one proxy.
			if (!_proxy.ContainsKey (managedTypeKey)) {
				_proxy [managedTypeKey] = proxyRef;
				_proxyOrder.Add (managedTypeKey);
			}
		}

		public void AddArray (string managedElementKey, int rankIndex, PrecompiledProxyRef proxyRef)
		{
			if (!_array.TryGetValue (managedElementKey, out var list)) {
				list = new List<(int, PrecompiledProxyRef)> ();
				_array [managedElementKey] = list;
				_arrayOrder.Add (managedElementKey);
			}
			// First association wins per (element, rank); an element+rank resolves to exactly one proxy.
			foreach (var (existingRank, _) in list) {
				if (existingRank == rankIndex) {
					return;
				}
			}
			list.Add ((rankIndex, proxyRef));
		}

		public PrecompiledUniverse ToUniverse ()
		{
			var universe = new PrecompiledUniverse ();
			foreach (var jniName in _externalOrder) {
				universe.External.Add (new KeyValuePair<string, List<PrecompiledProxyRef>> (jniName, _external [jniName]));
			}
			foreach (var key in _proxyOrder) {
				universe.Proxy.Add (new KeyValuePair<string, PrecompiledProxyRef> (key, _proxy [key]));
			}
			foreach (var key in _arrayOrder) {
				universe.Array.Add (new KeyValuePair<string, List<(int RankIndex, PrecompiledProxyRef Proxy)>> (key, _array [key]));
			}
			return universe;
		}
	}
}
