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
		public void DisposingPeerMembersReleasesRedirect (bool isStatic)
		{
			var members = isStatic
				? new JniPeerMembers (JavaLangRemappingTestRuntime.JniTypeName, typeof (JavaLangRemappingTestRuntime))
				: new JniPeerMembers (JavaLangRemappingTestObject.JniTypeName, typeof (JavaLangRemappingTestObject));
			JniType redirect = null;
			try {
				var method = GetRedirectedMethod (members, isStatic);
				redirect = method.StaticRedirect;
				Assert.IsNotNull (redirect);
				Assert.IsTrue (redirect.PeerReference.IsValid);
				Assert.AreSame (method, GetRedirectedMethod (members, isStatic));
				AssertRedirectIsCallable (members, isStatic);

				JniPeerMembers.Dispose (members);
				Assert.IsFalse (redirect.PeerReference.IsValid, "Disposing the peer members must release the redirect's global reference.");
			} finally {
				JniPeerMembers.Dispose (members);
				redirect?.Dispose ();
			}
		}

		static unsafe void AssertRedirectIsCallable (JniPeerMembers members, bool isStatic)
		{
			if (isStatic) {
				Assert.Greater (members.StaticMethods.InvokeInt64Method ("remappedToCurrentTimeMillis.()J", null), 0);
			} else {
				using var value = new JavaLangRemappingTestObject ();
				Assert.AreEqual (value.GetHashCode (), members.InstanceMethods.InvokeNonvirtualInt32Method ("remappedToStaticHashCode.()I", value, null));
			}
		}

		static JniMethodInfo GetRedirectedMethod (JniPeerMembers members, bool isStatic)
		{
			return isStatic
				? members.StaticMethods.GetMethodInfo ("remappedToCurrentTimeMillis.()J")
				: members.InstanceMethods.GetMethodInfo ("remappedToStaticHashCode.()I");
		}

	}
}
