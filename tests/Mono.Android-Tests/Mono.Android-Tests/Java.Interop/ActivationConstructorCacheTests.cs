#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Android.Runtime;
using Java.Interop;
using NUnit.Framework;
using JavaObject = Java.Interop.JavaObject;

namespace Java.InteropTests;

[TestFixture]
[Category ("ReflectionActivationCache")]
public class ActivationConstructorCacheTests
{
	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

	delegate bool TryConstructPeerDelegate (IJavaPeerable self, ref JniObjectReference reference, JniObjectReferenceOptions options, Type type);

	[TestCase (typeof (XAPeer), 1)]
	[TestCase (typeof (JIPeer), 2)]
	[TestCase (typeof (MissingPeer), 2)]
	public void ResolutionIsCached (Type peerType, int expectedLookups)
	{
		AssumeReflectionActivation ();
		var type = new CountingType (peerType);
		var resolve = GetResolver ();
		var activation = resolve.Invoke (null, new object [] { type });
		Assert.AreEqual (expectedLookups, type.Lookups);
		for (int i = 0; i < 10; i++)
			Assert.AreEqual (activation, resolve.Invoke (null, new object [] { type }));
		Assert.AreEqual (expectedLookups, type.Lookups, "Warm resolution must not enter the reflection binder.");
	}

	[TestCase (typeof (XAPeer))]
	[TestCase (typeof (JIPeer))]
	[TestCase (typeof (MissingPeer))]
	public void ConcurrentResolutionPublishesConsistentResult (Type peerType)
	{
		AssumeReflectionActivation ();
		var type = new CountingType (peerType);
		var resolve = GetResolver ();
		var results = new object? [32];
		Parallel.For (0, results.Length, i => results [i] = resolve.Invoke (null, new object [] { type }));
		foreach (var result in results)
			Assert.AreEqual (results [0], result);
		var lookups = type.Lookups;
		Assert.Greater (lookups, 0);
		Parallel.For (0, results.Length, i => resolve.Invoke (null, new object [] { type }));
		Assert.AreEqual (lookups, type.Lookups);
	}

	[TestCase (false, JniObjectReferenceOptions.Copy)]
	[TestCase (false, JniObjectReferenceOptions.CopyAndDispose)]
	[TestCase (true, JniObjectReferenceOptions.Copy)]
	[TestCase (true, JniObjectReferenceOptions.CopyAndDispose)]
	public void CoreClrConstructsExistingPeerAndPreservesOwnership (bool ji, JniObjectReferenceOptions options)
	{
		var construct = GetCoreClrConstructor ();
		var peerType = ji ? typeof (JIPeer) : typeof (XAPeer);
		var type = new CountingType (peerType);
		for (int i = 0; i < 2; i++) {
			using var source = new JavaObject ();
			var reference = source.PeerReference.NewLocalRef ();
			var self = (Java.Lang.Object) RuntimeHelpers.GetUninitializedObject (peerType);
			((IJavaPeerable) self).SetJniManagedPeerState (JniManagedPeerStates.Replaceable | JniManagedPeerStates.Activatable);
			try {
				Assert.IsTrue (construct (self, ref reference, options, type));
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (source.PeerReference, self.PeerReference));
				if (self is XAPeer xa) {
					Assert.AreSame (self, xa.ConstructedSelf);
					Assert.AreEqual (JniHandleOwnership.DoNotTransfer, xa.Transfer);
					Assert.IsFalse (xa.UsedJI, "XA must win when both constructors exist.");
				} else if (self is JIPeer jp) {
					Assert.AreSame (self, jp.ConstructedSelf);
					Assert.AreEqual (options, jp.Options);
				}
				Assert.AreEqual (options == JniObjectReferenceOptions.Copy, reference.IsValid);
			} finally {
				self.Dispose ();
				JniObjectReference.Dispose (ref reference);
			}
		}
		Assert.AreEqual (ji ? 2 : 1, type.Lookups, "Repeated construction must reuse the value manager's cache.");
	}

	[Test]
	public void CoreClrMissingConstructorLeavesReferenceAndPeerUntouched ()
	{
		var construct = GetCoreClrConstructor ();
		var type = new CountingType (typeof (MissingPeer));
		using var source = new JavaObject ();
		var reference = source.PeerReference.NewLocalRef ();
		var original = reference;
		var self = (MissingPeer) RuntimeHelpers.GetUninitializedObject (typeof (MissingPeer));
		GC.SuppressFinalize (self);
		try {
			for (int i = 0; i < 2; i++) {
				Assert.IsFalse (construct (self, ref reference, JniObjectReferenceOptions.CopyAndDispose, type));
				Assert.AreEqual (original, reference);
				Assert.IsFalse (self.PeerReference.IsValid);
			}
			Assert.AreEqual (2, type.Lookups);
		} finally {
			JniObjectReference.Dispose (ref reference);
		}
	}

	[TestCase (typeof (ThrowingXAPeer))]
	[TestCase (typeof (ThrowingJIPeer))]
	public void CoreClrThrowingConstructorDoesNotDisposeOrCopyBackReference ([DynamicallyAccessedMembers (Constructors)] Type peerType)
	{
		var construct = GetCoreClrConstructor ();
		var type = new CountingType (peerType);
		using var source = new JavaObject ();
		var reference = source.PeerReference.NewLocalRef ();
		var original = reference;
		try {
			for (int i = 0; i < 2; i++) {
				var self = (Java.Lang.Object) RuntimeHelpers.GetUninitializedObject (peerType);
				GC.SuppressFinalize (self);
				var exception = Assert.Throws<TargetInvocationException> (() =>
					construct (self, ref reference, JniObjectReferenceOptions.CopyAndDispose, type));
				Assert.IsInstanceOf<InvalidOperationException> (exception?.InnerException);
				Assert.AreEqual ("activation failed", exception?.InnerException?.Message);
				Assert.AreEqual (original, reference);
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (source.PeerReference, reference));
			}
			Assert.AreEqual (peerType == typeof (ThrowingXAPeer) ? 1 : 2, type.Lookups);
		} finally {
			JniObjectReference.Dispose (ref reference);
		}
	}

	[DynamicDependency (Constructors, typeof (XAPeer))]
	[DynamicDependency (Constructors, typeof (JIPeer))]
	[DynamicDependency (Constructors, typeof (MissingPeer))]
	[DynamicDependency (Constructors, typeof (ThrowingXAPeer))]
	[DynamicDependency (Constructors, typeof (ThrowingJIPeer))]
	static void AssumeReflectionActivation ()
	{
		if (Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap)
			Assert.Ignore ("This test exercises reflection activation, not the generated trimmable typemap.");
	}

	[UnconditionalSuppressMessage ("Trimming", "IL2111", Justification = "Only the explicitly preserved test peer constructors are resolved, through CountingType.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "This test explicitly exercises the reflection value manager with preserved peer constructors.")]
	static MethodInfo GetResolver ()
	{
		var managerType = Type.GetType ("Microsoft.Android.Runtime.JavaMarshalValueManager, Mono.Android", throwOnError: true)
			?? throw new InvalidOperationException ("Could not find the CoreCLR reflection value manager.");
		return managerType.GetMethod ("GetActivationConstructor", BindingFlags.NonPublic | BindingFlags.Static)
			?? throw new InvalidOperationException ("Could not find the value manager's activation constructor resolver.");
	}

	[UnconditionalSuppressMessage ("Trimming", "IL2111", Justification = "Only the explicitly preserved test peer constructors are invoked, through CountingType.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "This test explicitly exercises the reflection value manager with preserved peer constructors.")]
	static TryConstructPeerDelegate GetCoreClrConstructor ()
	{
		AssumeReflectionActivation ();
		if (!AppContext.TryGetSwitch ("Microsoft.Android.Runtime.RuntimeFeature.IsCoreClrRuntime", out bool isCoreClr) || !isCoreClr)
			Assert.Ignore ("This test exercises the CoreCLR reflection value manager.");
		var manager = JniEnvironment.Runtime.ValueManager;
		Assert.AreEqual ("Microsoft.Android.Runtime.JavaMarshalValueManager", manager.GetType ().FullName);
		var managerType = Type.GetType ("Microsoft.Android.Runtime.JavaMarshalValueManager, Mono.Android", throwOnError: true)
			?? throw new InvalidOperationException ("Could not find the CoreCLR reflection value manager.");
		var method = managerType.GetMethod ("TryConstructPeer", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			?? throw new InvalidOperationException ("Could not find CoreCLR TryConstructPeer.");
		return method.CreateDelegate<TryConstructPeerDelegate> (manager);
	}

	sealed class CountingType : TypeDelegator
	{
		int lookups;
		public int Lookups => Volatile.Read (ref lookups);

		public CountingType (Type type) : base (type) {}

		public override bool Equals (object? other) => ReferenceEquals (this, other);
		public override bool Equals (Type? other) => ReferenceEquals (this, other);
		public override int GetHashCode () => RuntimeHelpers.GetHashCode (this);

		[DynamicallyAccessedMembers (Constructors)]
		protected override ConstructorInfo? GetConstructorImpl (BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type [] types, ParameterModifier []? modifiers)
		{
			Interlocked.Increment (ref lookups);
			Assert.AreEqual (BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, bindingAttr);
			Assert.IsNull (binder);
			return base.GetConstructorImpl (bindingAttr, binder, callConvention, types, modifiers);
		}
	}

	sealed class XAPeer : Java.Lang.Object
	{
		public object? ConstructedSelf;
		public JniHandleOwnership Transfer;
		public bool UsedJI;

		internal XAPeer (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
			ConstructedSelf = this;
			Transfer = transfer;
		}

		public XAPeer (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			UsedJI = true;
			Construct (ref reference, options);
		}
	}

	sealed class JIPeer : Java.Lang.Object
	{
		public object? ConstructedSelf;
		public JniObjectReferenceOptions Options;

		internal JIPeer (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			ConstructedSelf = this;
			Options = options;
			Construct (ref reference, options);
		}
	}

	sealed class MissingPeer : Java.Lang.Object
	{
		public MissingPeer () {}
	}

	sealed class ThrowingXAPeer : Java.Lang.Object
	{
		public ThrowingXAPeer (IntPtr handle, JniHandleOwnership transfer) : base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			throw new InvalidOperationException ("activation failed");
		}
	}

	sealed class ThrowingJIPeer : Java.Lang.Object
	{
		public ThrowingJIPeer (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			reference = default;
			throw new InvalidOperationException ("activation failed");
		}
	}
}
