using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniPeerMembersTests : JavaVMFixture
	{
		[Test]
		public void Ctor_CanReferenceNonexistentType ()
		{
			var members = new JniPeerMembers (JavaObjectWithMissingJavaPeer.JniTypeName, typeof(JavaObjectWithMissingJavaPeer));
			JniPeerMembers.Dispose (members);
		}
	}
}

