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

		public unsafe Throwable (IntPtr handle, JniHandleOwnership transfer)
			: base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None, new JniObjectReference (handle))
		{
			if (GetType () == typeof (Throwable))
				is_generated = true;

			SetHandle (handle, transfer);
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
			get { return class_ref; }
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

		internal System.Type GetThresholdType ()
		{
			return ThresholdType;
		}

		public unsafe Java.Lang.Class? Class {
			[Register ("getClass", "()Ljava/lang/Class;", "GetGetClassHandler")]
			get {
				IntPtr value;
				const string __id = "getClass.()Ljava/lang/Class;";
				value = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null).Handle;
				return global::Java.Lang.Object.GetObject<Java.Lang.Class> (value, JniHandleOwnership.TransferLocalRef);
			}
		}

		[EditorBrowsable (EditorBrowsableState.Never)]
		protected void SetHandle (IntPtr value, JniHandleOwnership transfer)
		{
			var reference = new JniObjectReference (value);

			Construct (
					ref reference,
					value == IntPtr.Zero ? JniObjectReferenceOptions.None : JniObjectReferenceOptions.Copy);
			if (value != IntPtr.Zero) {
				SetJavaStackTrace (new JniObjectReference (value));
			}
			JNIEnv.DeleteRef (value, transfer);
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
