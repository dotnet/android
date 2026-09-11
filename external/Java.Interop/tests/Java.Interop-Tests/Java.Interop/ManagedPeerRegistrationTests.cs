#nullable enable
#if !__ANDROID__

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
	[NonParallelizable]
	[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "Tests exercise standalone delegate-based native registration.")]
	public class ManagedPeerRegistrationTests : JavaVMFixture {

		const string JniTypeName = "net/dot/jni/test/ManagedPeerRegistration";

		[UnmanagedFunctionPointer (CallingConvention.Winapi)]
		delegate int GetValue (IntPtr env, IntPtr klass);

		sealed class NativeTarget {
			public int Value (IntPtr env, IntPtr klass) => 42;
		}

		[Test]
		public void EmptyRegistration_AllowsNonEmptyRegistration ()
		{
			using var owner = new JniType (JniTypeName);
			owner.RegisterNativeMethods ();
			owner.RegisterNativeMethods (new JniNativeMethodRegistration ("value", "()I", new GetValue (static (env, klass) => 42)));
			Assert.AreEqual (42, Call (owner, "value"));
		}

		[Test]
		public void ManagedMarshallingFailure_DoesNotAdoptOwner ()
		{
			using var owner = new JniType (JniTypeName);
			Assert.Throws<ArgumentException> (() => owner.RegisterNativeMethods (new JniNativeMethodRegistration [1]));
			owner.DisposeUnlessRegisteredWithRuntime ();
			Assert.IsFalse (owner.PeerReference.IsValid);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void RegistrationAttempt_RetainsOwnerAndDelegate (bool fail)
		{
			var retained = RegisterAndReleaseOwner (fail);
			Collect ();
			Assert.IsTrue (retained.Owner.TryGetTarget (out var owner), "The runtime must retain the registration owner.");
			Assert.IsTrue (retained.Target.IsAlive, "The runtime must retain the registered delegate.");
			using var retainedOwner = owner ?? throw new InvalidOperationException ("The registration owner was collected.");
			Assert.IsTrue (retainedOwner.PeerReference.IsValid);
			Assert.AreEqual (42, Call (retainedOwner, "value"));
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static (WeakReference<JniType> Owner, WeakReference Target) RegisterAndReleaseOwner (bool fail)
		{
			var owner = new JniType (JniTypeName);
			var target = new NativeTarget ();
			if (fail) {
				using var error = Assert.Throws<JavaException> (() => owner.RegisterNativeMethods (
					new JniNativeMethodRegistration ("value", "()I", new GetValue (target.Value)),
					new JniNativeMethodRegistration ("missing", "()I", new GetValue (target.Value))));
			} else {
				owner.RegisterNativeMethods (new JniNativeMethodRegistration ("value", "()I", new GetValue (target.Value)));
			}
			return (new WeakReference<JniType> (owner), new WeakReference (target));
		}

		[TestCase (false)]
		[TestCase (true)]
		public void RepeatedRegistration_Throws (bool firstAttemptFails)
		{
			using var owner = new JniType (JniTypeName);
			var target = new NativeTarget ();
			if (firstAttemptFails) {
				using var error = Assert.Throws<JavaException> (() => owner.RegisterNativeMethods (
					new JniNativeMethodRegistration ("value", "()I", new GetValue (target.Value)),
					new JniNativeMethodRegistration ("missing", "()I", new GetValue (target.Value))));
			} else {
				owner.RegisterNativeMethods (new JniNativeMethodRegistration ("existing", "()I", new GetValue (target.Value)));
			}
			Assert.Throws<InvalidOperationException> (() =>
				owner.RegisterNativeMethods (new JniNativeMethodRegistration ("value", "()I", new GetValue (target.Value))));
		}

		static int Call (JniType owner, string name)
		{
			var method = owner.GetStaticMethod (name, "()I");
			return JniEnvironment.StaticMethods.CallStaticIntMethod (owner.PeerReference, method);
		}

		static void Collect ()
		{
			GC.Collect ();
			GC.WaitForPendingFinalizers ();
			GC.Collect ();
		}
	}
}
#endif // !__ANDROID__
