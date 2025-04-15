using System;
using System.Runtime.CompilerServices;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
#if !__ANDROID__
	// We want stability around the CallVirtualFromConstructorDerived static fields
	[NonParallelizable]
#endif  // !__ANDROID__
	public class InvokeVirtualFromConstructorTests : JavaVMFixture
	{
		[Test]
		public void CreateManagedInstanceFirst_WithAllocObject ()
		{
			CallVirtualFromConstructorDerived.Intermediate_FromActivationConstructor    = null;
			CallVirtualFromConstructorDerived.Intermediate_FromCalledFromConstructor    = null;

			using var t = new CallVirtualFromConstructorDerived (42);
			Assert.IsTrue (
					t.Called,
					"CalledFromConstructor method override should have been called.");
			Assert.IsFalse (
					t.InvokedActivationConstructor,
					"Activation Constructor should have been called, as calledFromConstructor() is invoked before ManagedPeer.construct().");
			Assert.IsTrue (
					t.InvokedConstructor,
					"(int) constructor should have been called, via ManagedPeer.construct().");

			var registered      = JniRuntime.CurrentRuntime.ValueManager.PeekValue (t.PeerReference);
			var acIntermediate  = CallVirtualFromConstructorDerived.Intermediate_FromActivationConstructor;
			var cfIntermediate  = CallVirtualFromConstructorDerived.Intermediate_FromCalledFromConstructor;

			Assert.AreSame (t, registered,
					"Expected t and registered to be the same instance; " +
					$"t={RuntimeHelpers.GetHashCode (t).ToString ("x")}, " +
					$"registered={RuntimeHelpers.GetHashCode (registered).ToString ("x")}");
			Assert.IsNull (acIntermediate,
					"Activation Constructor should not have been called, because of AllocObject semantics");
			Assert.AreSame (t, cfIntermediate,
					"Expected t and cfIntermediate to be the same instance; " +
					$"t={RuntimeHelpers.GetHashCode (t).ToString ("x")}, " +
					$"cfIntermediate={RuntimeHelpers.GetHashCode (cfIntermediate).ToString ("x")}");

			Dispose (ref CallVirtualFromConstructorDerived.Intermediate_FromActivationConstructor);
			Dispose (ref CallVirtualFromConstructorDerived.Intermediate_FromCalledFromConstructor);
		}

		static void Dispose<T> (ref T peer)
			where T : class, IJavaPeerable
		{
			if (peer == null)
				return;

			peer.Dispose ();
			peer = null;
		}

		[Test]
		public void CreateManagedInstanceFirst_WithNewObject ()
		{
			CallVirtualFromConstructorDerived.Intermediate_FromActivationConstructor    = null;
			CallVirtualFromConstructorDerived.Intermediate_FromCalledFromConstructor    = null;

			using var t = new CallVirtualFromConstructorDerived (42, useNewObject: true);
			Assert.IsFalse (
					t.Called,
					"CalledFromConstructor method override was called on a different instance.");
			Assert.IsFalse (
					t.InvokedActivationConstructor,
					"Activation Constructor should not have been called, as calledFromConstructor() is invoked before ManagedPeer.construct().");
			Assert.IsTrue (
					t.InvokedConstructor,
					"(int) constructor should have been called, via ManagedPeer.construct().");

			var registered      = JniRuntime.CurrentRuntime.ValueManager.PeekValue (t.PeerReference);
			var acIntermediate  = CallVirtualFromConstructorDerived.Intermediate_FromActivationConstructor;
			var cfIntermediate  = CallVirtualFromConstructorDerived.Intermediate_FromCalledFromConstructor;

			Assert.AreSame (t, registered,
					"Expected t and registered to be the same instance; " +
					$"t={RuntimeHelpers.GetHashCode (t).ToString ("x")}, " +
					$"registered={RuntimeHelpers.GetHashCode (registered).ToString ("x")}");
			Assert.IsNotNull (acIntermediate,
					"Activation Constructor should have been called, because of NewObject");
			Assert.IsTrue (
					acIntermediate.Called,
					"CalledFromConstructor method override should have been called on acIntermediate.");
			Assert.IsTrue (
					acIntermediate.InvokedActivationConstructor,
					"Activation Constructor should have been called on intermediate instance, as calledFromConstructor() is invoked before ManagedPeer.construct().");
			Assert.AreNotSame (t, acIntermediate,
					"Expected t and registered to be different instances; " +
					$"t={RuntimeHelpers.GetHashCode (t).ToString ("x")}, " +
					$"acIntermediate={RuntimeHelpers.GetHashCode (acIntermediate).ToString ("x")}");
			Assert.AreNotSame (t, cfIntermediate,
					"Expected t and cfIntermediate to be different instances; " +
					$"t={RuntimeHelpers.GetHashCode (t).ToString ("x")}, " +
					$"cfIntermediate={RuntimeHelpers.GetHashCode (cfIntermediate).ToString ("x")}");
			Assert.AreSame (acIntermediate, cfIntermediate,
					"Expected acIntermediate and cfIntermediate to be the same instance; " +
					$"acIntermediate={RuntimeHelpers.GetHashCode (acIntermediate).ToString ("x")}, " +
					$"cfIntermediate={RuntimeHelpers.GetHashCode (cfIntermediate).ToString ("x")}");

			Dispose (ref CallVirtualFromConstructorDerived.Intermediate_FromActivationConstructor);
			Dispose (ref CallVirtualFromConstructorDerived.Intermediate_FromCalledFromConstructor);
		}

		[Test]
		public void CreateJavaInstanceFirst ()
		{
			CallVirtualFromConstructorDerived.Intermediate_FromActivationConstructor    = null;
			CallVirtualFromConstructorDerived.Intermediate_FromCalledFromConstructor    = null;

			using var t = CallVirtualFromConstructorDerived.NewInstance (42);

			Assert.IsTrue (
					t.Called,
					"CalledFromConstructor method override should have been called.");
			Assert.IsTrue (
					t.InvokedActivationConstructor,
					"Activation Constructor should have been called, as calledFromConstructor() is invoked before ManagedPeer.construct().");
			Assert.IsTrue (
					t.InvokedConstructor,
					"(int) constructor should have been called, via ManagedPeer.construct().");

			var registered      = JniRuntime.CurrentRuntime.ValueManager.PeekValue (t.PeerReference);
			var acIntermediate  = CallVirtualFromConstructorDerived.Intermediate_FromActivationConstructor;
			var cfIntermediate  = CallVirtualFromConstructorDerived.Intermediate_FromCalledFromConstructor;

			Assert.AreSame (t, registered,
					"Expected t and registered to be the same instance; " +
					$"t={RuntimeHelpers.GetHashCode (t).ToString ("x")}, " +
					$"registered={RuntimeHelpers.GetHashCode (registered).ToString ("x")}");
			Assert.AreSame (t, acIntermediate,
					"Expected t and registered to be the same instance; " +
					$"t={RuntimeHelpers.GetHashCode (t).ToString ("x")}, " +
					$"acIntermediate={RuntimeHelpers.GetHashCode (acIntermediate).ToString ("x")}");
			Assert.AreSame (t, cfIntermediate,
					"Expected t and cfIntermediate to be the same instance; " +
					$"t={RuntimeHelpers.GetHashCode (t).ToString ("x")}, " +
					$"cfIntermediate={RuntimeHelpers.GetHashCode (cfIntermediate).ToString ("x")}");

			Dispose (ref CallVirtualFromConstructorDerived.Intermediate_FromActivationConstructor);
			Dispose (ref CallVirtualFromConstructorDerived.Intermediate_FromCalledFromConstructor);
		}
	}
}

