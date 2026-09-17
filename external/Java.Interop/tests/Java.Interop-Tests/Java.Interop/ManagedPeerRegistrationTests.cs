#nullable enable
#if !__ANDROID__

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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

		[TestCase (false)]
		[TestCase (true)]
		public void Dispose_PreventsConcurrentRegistration (bool registerFirst)
		{
			var runtime = JniEnvironment.Runtime;
			var original = runtime.ObjectReferenceManager;
			var references = new BlockingDeleteReferenceManager (original);
			var property = typeof (JniRuntime).GetProperty (nameof (JniRuntime.ObjectReferenceManager)) ??
				throw new InvalidOperationException ("Could not replace the JNI object-reference manager.");
			property.SetValue (runtime, references);

			JniType? owner = null;
			Task? dispose = null;
			IntPtr handle = IntPtr.Zero;
			bool registrationCompleted = false;
			try {
				var registrationOwner = new JniType (JniTypeName);
				owner = registrationOwner;
				handle = registrationOwner.PeerReference.Handle;
				if (registerFirst) {
					registrationOwner.RegisterNativeMethods (
						new JniNativeMethodRegistration ("value", "()I", new GetValue (static (env, klass) => 41)));
					Assert.AreEqual (41, Call (registrationOwner, "value"));
					Assert.IsTrue (IsTracked (runtime, handle));
				}
				references.Block (handle);
				var disposeTask = Task.Factory.StartNew (
					() => {
						runtime.AttachCurrentThread ();
						registrationOwner.Dispose ();
					},
					CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
				dispose = disposeTask;
				Assert.IsTrue (references.DeleteStarted.Wait (TimeSpan.FromSeconds (30)), "Disposal did not reach global-reference deletion.");

				Assert.Throws<ObjectDisposedException> (() => {
					registrationOwner.RegisterNativeMethods (
						new JniNativeMethodRegistration (
							registerFirst ? "existing" : "value",
							"()I",
							new GetValue (static (env, klass) => 42)));
					registrationCompleted = true;
				});
				references.ReleaseDelete.Set ();
				disposeTask.GetAwaiter ().GetResult ();

				Assert.IsFalse (registrationOwner.PeerReference.IsValid);
				Assert.IsFalse (IsTracked (runtime, handle));
			} finally {
				references.ReleaseDelete.Set ();
				if (dispose != null)
					dispose.GetAwaiter ().GetResult ();
				property.SetValue (runtime, original);

				if (handle != IntPtr.Zero && IsTracked (runtime, handle))
					runtime.UnTrack (handle);
				if (registrationCompleted) {
					using var cleanup = new JniType (JniTypeName);
					cleanup.UnregisterNativeMethods ();
				}
				owner?.Dispose ();
			}
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

		static bool IsTracked (JniRuntime runtime, IntPtr handle)
		{
			var field = typeof (JniRuntime).GetField ("TrackedInstances", BindingFlags.NonPublic | BindingFlags.Instance);
			if (field?.GetValue (runtime) is not Dictionary<IntPtr, IDisposable> tracked)
				throw new InvalidOperationException ("Could not inspect tracked JNI types.");
			lock (tracked)
				return tracked.ContainsKey (handle);
		}

		static void Collect ()
		{
			GC.Collect ();
			GC.WaitForPendingFinalizers ();
			GC.Collect ();
		}

		sealed class BlockingDeleteReferenceManager : JniRuntime.JniObjectReferenceManager {

			readonly JniRuntime.JniObjectReferenceManager inner;
			IntPtr blockedHandle;

			public BlockingDeleteReferenceManager (JniRuntime.JniObjectReferenceManager inner)
			{
				this.inner = inner;
			}

			public ManualResetEventSlim DeleteStarted {get;} = new ManualResetEventSlim ();
			public ManualResetEventSlim ReleaseDelete {get;} = new ManualResetEventSlim ();

			public override int GlobalReferenceCount => inner.GlobalReferenceCount;
			public override int WeakGlobalReferenceCount => inner.WeakGlobalReferenceCount;
			public override bool LogLocalReferenceMessages => inner.LogLocalReferenceMessages;
			public override bool LogGlobalReferenceMessages => inner.LogGlobalReferenceMessages;

			public void Block (IntPtr handle)
			{
				blockedHandle = handle;
			}

			public override JniObjectReference CreateGlobalReference (JniObjectReference reference) =>
				inner.CreateGlobalReference (reference);

			public override void DeleteGlobalReference (ref JniObjectReference reference)
			{
				if (reference.Handle == blockedHandle) {
					DeleteStarted.Set ();
					if (!ReleaseDelete.Wait (TimeSpan.FromSeconds (30)))
						throw new TimeoutException ("Global-reference deletion was not released.");
				}
				inner.DeleteGlobalReference (ref reference);
			}

			public override JniObjectReference CreateLocalReference (JniObjectReference reference, ref int localReferenceCount) =>
				inner.CreateLocalReference (reference, ref localReferenceCount);

			public override void DeleteLocalReference (ref JniObjectReference reference, ref int localReferenceCount) =>
				inner.DeleteLocalReference (ref reference, ref localReferenceCount);

			public override void CreatedLocalReference (JniObjectReference reference, ref int localReferenceCount) =>
				inner.CreatedLocalReference (reference, ref localReferenceCount);

			public override IntPtr ReleaseLocalReference (ref JniObjectReference reference, ref int localReferenceCount) =>
				inner.ReleaseLocalReference (ref reference, ref localReferenceCount);

			public override JniObjectReference CreateWeakGlobalReference (JniObjectReference reference) =>
				inner.CreateWeakGlobalReference (reference);

			public override void DeleteWeakGlobalReference (ref JniObjectReference reference) =>
				inner.DeleteWeakGlobalReference (ref reference);

			public override void WriteLocalReferenceLine (string format, params object [] args) =>
				inner.WriteLocalReferenceLine (format, args);

			public override void WriteGlobalReferenceLine (string format, params object? [] args) =>
				inner.WriteGlobalReferenceLine (format, args);
		}
	}
}
#endif // !__ANDROID__
