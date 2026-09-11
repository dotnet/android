#nullable enable

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Java.Interop {

	partial class JniPeerMembers {

		private sealed class JniValueCache<TKey, TValue> : IDisposable, IReadOnlyDictionary<TKey, TValue>
			where TKey : notnull
			where TValue : class
		{

			readonly ConcurrentDictionary<TKey, TValue> values;
			readonly Action<TValue> dispose;

			public JniValueCache (int concurrencyLevel, int capacity, Action<TValue> dispose)
			{
				values       = new ConcurrentDictionary<TKey, TValue> (concurrencyLevel, capacity);
				this.dispose = dispose;
			}

			public int Count => values.Count;
			public IEnumerable<TKey> Keys => values.Keys;
			public IEnumerable<TValue> Values => values.Values;
			public TValue this [TKey key] => values [key];

			public bool ContainsKey (TKey key) => values.ContainsKey (key);
			public bool TryGetValue (TKey key, [MaybeNullWhen (false)] out TValue value) => values.TryGetValue (key, out value);

			public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator () => values.GetEnumerator ();
			IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();

			internal static JniValueCache<TKey, TValue> GetOrCreate (ref JniValueCache<TKey, TValue>? cache, int concurrencyLevel, int capacity, Action<TValue> dispose)
			{
				var value = Volatile.Read (ref cache);
				if (value != null)
					return value;

				var candidate = new JniValueCache<TKey, TValue> (concurrencyLevel, capacity, dispose);
				var existing = Interlocked.CompareExchange (ref cache, candidate, null);
				if (existing == null)
					return candidate;

				candidate.Dispose ();
				return existing;
			}

			internal static void Dispose (ref JniValueCache<TKey, TValue>? cache)
			{
				Interlocked.Exchange (ref cache, null)?.Dispose ();
			}

			public TValue GetOrAdd (TKey key, Func<TKey, TValue> factory)
			{
				return GetOrAdd (key, static (key, factory) => factory (key), factory);
			}

			public TValue GetOrAdd<TArg> (TKey key, Func<TKey, TArg, TValue> factory, TArg argument)
			{
				if (values.TryGetValue (key, out var value))
					return value;

				// ConcurrentDictionary may invoke a GetOrAdd factory multiple times and discard
				// losing values. Construct explicitly so an unpublished owner can be disposed.
				// JNI lookup can also reenter this cache, so do not lock construction.
				TValue? candidate = factory (key, argument);
				try {
					value = values.GetOrAdd (key, candidate);
					if (ReferenceEquals (value, candidate))
						candidate = null;
					return value;
				} finally {
					if (candidate != null)
						dispose (candidate);
				}
			}

			public void Dispose ()
			{
				foreach (var value in values.Values)
					dispose (value);
				values.Clear ();
			}
		}
	}
}
