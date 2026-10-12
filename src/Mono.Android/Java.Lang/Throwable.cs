using System;
using System.Collections.Generic;

using Java.Interop;

using Android.Runtime;
using System.ComponentModel;
using System.Diagnostics;

namespace Java.Lang {

	public partial class Throwable : JavaException, IJavaObject, IDisposable, IJavaObjectEx
	{
		protected bool is_generated;

		/// <summary>Initializes an empty peer for a derived binding to construct its Java throwable.</summary>
		/// <param name="options">Must be <see cref="JniObjectReferenceOptions.None"/>.</param>
		protected Throwable (JniObjectReferenceOptions options)
			: base (default (JniObjectReference), GetEmptyPeerInitializationOptions (options))
		{
			if (GetType () == typeof (Throwable))
				is_generated = true;

			SetHandle (IntPtr.Zero, JniHandleOwnership.DoNotTransfer);
		}

		static JniObjectReferenceOptions GetEmptyPeerInitializationOptions (JniObjectReferenceOptions options)
		{
			if (options != JniObjectReferenceOptions.None)
				throw new ArgumentException ("Only JniObjectReferenceOptions.None is supported for empty peer initialization.", nameof (options));
			return JniObjectReferenceOptions.CopyAndDispose;
		}

		/// <safety>
		/// <paramref name="handle"/> must be null or a live JNI Throwable reference usable on the current thread
		/// during construction. <paramref name="transfer"/> must describe reference ownership the caller actually
		/// holds; transferred references must not be reused after this constructor releases them.
		/// </safety>
		public unsafe Throwable (IntPtr handle, JniHandleOwnership transfer)
			: base (GetThrowableReference (handle, transfer), JniObjectReferenceOptions.CopyAndDispose)
		{
			if (GetType () == typeof (Throwable))
				is_generated = true;

			SetHandle (handle, transfer);
		}

		static JniObjectReference GetThrowableReference (IntPtr handle, JniHandleOwnership transfer)
		{
			var type = (transfer & (JniHandleOwnership.TransferLocalRef | JniHandleOwnership.TransferGlobalRef)) switch {
				JniHandleOwnership.TransferLocalRef => JniObjectReferenceType.Local,
				JniHandleOwnership.TransferGlobalRef => JniObjectReferenceType.Global,
				_ => JniObjectReferenceType.Invalid,
			};
			return new JniObjectReference (handle, type);
		}

		IntPtr IJavaObjectEx.ToLocalJniHandle ()
		{
			lock (this) {
				var peerRef = PeerReference;
				if (!peerRef.IsValid)
					return IntPtr.Zero;
				return peerRef.NewLocalRef ().Handle;
			}
		}

		public override string StackTrace => base.StackTrace;

		public override string ToString ()
		{
			return base.ToString ();
		}

		[EditorBrowsable (EditorBrowsableState.Never)]
		public new int JniIdentityHashCode => base.JniIdentityHashCode;

		[DebuggerBrowsable (DebuggerBrowsableState.Never)]
		[EditorBrowsable (EditorBrowsableState.Never)]
		public new JniObjectReference PeerReference => base.PeerReference;

		[DebuggerBrowsable (DebuggerBrowsableState.Never)]
		[EditorBrowsable (EditorBrowsableState.Never)]
		public override JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		[EditorBrowsable (EditorBrowsableState.Never)]
		public IntPtr Handle {
			get {
				var peerRef = PeerReference;
				if (!peerRef.IsValid) {
					return IntPtr.Zero;
				}
				return peerRef.Handle;
			}
		}

		[DebuggerBrowsable (DebuggerBrowsableState.Never)]
		[EditorBrowsable (EditorBrowsableState.Never)]
		protected virtual IntPtr ThresholdClass {
			get { return _members.GetPeerTypeHandle (); }
		}

		[DebuggerBrowsable (DebuggerBrowsableState.Never)]
		[EditorBrowsable (EditorBrowsableState.Never)]
		protected virtual System.Type ThresholdType {
			get { return typeof (Java.Lang.Throwable); }
		}

		internal IntPtr GetThresholdClass ()
		{
			return ThresholdClass;
		}

		// Used only to preserve virtual dispatch for legacy bindings.
		internal System.Type GetThresholdType ()
		{
			return ThresholdType;
		}

		public Java.Lang.Class? Class {
			[Register ("getClass", "()Ljava/lang/Class;", "GetGetClassHandler")]
			get {
				unsafe {
					// SAFETY: getClass is a parameterless instance call returning Class; its local reference transfers to the managed peer.
					IntPtr value;
					const string __id = "getClass.()Ljava/lang/Class;";
					value = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null).Handle;
					return global::Java.Lang.Object.GetObject<Java.Lang.Class> (value, JniHandleOwnership.TransferLocalRef);
				}
			}
		}

		[EditorBrowsable (EditorBrowsableState.Never)]
		protected void SetHandle (IntPtr value, JniHandleOwnership transfer)
		{
			try {
				var reference = new JniObjectReference (value);
				Construct (
						ref reference,
						value == IntPtr.Zero
							? JniObjectReferenceOptions.None
							: JNIEnv.ToJniObjectReferenceOptions (transfer));
				if (value != IntPtr.Zero) {
					SetJavaStackTrace (new JniObjectReference (value));
				}
			} catch {
				Dispose ();
				throw;
			} finally {
				JNIEnv.DeleteRef (value, transfer);
			}
		}

		public static Throwable FromException (System.Exception e)
		{
			if (e == null)
				throw new ArgumentNullException ("e");

			if (e is Throwable)
				return (Throwable) e;

			return Android.Runtime.JavaProxyThrowable.Create (e);
		}

		public static System.Exception ToException (Throwable e)
		{
			if (e == null)
				throw new ArgumentNullException ("e");

			return e;
		}

		~Throwable ()
		{
		}

		public new void UnregisterFromRuntime () => base.UnregisterFromRuntime ();

		public new void Dispose () => base.Dispose ();

		protected override void Dispose (bool disposing)
		{
		}
	}
}
