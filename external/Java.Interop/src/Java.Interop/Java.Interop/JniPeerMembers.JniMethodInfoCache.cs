#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Java.Interop {

	partial class JniPeerMembers {

		private sealed class JniMethodInfoCache : IDisposable {

			readonly ConcurrentDictionary<string, JniMethodInfo> methods;

			public JniMethodInfoCache (int concurrencyLevel, int capacity)
			{
				methods = new ConcurrentDictionary<string, JniMethodInfo> (concurrencyLevel, capacity);
			}

			internal static JniMethodInfoCache GetOrCreate (ref JniMethodInfoCache? cache, int concurrencyLevel, int capacity)
			{
				var value = Volatile.Read (ref cache);
				if (value != null)
					return value;

				var candidate = new JniMethodInfoCache (concurrencyLevel, capacity);
				var existing = Interlocked.CompareExchange (ref cache, candidate, null);
				if (existing == null)
					return candidate;

				candidate.Dispose ();
				return existing;
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
