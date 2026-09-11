using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Java.Interop;
using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	[Category ("NativeAOTIgnore")]
	public class JniRedirectCacheOwnershipTests : JavaVMFixture
	{
		[TestCase (false)]
		[TestCase (true)]
		public void DisposingMethodCacheReleasesRedirect (bool isStatic)
		{
			var members = isStatic
				? new JniPeerMembers (IAndroidInterface.JniTypeName, typeof (IAndroidInterface))
				: new JniPeerMembers (JavaLangRemappingTestObject.JniTypeName, typeof (JavaLangRemappingTestObject));
			try {
				for (int i = 0; i < 3; ++i) {
					var method = GetRedirectedMethod (members, isStatic);
					var redirect = method.StaticRedirect;
					Assert.IsNotNull (redirect);
					Assert.IsTrue (redirect.PeerReference.IsValid);
					Assert.AreSame (method, GetRedirectedMethod (members, isStatic));

					try {
						AssertRedirectIsCallable (members, isStatic);
						if (isStatic)
							members.StaticMethods.Dispose ();
						else
							members.InstanceMethods.Dispose ();
						Assert.IsFalse (redirect.PeerReference.IsValid, "The method cache owns the redirect's global reference.");
						if (isStatic)
							members.StaticMethods.Dispose ();
						else
							members.InstanceMethods.Dispose ();
					} finally {
						redirect.Dispose ();
					}
				}
			} finally {
				JniPeerMembers.Dispose (members);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void DisposingOrdinaryMethodCacheDoesNotDisposePeerType (bool isStatic)
		{
			var members = new JniPeerMembers (JavaLangRemappingTestRuntime.JniTypeName, typeof (JavaLangRemappingTestRuntime));
			try {
				var peerType = members.JniPeerType;
				var method = isStatic
					? members.StaticMethods.GetMethodInfo ("getRuntime.()Ljava/lang/Runtime;")
					: members.InstanceMethods.GetMethodInfo ("hashCode.()I");
				Assert.IsNull (method.StaticRedirect);

				if (isStatic)
					members.StaticMethods.Dispose ();
				else
					members.InstanceMethods.Dispose ();
				Assert.IsTrue (peerType.PeerReference.IsValid);
			} finally {
				JniPeerMembers.Dispose (members);
			}
		}

		[Test]
		public void ConcurrentPublicationDisposesOnlyLosingRedirects ()
		{
			using var cache = new JniPeerMembers.JniMethodInfoCache (1, 3);
			var candidates = new JniMethodInfo [2];
			var results = new JniMethodInfo [candidates.Length];
			using var ready = new Barrier (candidates.Length);
			try {
				Parallel.For (0, candidates.Length, i => {
					results [i] = cache.GetOrAdd ("currentTimeMillis.()J", (member, index) => {
						candidates [index] = CreateRedirect ();
						if (!ready.SignalAndWait (TimeSpan.FromSeconds (30)))
							throw new TimeoutException ("Both candidates must be created before publication.");
						return candidates [index];
					}, i);
				});
				var winner = results [0];
				Assert.AreSame (winner, results [1]);
				Assert.AreSame (winner, results [1]);
				foreach (var candidate in candidates)
					Assert.AreEqual (ReferenceEquals (candidate, winner), candidate.StaticRedirect.PeerReference.IsValid);
				AssertSystemRedirectIsCallable (winner);
				Assert.AreSame (winner, cache.GetOrAdd ("currentTimeMillis.()J",
					(member, state) => throw new InvalidOperationException ("A cache hit must not construct a candidate."), 0));
			} finally {
				foreach (var candidate in candidates)
					candidate?.StaticRedirect?.Dispose ();
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void ReentrantPublicationPreservesWinner (bool returnWinner)
		{
			using var cache = new JniPeerMembers.JniMethodInfoCache (1, 3);
			var outer = CreateRedirect ();
			var inner = CreateRedirect ();
			try {
				var method = cache.GetOrAdd ("currentTimeMillis.()J", (member, state) => {
					var winner = cache.GetOrAdd (member, (key, argument) => inner, state);
					return returnWinner ? winner : outer;
				}, 0);

				Assert.AreSame (inner, method);
				Assert.AreEqual (returnWinner, outer.StaticRedirect.PeerReference.IsValid);
				AssertSystemRedirectIsCallable (inner);
			} finally {
				outer.StaticRedirect.Dispose ();
				inner.StaticRedirect.Dispose ();
			}
		}

		[Test]
		public void PublicationFailureDisposesCandidate ()
		{
			var comparer = new PublicationFailureComparer ();
			using var cache = new JniPeerMembers.JniMethodInfoCache (comparer);
			var candidate = CreateRedirect ();
			try {
				var error = Assert.Throws<InvalidOperationException> (() =>
					cache.GetOrAdd ("currentTimeMillis.()J", (member, state) => {
						comparer.Fail = true;
						return candidate;
					}, 0));
				Assert.AreEqual ("Publication failed.", error.Message);
				Assert.IsFalse (candidate.StaticRedirect.PeerReference.IsValid);
			} finally {
				candidate.StaticRedirect.Dispose ();
			}
		}

		static JniMethodInfo CreateRedirect ()
		{
			var type = new JniType ("java/lang/System");
			try {
				var method = type.GetStaticMethod ("currentTimeMillis", "()J");
				method.StaticRedirect = type;
				type = null;
				return method;
			} finally {
				type?.Dispose ();
			}
		}

		static unsafe void AssertSystemRedirectIsCallable (JniMethodInfo method)
		{
			Assert.IsTrue (method.StaticRedirect.PeerReference.IsValid);
			Assert.Greater (JniEnvironment.StaticMethods.CallStaticLongMethod (method.StaticRedirect.PeerReference, method, null), 0);
		}

		static unsafe void AssertRedirectIsCallable (JniPeerMembers members, bool isStatic)
		{
			if (isStatic) {
				var value = members.StaticMethods.InvokeObjectMethod ("getClassName.()Ljava/lang/String;", null);
				Assert.AreEqual ("DesugarAndroidInterface$-CC", JniEnvironment.Strings.ToString (ref value, JniObjectReferenceOptions.CopyAndDispose));
			} else {
				using var value = new JavaLangRemappingTestObject ();
				Assert.AreEqual (value.GetHashCode (), members.InstanceMethods.InvokeNonvirtualInt32Method ("remappedToStaticHashCode.()I", value, null));
			}
		}

		static JniMethodInfo GetRedirectedMethod (JniPeerMembers members, bool isStatic)
		{
			return isStatic
				? members.StaticMethods.GetMethodInfo ("getClassName.()Ljava/lang/String;")
				: members.InstanceMethods.GetMethodInfo ("remappedToStaticHashCode.()I");
		}

		sealed class PublicationFailureComparer : IEqualityComparer<string>
		{
			public bool Fail;

			public bool Equals (string x, string y) => StringComparer.Ordinal.Equals (x, y);

			public int GetHashCode (string value)
			{
				if (Fail)
					throw new InvalidOperationException ("Publication failed.");
				return StringComparer.Ordinal.GetHashCode (value);
			}
		}
	}
}
