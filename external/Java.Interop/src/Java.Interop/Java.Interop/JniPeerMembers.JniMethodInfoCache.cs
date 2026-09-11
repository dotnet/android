#nullable enable

using System;
using System.Collections.Concurrent;

namespace Java.Interop {

	partial class JniPeerMembers {

		static JniMethodInfo GetOrAddMethodInfo<TArg> (ConcurrentDictionary<string, JniMethodInfo> cache, string member, Func<string, TArg, JniMethodInfo> factory, TArg argument)
		{
			if (cache.TryGetValue (member, out var method))
				return method;

			// ConcurrentDictionary may invoke a GetOrAdd factory multiple times and discard
			// losing values. Construct explicitly so an unpublished StaticRedirect owner can
			// be disposed. JNI lookup can also reenter this cache, so do not lock construction.
			var candidate = factory (member, argument);
			try {
				method = cache.GetOrAdd (member, candidate);
				if (ReferenceEquals (method, candidate))
					candidate = null;
				return method;
			} finally {
				candidate?.StaticRedirect?.Dispose ();
			}
		}
	}
}
