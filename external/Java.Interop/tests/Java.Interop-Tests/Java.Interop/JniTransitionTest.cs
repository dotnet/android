using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniTransitionTests : JavaVMFixture
	{
		[Test]
		public void Dispose_ClearsLocalReferences ()
		{
			if (!HaveSafeHandles) {
				Assert.Ignore ("SafeHandles aren't used, so magical disposal from a distance isn't supported.");
				return;
			}
			JniObjectReference lref;
			using (var envp = new JniTransition (JniEnvironment.EnvironmentPointer)) {
				lref    = new JavaObject ().PeerReference.NewLocalRef ();
				Assert.IsTrue (lref.IsValid);
			}
			Assert.IsFalse (lref.IsValid);
		}
	}
}

