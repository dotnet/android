using System;
using System.Reflection;

using Android.Runtime;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	[NonParallelizable]
	[Category ("TransferredReferences")]
	public class TransferredReferenceTests
	{
		[Test]
		public void GetObject_Success (
			[Values (JniHandleOwnership.DoNotTransfer, JniHandleOwnership.TransferLocalRef, JniHandleOwnership.TransferGlobalRef)] JniHandleOwnership ownership,
			[Values (false, true)] bool doNotRegister)
		{
			var transfer = WithRegistration (ownership, doNotRegister);
			using var input = new InputReferenceTracker (JNIEnv.AllocObject ("java/lang/Object"), transfer);
			Assert.IsNull (JniEnvironment.Runtime.ValueManager.PeekPeer (new JniObjectReference (input.Handle)));

			using var peer = Java.Lang.Object.GetObject<Java.Lang.Object> (input.Handle, transfer);
			input.AssertOwnership (transfer);
			Assert.IsNotNull (peer);
			Assert.IsTrue (peer.PeerReference.IsValid);
			Assert.AreNotEqual (input.Handle, peer.Handle, "The peer must own a separate reference.");
		}

		[Test]
		public void GetObject_CachedPeer (
			[Values (JniHandleOwnership.DoNotTransfer, JniHandleOwnership.TransferLocalRef, JniHandleOwnership.TransferGlobalRef)] JniHandleOwnership ownership)
		{
			using var peer = new Java.Lang.Object (JNIEnv.CreateInstance ("java/lang/Object", "()V"), JniHandleOwnership.TransferLocalRef);
			using var input = new InputReferenceTracker (JNIEnv.NewLocalRef (peer.Handle), ownership);

			Assert.AreSame (peer, Java.Lang.Object.GetObject<Java.Lang.Object> (input.Handle, ownership));
			input.AssertOwnership (ownership);
			Assert.IsTrue (peer.PeerReference.IsValid);
		}

		[Test]
		public void GetObject_MissingActivationConstructor (
			[Values (JniHandleOwnership.DoNotTransfer, JniHandleOwnership.TransferLocalRef, JniHandleOwnership.TransferGlobalRef)] JniHandleOwnership ownership,
			[Values (false, true)] bool doNotRegister)
		{
			if (Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap) {
				Assert.Ignore ("The trimmable typemap supports inherited activation constructors.");
			}

			var transfer = WithRegistration (ownership, doNotRegister);
			using var input = new InputReferenceTracker (JNIEnv.AllocObject (typeof (MissingTransferredReferencePeer)), transfer);
			Assert.IsNull (JniEnvironment.Runtime.ValueManager.PeekPeer (new JniObjectReference (input.Handle)));

			var exception = Assert.Throws<NotSupportedException> (() =>
				Java.Lang.Object.GetObject<MissingTransferredReferencePeer> (input.Handle, transfer));
			Assert.IsInstanceOf<MissingMethodException> (exception.InnerException);
			input.AssertOwnership (transfer);
		}

		[Test]
		public void GetObject_ThrowingActivationConstructor (
			[Values (JniHandleOwnership.DoNotTransfer, JniHandleOwnership.TransferLocalRef, JniHandleOwnership.TransferGlobalRef)] JniHandleOwnership ownership,
			[Values (false, true)] bool doNotRegister)
		{
			var transfer = WithRegistration (ownership, doNotRegister);
			using var input = new InputReferenceTracker (JNIEnv.AllocObject (typeof (ThrowingTransferredReferencePeer)), transfer);
			Assert.IsNull (JniEnvironment.Runtime.ValueManager.PeekPeer (new JniObjectReference (input.Handle)));

			var exception = Assert.Catch<Exception> (() =>
				Java.Lang.Object.GetObject<ThrowingTransferredReferencePeer> (input.Handle, transfer));
			if (exception is TargetInvocationException invocation) {
				exception = invocation.InnerException;
			}
			Assert.IsInstanceOf<InvalidOperationException> (exception);
			Assert.AreEqual (ThrowingTransferredReferencePeer.ExceptionMessage, exception.Message);
			input.AssertOwnership (transfer);
		}

		[Test]
		public void ObjectSetHandle (
			[Values (JniHandleOwnership.DoNotTransfer, JniHandleOwnership.TransferLocalRef, JniHandleOwnership.TransferGlobalRef)] JniHandleOwnership ownership,
			[Values (false, true)] bool doNotRegister,
			[Values (false, true)] bool failCopy)
		{
			var transfer = WithRegistration (ownership, doNotRegister);
			using var input = new InputReferenceTracker (JNIEnv.AllocObject ("java/lang/Object"), transfer) {
				FailCopy = failCopy,
			};
			using var peer = new SetHandleObject ();
			if (failCopy) {
				Assert.Throws<InvalidOperationException> (() => peer.Assign (input.Handle, transfer));
				Assert.IsFalse (peer.PeerReference.IsValid);
			} else {
				peer.Assign (input.Handle, transfer);
				Assert.IsTrue (peer.PeerReference.IsValid);
				var registered = JniEnvironment.Runtime.ValueManager.PeekPeer (peer.PeerReference);
				if (doNotRegister) {
					Assert.IsNull (registered);
				} else {
					Assert.AreSame (peer, registered);
				}
			}
			input.AssertOwnership (transfer);
		}

		[Test]
		public void ThrowableSetHandle (
			[Values (JniHandleOwnership.DoNotTransfer, JniHandleOwnership.TransferLocalRef, JniHandleOwnership.TransferGlobalRef)] JniHandleOwnership ownership,
			[Values (false, true)] bool doNotRegister,
			[Values (false, true)] bool failCopy)
		{
			var transfer = WithRegistration (ownership, doNotRegister);
			using var input = new InputReferenceTracker (JNIEnv.CreateInstance ("java/lang/Throwable", "()V"), transfer) {
				FailCopy = failCopy,
			};
			using var peer = new SetHandleThrowable ();
			if (failCopy) {
				Assert.Throws<InvalidOperationException> (() => peer.Assign (input.Handle, transfer));
				Assert.IsFalse (peer.PeerReference.IsValid);
			} else {
				peer.Assign (input.Handle, transfer);
				Assert.IsTrue (peer.PeerReference.IsValid);
				Assert.IsNotNull (peer.JavaStackTrace);
			}
			input.AssertOwnership (transfer);
		}

		static JniHandleOwnership WithRegistration (JniHandleOwnership ownership, bool doNotRegister)
		{
			return ownership | (doNotRegister ? JniHandleOwnership.DoNotRegister : JniHandleOwnership.DoNotTransfer);
		}

		// Observe only the input handle, forwarding all JNI operations to the real manager.
		// This avoids global-count races and never asks JNI about an already deleted handle.
		sealed class InputReferenceTracker : JniRuntime.JniObjectReferenceManager
		{
			static readonly PropertyInfo managerProperty = typeof (JniRuntime).GetProperty (nameof (JniRuntime.ObjectReferenceManager))
				?? throw new InvalidOperationException ("Could not find the JNI object reference manager property.");

			readonly JniRuntime.JniObjectReferenceManager original;
			readonly JniObjectReferenceType referenceType;
			int deletions;

			public IntPtr Handle { get; }
			public bool FailCopy { get; set; }

			public InputReferenceTracker (IntPtr local, JniHandleOwnership ownership)
			{
				OnSetRuntime (JniEnvironment.Runtime);
				original = Runtime.ObjectReferenceManager;
				referenceType = (ownership & JniHandleOwnership.TransferLocalRef) != 0
					? JniObjectReferenceType.Local
					: JniObjectReferenceType.Global;
				if (referenceType == JniObjectReferenceType.Local) {
					Handle = local;
				} else {
					try {
						Handle = JNIEnv.NewGlobalRef (local);
					} finally {
						JNIEnv.DeleteLocalRef (local);
					}
				}
				bool installed = false;
				try {
					managerProperty.SetValue (Runtime, this);
					installed = true;
				} finally {
					if (!installed) {
						DeleteInput ();
					}
				}
			}

			public void AssertOwnership (JniHandleOwnership ownership)
			{
				bool transferred = (ownership & (JniHandleOwnership.TransferLocalRef | JniHandleOwnership.TransferGlobalRef)) != 0;
				Assert.AreEqual (transferred ? 1 : 0, deletions, "Input reference deletion count.");
			}

			protected override void Dispose (bool disposing)
			{
				try {
					// Also clean up when a regression leaves the transferred input behind.
					if (deletions == 0) {
						DeleteInput ();
					}
				} finally {
					managerProperty.SetValue (Runtime, original);
				}
			}

			void DeleteInput ()
			{
				if (referenceType == JniObjectReferenceType.Local) {
					JNIEnv.DeleteLocalRef (Handle);
				} else {
					JNIEnv.DeleteGlobalRef (Handle);
				}
			}

			void Deleting (JniObjectReference reference)
			{
				if (reference.Handle != Handle) {
					return;
				}
				Assert.AreEqual (referenceType, reference.Type, "Wrong deletion API for the input reference.");
				// Fail before entering JNI if a regression attempts a double delete.
				Assert.AreEqual (0, deletions, "Input reference was deleted more than once.");
				deletions++;
			}

			public override int GlobalReferenceCount => original.GlobalReferenceCount;
			public override int WeakGlobalReferenceCount => original.WeakGlobalReferenceCount;
			public override bool LogGlobalReferenceMessages => original.LogGlobalReferenceMessages;
			public override bool LogLocalReferenceMessages => original.LogLocalReferenceMessages;

			public override void WriteGlobalReferenceLine (string format, params object [] args) => original.WriteGlobalReferenceLine (format, args);
			public override void WriteLocalReferenceLine (string format, params object [] args) => original.WriteLocalReferenceLine (format, args);
			public override JniObjectReference CreateLocalReference (JniObjectReference reference, ref int count) => original.CreateLocalReference (reference, ref count);
			public override void CreatedLocalReference (JniObjectReference reference, ref int count) => original.CreatedLocalReference (reference, ref count);
			public override IntPtr ReleaseLocalReference (ref JniObjectReference reference, ref int count) => original.ReleaseLocalReference (ref reference, ref count);
			public override JniObjectReference CreateWeakGlobalReference (JniObjectReference reference) => original.CreateWeakGlobalReference (reference);
			public override void DeleteWeakGlobalReference (ref JniObjectReference reference) => original.DeleteWeakGlobalReference (ref reference);

			public override JniObjectReference CreateGlobalReference (JniObjectReference reference)
			{
				if (FailCopy && reference.Handle == Handle) {
					throw new InvalidOperationException ("Injected input reference copy failure.");
				}
				return original.CreateGlobalReference (reference);
			}

			public override void DeleteLocalReference (ref JniObjectReference reference, ref int count)
			{
				Deleting (reference);
				original.DeleteLocalReference (ref reference, ref count);
			}

			public override void DeleteGlobalReference (ref JniObjectReference reference)
			{
				Deleting (reference);
				original.DeleteGlobalReference (ref reference);
			}
		}

		sealed class SetHandleObject : Java.Lang.Object
		{
			public SetHandleObject () : base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
			{
			}

			public void Assign (IntPtr handle, JniHandleOwnership transfer) => SetHandle (handle, transfer);
		}

		sealed class SetHandleThrowable : Java.Lang.Throwable
		{
			public SetHandleThrowable () : base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
			{
			}

			public void Assign (IntPtr handle, JniHandleOwnership transfer) => SetHandle (handle, transfer);
		}
	}

	[Register ("net/dot/android/test/MissingTransferredReferencePeer")]
	public sealed class MissingTransferredReferencePeer : Java.Lang.Object
	{
		public MissingTransferredReferencePeer ()
		{
		}
	}

	[Register ("net/dot/android/test/ThrowingTransferredReferencePeer")]
	public sealed class ThrowingTransferredReferencePeer : Java.Lang.Object
	{
		public const string ExceptionMessage = "transferred reference activation failure";

		public ThrowingTransferredReferencePeer ()
		{
		}

		public ThrowingTransferredReferencePeer (IntPtr handle, JniHandleOwnership transfer)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			// Fail before creating a peer reference so this tests only input ownership.
			throw new InvalidOperationException (ExceptionMessage);
		}
	}
}
