using System;
using System.Collections.Generic;
using System.Reflection;

using Java.Interop;
using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniPeerMembersDisposalTests : JavaVMFixture
	{
		const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

		[TestCase (false)]
		[TestCase (true)]
		public unsafe void Dispose_ReleasesSubclassCache (bool initializeOwner)
		{
			var members = new JniPeerMembers (CallNonvirtualBase.JniTypeName, typeof (CallNonvirtualBase));
			try {
				for (int cycle = 0; cycle < 3; ++cycle) {
					using var ownerType = initializeOwner ? members.JniPeerType : null;
					var peer = members.InstanceMethods.StartCreateInstance ("()V", typeof (CallNonvirtualDerived), null);
					try {
						Assert.IsTrue (peer.IsValid);
					} finally {
						JniObjectReference.Dispose (ref peer);
					}

					var constructors = members.InstanceMethods.GetConstructorsForType (typeof (CallNonvirtualDerived));
					using var subclassType = constructors.JniPeerType;
					Assert.IsTrue (subclassType.PeerReference.IsValid);
					Assert.AreEqual (JniObjectReferenceType.Global, subclassType.PeerReference.Type);
					Assert.IsTrue (IsTracked (subclassType));
					Assert.AreSame (ownerType, GetOwnerType (members));

					JniPeerMembers.Dispose (members);

					Assert.IsFalse (subclassType.PeerReference.IsValid, "Subclass class reference must be released before runtime shutdown.");
					Assert.IsFalse (IsTracked (subclassType));
					if (ownerType != null) {
						Assert.IsFalse (ownerType.PeerReference.IsValid);
						Assert.IsFalse (IsTracked (ownerType));
					}
					Assert.Throws<InvalidOperationException> (() => {
						var type = constructors.JniPeerType;
					});
					AssertUninitialized (members);

					JniPeerMembers.Dispose (members);
					AssertUninitialized (members);
				}
			} finally {
				JniPeerMembers.Dispose (members);
			}
		}

		[Test]
		public void Dispose_UninitializedMembers_RemainsLazy ()
		{
			var members = new JniPeerMembers (JavaObjectWithMissingJavaPeer.JniTypeName, typeof (JavaObjectWithMissingJavaPeer));
			try {
				AssertUninitialized (members);
				for (int i = 0; i < 3; ++i) {
					// Resolving the owning class here would throw because it does not exist.
					JniPeerMembers.Dispose (members);
					AssertUninitialized (members);
				}
			} finally {
				JniPeerMembers.Dispose (members);
			}
		}

		static JniType GetOwnerType (JniPeerMembers members)
		{
			return (JniType) GetFieldValue (typeof (JniPeerMembers).GetField ("jniPeerType", PrivateInstance), members);
		}

		static bool IsTracked (JniType type)
		{
			var tracked = (Dictionary<IntPtr, IDisposable>) GetFieldValue (
					typeof (JniRuntime).GetField ("TrackedInstances", PrivateInstance), JniEnvironment.Runtime);
			lock (tracked) {
				return tracked.ContainsValue (type);
			}
		}

		static void AssertUninitialized (JniPeerMembers members)
		{
			Assert.IsNull (GetOwnerType (members));
			Assert.IsNull (GetFieldValue (
					typeof (JniPeerMembers.JniInstanceMethods).GetField ("instanceMethods", PrivateInstance), members.InstanceMethods));
			Assert.IsNull (GetFieldValue (
					typeof (JniPeerMembers.JniInstanceMethods).GetField ("subclassConstructors", PrivateInstance), members.InstanceMethods));
			Assert.IsNull (GetFieldValue (
					typeof (JniPeerMembers.JniInstanceFields).GetField ("instanceFields", PrivateInstance), members.InstanceFields));
			Assert.IsNull (GetFieldValue (
					typeof (JniPeerMembers.JniStaticMethods).GetField ("staticMethods", PrivateInstance), members.StaticMethods));
			Assert.IsNull (GetFieldValue (
					typeof (JniPeerMembers.JniStaticFields).GetField ("staticFields", PrivateInstance), members.StaticFields));
		}

		static object GetFieldValue (FieldInfo field, object owner)
		{
			if (field == null)
				throw new InvalidOperationException ("Expected private cache field was not found.");
			return field.GetValue (owner);
		}
	}
}
