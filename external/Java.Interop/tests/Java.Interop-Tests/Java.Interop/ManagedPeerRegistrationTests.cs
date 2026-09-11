#nullable enable
#if !__ANDROID__

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
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
		public void EmptyRegistration_DoesNotAdoptOwner ()
		{
			using var owner = new JniType (JniTypeName);
			owner.RegisterNativeMethods ();
			Assert.IsFalse (owner.IsRegisteredWithRuntime);
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
		public void RepeatedRegistration_RetainsPreviousAndAttemptedDelegates (bool fail)
		{
			using var owner = new JniType (JniTypeName);
			var targets = RegisterRepeatedly (owner, fail);
			Collect ();
			Assert.IsTrue (targets.Previous.IsAlive, "The previous registration must remain rooted.");
			Assert.IsTrue (targets.Attempted.IsAlive, "The partial registration must remain rooted.");
			Assert.AreEqual (42, Call (owner, "existing"));
			Assert.AreEqual (42, Call (owner, "value"));
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static (WeakReference Previous, WeakReference Attempted) RegisterRepeatedly (JniType owner, bool fail)
		{
			var previous = new NativeTarget ();
			var attempted = new NativeTarget ();
			owner.RegisterNativeMethods (new JniNativeMethodRegistration ("existing", "()I", new GetValue (previous.Value)));
			if (fail) {
				using var error = Assert.Throws<JavaException> (() => owner.RegisterNativeMethods (
					new JniNativeMethodRegistration ("value", "()I", new GetValue (attempted.Value)),
					new JniNativeMethodRegistration ("missing", "()I", new GetValue (attempted.Value))));
			} else {
				owner.RegisterNativeMethods (new JniNativeMethodRegistration ("value", "()I", new GetValue (attempted.Value)));
			}
			return (new WeakReference (previous), new WeakReference (attempted));
		}

		[Test]
		public void ConcurrentRegistration_RetainsDelegates ()
		{
			using var owner = new JniType (JniTypeName);
			using var barrier = new Barrier (2);
			var first = Task.Factory.StartNew (() => RegisterConcurrently (owner, "existing", barrier),
				CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
			var second = Task.Factory.StartNew (() => RegisterConcurrently (owner, "value", barrier),
				CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
			var targets = Task.WhenAll (first, second).GetAwaiter ().GetResult ();
			Collect ();
			Assert.IsTrue (targets [0].IsAlive);
			Assert.IsTrue (targets [1].IsAlive);
			Assert.AreEqual (42, Call (owner, "existing"));
			Assert.AreEqual (42, Call (owner, "value"));
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static WeakReference RegisterConcurrently (JniType owner, string name, Barrier barrier)
		{
			var target = new NativeTarget ();
			Assert.IsTrue (barrier.SignalAndWait (TimeSpan.FromSeconds (30)), "Concurrent registrars did not reach the barrier.");
			owner.RegisterNativeMethods (new JniNativeMethodRegistration (name, "()I", new GetValue (target.Value)));
			return new WeakReference (target);
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
