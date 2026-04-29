#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Android.Runtime;

/// <summary>
/// First-hit composite of multiple <c>IReadOnlyDictionary&lt;string, Type&gt;</c> sources.
/// Used to merge per-assembly per-rank array dicts in shared-universe mode without
/// enumerating <see cref="System.Runtime.InteropServices.TypeMapping"/>-backed
/// dictionaries (which only support <c>TryGetValue</c>).
/// </summary>
sealed class CompositeStringTypeReadOnlyDictionary : IReadOnlyDictionary<string, Type>
{
	readonly IReadOnlyDictionary<string, Type>?[] _sources;

	public CompositeStringTypeReadOnlyDictionary (IReadOnlyDictionary<string, Type>?[] sources)
	{
		ArgumentNullException.ThrowIfNull (sources);
		_sources = sources;
	}

	public bool TryGetValue (string key, [MaybeNullWhen (false)] out Type value)
	{
		foreach (var source in _sources) {
			if (source is not null && source.TryGetValue (key, out var v)) {
				value = v;
				return true;
			}
		}
		value = null;
		return false;
	}

	public bool ContainsKey (string key) => TryGetValue (key, out _);

	public Type this [string key] => TryGetValue (key, out var v)
		? v
		: throw new KeyNotFoundException (key);

	// Composite sources may be TypeMapping-backed dicts that throw on enumeration.
	// The trimmable typemap consumer only calls TryGetValue, so these throw to fail fast.
	public int Count => throw new NotSupportedException ();
	public IEnumerable<string> Keys => throw new NotSupportedException ();
	public IEnumerable<Type> Values => throw new NotSupportedException ();
	public IEnumerator<KeyValuePair<string, Type>> GetEnumerator () => throw new NotSupportedException ();
	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator () => throw new NotSupportedException ();
}
