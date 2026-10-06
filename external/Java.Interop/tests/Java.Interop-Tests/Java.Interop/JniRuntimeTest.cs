using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics.CodeAnalysis;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniRuntimeTest : JavaVMFixture
	{
		[Test]
		public void CreateJavaVM ()
		{
			Assert.AreSame (JniRuntime.CurrentRuntime, JniRuntime.CurrentRuntime);
			Assert.IsTrue (JniRuntime.CurrentRuntime.InvocationPointer != IntPtr.Zero);
			Assert.IsTrue (JniEnvironment.EnvironmentPointer != IntPtr.Zero);
		}

		[Test]
		public void CreateJavaVMWithNullBuilder ()
		{
			Assert.Throws<ArgumentNullException> (() => new JavaVMWithNullBuilder ());
		}

		class JavaVMWithNullBuilder : JniRuntime {
			public JavaVMWithNullBuilder ()
				: base ((JniRuntime.CreationOptions) null)
			{
			}
		}

		[Test]
		public void Dispose_ClearsJniEnvironment ()
		{
			var c   = JniRuntime.CurrentRuntime;
			JniRuntime r    = null;
			var t   = new Thread (() => {
				r   = new JniProxyRuntime (c);
				JniRuntime.SetCurrent (r);
				Assert.AreEqual (r, JniEnvironment.Runtime);
				r.Dispose ();
				Assert.Throws<NotSupportedException>(() => {
					var env = JniEnvironment.Runtime;
				});
			});
			t.Start ();
			t.Join ();
			Assert.IsNotNull (r);
			JniRuntime.SetCurrent (c);
		}


		[Test]
		public void GetRegisteredJavaVM_ExistingInstance ()
		{
			Assert.AreEqual (JniRuntime.CurrentRuntime, JniRuntime.GetRegisteredRuntime (JniRuntime.CurrentRuntime.InvocationPointer));
		}
	}

	class JniProxyRuntime : JniRuntime
	{
		public JniProxyRuntime (JniRuntime proxy)
			: base (CreateOptions (proxy))
		{
		}

		static JniRuntime.CreationOptions CreateOptions (JniRuntime proxy)
		{
			return new JniRuntime.CreationOptions {
				DestroyRuntimeOnDispose     = false,
				InvocationPointer           = proxy.InvocationPointer,
				ObjectReferenceManager      = new ProxyObjectReferenceManager (),
				ValueManager                = new ProxyValueManager (),
				TypeManager                 = new JniTypeManager (),
			};
		}

		class ProxyObjectReferenceManager : JniObjectReferenceManager {

			public override int GlobalReferenceCount {
				get {return 1;}
			}

			public override int WeakGlobalReferenceCount {
				get {return 0;}
			}
		}

		// This runtime only tests environment disposal, not peer creation or marshaling.
		class ProxyValueManager : JniValueManager {
			const DynamicallyAccessedMemberTypes PeerConstructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

			public override void AddPeer (IJavaPeerable peer)
			{
			}

			public override void CollectPeers ()
			{
			}

			public override void FinalizePeer (IJavaPeerable peer)
			{
			}

			public override List<JniSurfacedPeerInfo>   GetSurfacedPeers ()
			{
				return [];
			}

			public override IJavaPeerable PeekPeer (JniObjectReference reference)
			{
				return null;
			}

			public override void RemovePeer (IJavaPeerable peer)
			{
			}

			public override void WaitForGCBridgeProcessing ()
			{
			}

			public override void ActivatePeer (JniObjectReference reference, Type type, ConstructorInfo cinfo, object[] argumentValues)
				=> throw new NotSupportedException ();

			protected override void ConstructPeerCore (IJavaPeerable peer, ref JniObjectReference reference, JniObjectReferenceOptions options)
				=> throw new NotSupportedException ();

			public override IJavaPeerable CreatePeer (ref JniObjectReference reference, JniObjectReferenceOptions transfer,
					[DynamicallyAccessedMembers (PeerConstructors)] Type targetType)
				=> throw new NotSupportedException ();

			protected override object CreateValueCore (ref JniObjectReference reference, JniObjectReferenceOptions options,
					[DynamicallyAccessedMembers (PeerConstructors)] Type targetType = null)
				=> throw new NotSupportedException ();

			protected override T CreateValueCore<[DynamicallyAccessedMembers (PeerConstructors)] T> (
					ref JniObjectReference reference, JniObjectReferenceOptions options,
					[DynamicallyAccessedMembers (PeerConstructors)] Type targetType = null)
				=> throw new NotSupportedException ();

			protected override object GetValueCore (ref JniObjectReference reference, JniObjectReferenceOptions options,
					[DynamicallyAccessedMembers (PeerConstructors)] Type targetType = null)
				=> throw new NotSupportedException ();

			protected override T GetValueCore<[DynamicallyAccessedMembers (PeerConstructors)] T> (
					ref JniObjectReference reference, JniObjectReferenceOptions options,
					[DynamicallyAccessedMembers (PeerConstructors)] Type targetType = null)
				=> throw new NotSupportedException ();

			protected override JniValueMarshaler GetValueMarshalerCore (Type type)
				=> throw new NotSupportedException ();

			protected override JniValueMarshaler<T> GetValueMarshalerCore<T> ()
				=> throw new NotSupportedException ();

			protected override JniObjectReference CreateLocalObjectReferenceArgumentCore (Type type, object value)
				=> throw new NotSupportedException ();
		}
	}
}
