#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Java.Interop {

	partial class JniPeerMembers {

		internal sealed class JniMethodInfoCache : IDisposable {

			readonly ConcurrentDictionary<string, JniMethodInfo> methods;

			public JniMethodInfoCache (int concurrencyLevel, int capacity)
			{
				methods = new ConcurrentDictionary<string, JniMethodInfo> (concurrencyLevel, capacity);
			}

			internal JniMethodInfoCache (int concurrencyLevel, int capacity, IEqualityComparer<string> comparer)
			{
				methods = new ConcurrentDictionary<string, JniMethodInfo> (concurrencyLevel, capacity, comparer);
			}

			internal static JniMethodInfoCache GetOrCreate (ref JniMethodInfoCache? cache, int concurrencyLevel, int capacity)
			{
				var value = Volatile.Read (ref cache);
				if (value != null)
					return value;

				var candidate = new JniMethodInfoCache (concurrencyLevel, capacity);
				return Interlocked.CompareExchange (ref cache, candidate, null) ?? candidate;
			}

			internal static void Dispose (ref JniMethodInfoCache? cache)
			{
				Interlocked.Exchange (ref cache, null)?.Dispose ();
			}

			public JniMethodInfo GetOrAdd<TArg> (string member, Func<string, TArg, JniMethodInfo> factory, TArg argument)
			{
				if (methods.TryGetValue (member, out var method))
					return method;

				// ConcurrentDictionary may invoke a GetOrAdd factory multiple times and discard
				// losing values. Construct explicitly so an unpublished StaticRedirect owner can
				// be disposed. JNI lookup can also reenter this cache, so do not lock construction.
				var candidate = factory (member, argument);
				try {
					method = methods.GetOrAdd (member, candidate);
					if (ReferenceEquals (method, candidate))
						candidate = null;
					return method;
				} finally {
					candidate?.StaticRedirect?.Dispose ();
				}
			}

			public void Dispose ()
			{
				foreach (var method in methods.Values)
					method.StaticRedirect?.Dispose ();
				methods.Clear ();
			}
		}
	}
}
