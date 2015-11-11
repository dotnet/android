using System;
using System.Collections.Generic;
using System.Reflection;

using Java.Interop;
using Java.Interop.GenericMarshaler;

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

		[Test]
		public void VirtualInvokeOnBaseInvokesMostDerivedJavaMethod ()
		{
			var registered  = GetInstanceMethods (MyString._members.InstanceMethods);
			Assert.AreEqual (0, registered.Count);
			using (var s = new MyString ("hello!")) {
				Assert.AreEqual (1, registered.Count);  // for the constructor
				Assert.AreEqual ("hello!", s.ToString ());
				Assert.AreEqual (1, registered.Count);
			}
		}

		static Dictionary<string, JniInstanceMethodInfo> GetInstanceMethods (JniPeerMembers.JniInstanceMethods methods)
		{
			var f   = typeof (JniPeerMembers.JniInstanceMethods).GetField ("InstanceMethods", BindingFlags.NonPublic | BindingFlags.Instance);
			return (Dictionary<string, JniInstanceMethodInfo>) f.GetValue (methods);
		}
	}

	[JniTypeSignature (JniTypeName)]
	class MyString : JavaObject {
		internal    const   string      JniTypeName = "java/lang/String";

		internal    static  readonly    JniPeerMembers  _members    = new JniPeerMembers (JniTypeName, typeof (MyString));

		public override JniPeerMembers JniPeerMembers {
			get {return _members;}
		}

		public unsafe MyString (string value)
			: base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.Invalid)
		{
			const   string  id  = "(Ljava/lang/String;)V";
			var peer = _members.InstanceMethods.StartGenericCreateInstance (id, GetType (), value);
			using (SetPeerReference (ref peer, JniObjectReferenceOptions.DisposeSourceReference)) {
				_members.InstanceMethods.FinishGenericCreateInstance (id, this, value);
			}
		}
	}
}

