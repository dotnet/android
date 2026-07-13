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
/// One precompiled typemap universe: the flattened JNI-name → proxies map (<c>GetProxyTypes</c>) and
/// the managed-type → proxy map (<c>TryGetProxyType</c>), independent of any PE/token concerns.
/// </summary>
sealed class PrecompiledUniverse
{
	public List<KeyValuePair<string, List<PrecompiledProxyRef>>> External { get; } = new ();
	public List<KeyValuePair<string, PrecompiledProxyRef>> Proxy { get; } = new ();
}

/// <summary>
/// Builds <see cref="PrecompiledUniverse"/> instances from the scanned <see cref="TypeMapAssemblyData"/>
/// models. Both maps are derived from <see cref="TypeMapAssemblyData.ProxyTypes"/>:
/// <list type="bullet">
/// <item><b>External</b> (<c>GetProxyTypes</c>): proxies grouped by <c>JniName</c>. Alias groups (2+
/// proxies sharing a JNI name) flatten naturally into a multi-value entry.</item>
/// <item><b>Proxy</b> (<c>TryGetProxyType</c>): each proxy keyed by the simplified assembly-qualified
/// name of its <c>TargetType</c> and, when present, its <c>InvokerType</c> — mirroring the runtime's
/// <c>TypeMapAssociation</c>-populated proxy map (including invoker associations).</item>
/// </list>
/// Array proxies are intentionally not included yet (see <c>PrecompiledTypeMap.TryGetArrayProxyType</c>).
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
	}

	// Matches the runtime key produced from Type.AssemblyQualifiedName: "Namespace.Type, AssemblyName".
	static string SimplifiedKey (TypeRefData type) => $"{type.ManagedTypeName}, {type.AssemblyName}";

	sealed class UniverseAccumulator
	{
		readonly Dictionary<string, List<PrecompiledProxyRef>> _external = new (StringComparer.Ordinal);
		readonly List<string> _externalOrder = new ();
		readonly Dictionary<string, PrecompiledProxyRef> _proxy = new (StringComparer.Ordinal);
		readonly List<string> _proxyOrder = new ();

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

		public PrecompiledUniverse ToUniverse ()
		{
			var universe = new PrecompiledUniverse ();
			foreach (var jniName in _externalOrder) {
				universe.External.Add (new KeyValuePair<string, List<PrecompiledProxyRef>> (jniName, _external [jniName]));
			}
			foreach (var key in _proxyOrder) {
				universe.Proxy.Add (new KeyValuePair<string, PrecompiledProxyRef> (key, _proxy [key]));
			}
			return universe;
		}
	}
}
