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

		internal const string JniTypeName = "net/dot/jni/test/ManagedPeerRegistration";
		static Action<JniNativeMethodRegistrationArguments>? addRegistrations;
		JniType? registeredClass;

		public class Registration {
			[JniAddNativeMethodRegistration]
			static void Register (JniNativeMethodRegistrationArguments args)
			{
				addRegistrations?.Invoke (args);
			}
		}
		[UnmanagedFunctionPointer (CallingConvention.Winapi)]
		delegate int GetValue (IntPtr env, IntPtr klass);

		sealed class NativeTarget {
			public int Value (IntPtr env, IntPtr klass) => 42;
		}

		[SetUp]
		public void SetUp ()
		{
			var manager = TypeManager ?? throw new InvalidOperationException ("The test type manager is not initialized.");
			manager.NativeRegistrationObserver = ObserveRegistration;
		}

		[TearDown]
		public void TearDown ()
		{
			if (TypeManager != null)
				TypeManager.NativeRegistrationObserver = null;
			addRegistrations = null;
			registeredClass?.Dispose ();
			registeredClass = null;
		}

		[Test]
		public void EmptyJniTypeRegistration_DoesNotAdoptOwner ()
		{
			using var owner = new JniType (JniTypeName);
			owner.RegisterNativeMethods ();
			Assert.IsFalse (owner.IsRegisteredWithRuntime);
		}

		[Test]
		public void EmptyRegistration_DisposesClass ()
		{
			using var existingOwner = new JniType (JniTypeName);
			existingOwner.RegisterNativeMethods (
				new JniNativeMethodRegistration ("existing", "()I", new GetValue (new NativeTarget ().Value)));
			Register ();
			Assert.IsFalse (GetRegisteredClass ().PeerReference.IsValid);
			Assert.AreEqual (42, Call (existingOwner, "existing"));
		}

		[Test]
		public void FailureBeforeAdoption_DisposesClass ()
		{
			var expected = new InvalidOperationException ("Registration failed before adoption.");
			addRegistrations = args => throw expected;

			var error = Assert.Throws<NotSupportedException> (Register);
			Assert.AreSame (expected, error?.InnerException);
			Assert.IsFalse (GetRegisteredClass ().PeerReference.IsValid);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void MarshalingFailure_DisposesClass (bool genericDelegate)
		{
			using var existingOwner = new JniType (JniTypeName);
			existingOwner.RegisterNativeMethods (
				new JniNativeMethodRegistration ("existing", "()I", new GetValue (new NativeTarget ().Value)));
			addRegistrations = args => {
				args.Registrations.Add (new JniNativeMethodRegistration ("value", "()I", new GetValue (new NativeTarget ().Value)));
				args.Registrations.Add (genericDelegate
					? new JniNativeMethodRegistration ("missing", "()I", new Func<IntPtr, IntPtr, int> (new NativeTarget ().Value))
					: default);
			};

			var error = Assert.Throws<NotSupportedException> (Register);
			Assert.That (error?.InnerException, Is.TypeOf<ArgumentException> ());
			Assert.IsFalse (GetRegisteredClass ().PeerReference.IsValid);
			Assert.AreEqual (42, Call (existingOwner, "existing"));
		}

		[TestCase (false)]
		[TestCase (true)]
		public void AdoptedRegistration_RemainsCallable (bool throwAfterAdoption)
		{
			var weakOwner = RegisterAndReleaseOwner (throwAfterAdoption);
			Collect ();
			Assert.IsTrue (weakOwner.TryGetTarget (out var owner), "The runtime must retain the registration owner.");
			registeredClass = owner ?? throw new InvalidOperationException ("The registration owner was collected.");
			Assert.IsTrue (registeredClass.PeerReference.IsValid);
			Assert.AreEqual (42, Call (registeredClass, "value"));
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		WeakReference<JniType> RegisterAndReleaseOwner (bool throwAfterAdoption)
		{
			addRegistrations = args => args.Registrations.Add (
				new JniNativeMethodRegistration ("value", "()I", new GetValue (new NativeTarget ().Value)));

			if (throwAfterAdoption) {
				var manager = TypeManager ?? throw new InvalidOperationException ("The test type manager is not initialized.");
				var expected = new InvalidOperationException ("Registration failed after adoption.");
				manager.NativeRegistrationObserver = nativeClass => {
					registeredClass = nativeClass;
					nativeClass.RegisterNativeMethods (
						new JniNativeMethodRegistration ("value", "()I", new GetValue (new NativeTarget ().Value)));
					throw expected;
				};
				var error = Assert.Throws<NotSupportedException> (Register);
				Assert.AreSame (expected, error?.InnerException);
			} else {
				Register ();
			}

			return ReleaseOwner ();
		}

		[Test]
		public void PartialRegistrationFailure_PreservesBothOwners ()
		{
			using var existingOwner = new JniType (JniTypeName);
			existingOwner.RegisterNativeMethods (
				new JniNativeMethodRegistration ("existing", "()I", new GetValue (new NativeTarget ().Value)));
			var weakOwner = RegisterPartialAndReleaseOwner (existingOwner);
			Collect ();
			Assert.IsTrue (weakOwner.TryGetTarget (out var owner), "A partial registration must remain runtime-owned.");
			registeredClass = owner ?? throw new InvalidOperationException ("The partial registration owner was collected.");
			Assert.IsTrue (registeredClass.PeerReference.IsValid);
			Assert.AreEqual (42, Call (existingOwner, "existing"));
			Assert.AreEqual (42, Call (registeredClass, "value"));
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		WeakReference<JniType> RegisterPartialAndReleaseOwner (JniType existingOwner)
		{
			addRegistrations = args => {
				args.Registrations.Add (new JniNativeMethodRegistration ("value", "()I", new GetValue (new NativeTarget ().Value)));
				args.Registrations.Add (new JniNativeMethodRegistration ("missing", "()I", new GetValue (new NativeTarget ().Value)));
			};

			var error = Assert.Throws<NotSupportedException> (Register);
			using var cause = error?.InnerException as JavaException;
			Assert.IsNotNull (cause);
			Assert.AreEqual (42, Call (existingOwner, "existing"), "Failure must not unregister another owner's methods.");
			Assert.AreEqual (42, Call (GetRegisteredClass (), "value"), "JNI may register a method before rejecting the batch.");
			return ReleaseOwner ();
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

		WeakReference<JniType> ReleaseOwner ()
		{
			var owner = new WeakReference<JniType> (GetRegisteredClass ());
			registeredClass = null;
			addRegistrations = null;
			var manager = TypeManager ?? throw new InvalidOperationException ("The test type manager is not initialized.");
			manager.NativeRegistrationObserver = ObserveRegistration;
			// Clear the reflection registrar's shared list so only the runtime can retain the delegates.
			Register ();
			return owner;
		}

		void ObserveRegistration (JniType nativeClass) => registeredClass = nativeClass;

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

		JniType GetRegisteredClass () =>
			registeredClass ?? throw new InvalidOperationException ("The standalone registrar was not invoked.");

		static unsafe void Register ()
		{
			using var managedPeer = new JniType ("net/dot/jni/ManagedPeer");
			using var nativeClass = new JniType (JniTypeName);
			var register = managedPeer.GetStaticMethod ("registerNativeMembers", "(Ljava/lang/Class;Ljava/lang/String;)V");
			var classRef = nativeClass.PeerReference.NewLocalRef ();
			var methods = JniEnvironment.Strings.NewString ("");
			try {
				var args = stackalloc JniArgumentValue [2];
				args [0] = new JniArgumentValue (classRef);
				args [1] = new JniArgumentValue (methods);
				JniEnvironment.StaticMethods.CallStaticVoidMethod (managedPeer.PeerReference, register, args);
			} finally {
				try {
					Assert.AreEqual (JniObjectReferenceType.Local, JniEnvironment.References.GetObjectRefType (classRef));
					Assert.AreEqual (JniObjectReferenceType.Local, JniEnvironment.References.GetObjectRefType (methods));
				} finally {
					JniObjectReference.Dispose (ref classRef);
					JniObjectReference.Dispose (ref methods);
				}
			}
		}
	}
}
#endif // !__ANDROID__
