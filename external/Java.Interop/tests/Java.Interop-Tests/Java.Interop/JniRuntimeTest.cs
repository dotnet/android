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
		[RequiresDynamicCode ("This test uses ReflectionJniTypeManager, which is reflection-based and not NativeAOT-compatible.")]
		[RequiresUnreferencedCode ("This test uses ReflectionJniTypeManager, which is reflection-based and not trimming-compatible.")]
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
		JniRuntime          Proxy;

		[RequiresDynamicCode ("JniProxyRuntime uses ReflectionJniTypeManager, which is reflection-based and not NativeAOT-compatible.")]
		[RequiresUnreferencedCode ("JniProxyRuntime uses ReflectionJniTypeManager, which is reflection-based and not trimming-compatible.")]
		public JniProxyRuntime (JniRuntime proxy)
			: base (CreateOptions (proxy))
		{
			Proxy   = proxy;
		}

		[RequiresDynamicCode ("JniProxyRuntime uses ReflectionJniTypeManager, which is reflection-based and not NativeAOT-compatible.")]
		[RequiresUnreferencedCode ("JniProxyRuntime uses ReflectionJniTypeManager, which is reflection-based and not trimming-compatible.")]
		static JniRuntime.CreationOptions CreateOptions (JniRuntime proxy)
		{
			return new JniRuntime.CreationOptions {
				DestroyRuntimeOnDispose     = false,
				InvocationPointer           = proxy.InvocationPointer,
				ObjectReferenceManager      = new ProxyObjectReferenceManager (),
				ValueManager                = new ProxyValueManager (),
				TypeManager                 = new ProxyTypeManager (),
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

		[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "ProxyValueManager intentionally uses reflection-backed value manager behavior for tests.")]
		[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "ProxyValueManager intentionally uses reflection-backed value manager behavior for tests.")]
		class ProxyValueManager : ReflectionJniValueManager {

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
				return null;
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
		}

		[RequiresDynamicCode ("ProxyTypeManager uses ReflectionJniTypeManager, which is reflection-based and not NativeAOT-compatible.")]
		[RequiresUnreferencedCode ("ProxyTypeManager uses ReflectionJniTypeManager, which is reflection-based and not trimming-compatible.")]
		class ProxyTypeManager : ReflectionJniTypeManager {
			public ProxyTypeManager ()
			{
			}
		}
	}
}
