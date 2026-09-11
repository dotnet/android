#nullable enable

using System;
using System.Collections.Concurrent;

namespace Java.Interop {

	partial class JniPeerMembers {

		static JniMethodInfo GetOrAddMethodInfo<TArg> (ConcurrentDictionary<string, JniMethodInfo> cache, string member, Func<string, TArg, JniMethodInfo> factory, TArg argument)
		{
			if (cache.TryGetValue (member, out var method))
				return method;

			// JNI lookup can reenter this cache. Construct before publication, but retain
			// ownership of the redirect until this candidate actually wins.
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
