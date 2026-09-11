#nullable enable
#if !__ANDROID__

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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
		public void RepeatedRegistration_RetainsEveryDelegateBatch (bool firstAttemptFails)
		{
			var retained = RegisterRepeatedly (firstAttemptFails);
			Collect ();
			Assert.IsTrue (retained.First.IsAlive, "The first registration delegate must remain retained.");
			Assert.IsTrue (retained.Second.IsAlive, "The second registration delegate must remain retained.");
			using var owner = retained.Owner;
			Assert.AreEqual (42, Call (owner, "value"));
			Assert.AreEqual (42, Call (owner, "existing"));
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static (JniType Owner, WeakReference First, WeakReference Second) RegisterRepeatedly (bool firstAttemptFails)
		{
			var owner = new JniType (JniTypeName);
			var first = new NativeTarget ();
			var second = new NativeTarget ();
			if (firstAttemptFails) {
				using var error = Assert.Throws<JavaException> (() => owner.RegisterNativeMethods (
					new JniNativeMethodRegistration ("value", "()I", new GetValue (first.Value)),
					new JniNativeMethodRegistration ("missing", "()I", new GetValue (first.Value))));
			} else {
				owner.RegisterNativeMethods (new JniNativeMethodRegistration ("value", "()I", new GetValue (first.Value)));
			}
			owner.RegisterNativeMethods (new JniNativeMethodRegistration ("existing", "()I", new GetValue (second.Value)));
			return (owner, new WeakReference (first), new WeakReference (second));
		}

		[Test]
		public void ConcurrentRegistration_SerializesAndRetainsEveryDelegateBatch ()
		{
			var retained = RegisterConcurrently ();
			Collect ();
			Assert.IsTrue (retained.First.IsAlive, "The first registration delegate must remain retained.");
			Assert.IsTrue (retained.Second.IsAlive, "The second registration delegate must remain retained.");
			using var owner = retained.Owner;
			Assert.AreEqual (42, Call (owner, "value"));
			Assert.AreEqual (42, Call (owner, "existing"));
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static (JniType Owner, WeakReference First, WeakReference Second) RegisterConcurrently ()
		{
			var owner = new JniType (JniTypeName);
			var first = new NativeTarget ();
			var second = new NativeTarget ();
			Parallel.Invoke (
				() => owner.RegisterNativeMethods (new JniNativeMethodRegistration ("value", "()I", new GetValue (first.Value))),
				() => owner.RegisterNativeMethods (new JniNativeMethodRegistration ("existing", "()I", new GetValue (second.Value))));
			return (owner, new WeakReference (first), new WeakReference (second));
		}

		[Test]
		public void Unregister_AllowsRegistrationAgain ()
		{
			using var owner = new JniType (JniTypeName);
			owner.RegisterNativeMethods (new JniNativeMethodRegistration ("value", "()I", new GetValue (static (env, klass) => 41)));
			Assert.AreEqual (41, Call (owner, "value"));

			owner.UnregisterNativeMethods ();
			owner.RegisterNativeMethods (new JniNativeMethodRegistration ("value", "()I", new GetValue (static (env, klass) => 42)));
			Assert.AreEqual (42, Call (owner, "value"));
		}

		[Test]
		public void Unregister_ReleasesRegisteredDelegates ()
		{
			var retained = RegisterThenUnregister ();
			Collect ();
			Assert.IsFalse (retained.Target.IsAlive, "Unregistering must release retained delegates.");
			retained.Owner.Dispose ();
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static (JniType Owner, WeakReference Target) RegisterThenUnregister ()
		{
			var owner = new JniType (JniTypeName);
			var target = new NativeTarget ();
			owner.RegisterNativeMethods (new JniNativeMethodRegistration ("value", "()I", new GetValue (target.Value)));
			owner.UnregisterNativeMethods ();
			return (owner, new WeakReference (target));
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
