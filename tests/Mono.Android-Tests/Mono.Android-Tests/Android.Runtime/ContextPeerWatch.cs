using System;

using Android.App;

using Java.Interop;

using NUnit.Framework;
using NUnit.Framework.Interfaces;

[assembly: Android.RuntimeTests.ContextPeerWatchAttribute]

namespace Android.RuntimeTests {

	// `Application.Context` caches one managed peer for the lifetime of the process, so
	// `JniValueManager.PeekPeer()` should keep returning that same peer forever.  On CoreCLR it
	// intermittently stops doing so, and the first test to *notice* is
	// `JnienvArrayMarshaling.GetObjectArray()` -- which is not necessarily the test that broke
	// it.  Diagnostics there confirmed the registry was already wrong on entry, so sample after
	// every test to name the test we were actually running when it diverged.
	[AttributeUsage (AttributeTargets.Assembly)]
	public sealed class ContextPeerWatchAttribute : Attribute, ITestAction {

		// Only the *first* divergence is interesting; later tests just observe the same
		// already-broken registry.
		public static string DivergedAfter;

		public ActionTargets Targets => ActionTargets.Test;

		public void BeforeTest (ITest test)
		{
		}

		public void AfterTest (ITest test)
		{
			if (DivergedAfter != null)
				return;

			var context = Application.Context;
			if (context == null || !context.PeerReference.IsValid)
				return;

			// `PeekPeer()` only reads the registry -- in particular it does not drain the
			// collected-peer queue the way `GetSurfacedPeers()` does -- so sampling it this
			// often does not itself perturb the behaviour under investigation.
			var peeked = JniRuntime.CurrentRuntime.ValueManager.PeekPeer (context.PeerReference);
			if (!ReferenceEquals (peeked, context))
				DivergedAfter = test.FullName;
		}
	}
}
