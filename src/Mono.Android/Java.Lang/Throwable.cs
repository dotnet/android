using System;
using System.Collections.Generic;

using Java.Interop;

using Android.Runtime;
using System.ComponentModel;
using System.Diagnostics;

namespace Java.Lang {

	public partial class Throwable : global::System.Exception, IJavaObject, IDisposable, IJavaObjectEx
#if JAVA_INTEROP
		, IJavaPeerable
#endif  // JAVA_INTEROP
	{

		protected bool is_generated;
		internal IntPtr handle;

		IntPtr           key_handle;
		JObjectRefType   handle_type;
#pragma warning disable CS0649, CS0169, CS0414 // Suppress fields are never used warnings, these fields are used directly by monodroid-glue.cc
		IntPtr           weak_handle;
		int              refs_added;
#pragma warning restore CS0649, CS0169, CS0414

		bool             isProxy;
		bool             needsActivation;

		string? nativeStack;

		public Throwable (IntPtr handle, JniHandleOwnership transfer)
			: base (_GetMessage (handle), _GetInnerException (handle))
		{
			if (GetType () == typeof (Throwable))
				is_generated = true;

			// Check if handle was preset by our java activation mechanism
			if (this.handle != IntPtr.Zero) {
				needsActivation = true;
				handle          = this.handle;
				if (handle_type != 0)
					return;
				transfer        = JniHandleOwnership.DoNotTransfer;
			}

			SetHandle (handle, transfer);
		}

#if JAVA_INTEROP
		static JniMethodInfo?         Throwable_getMessage;
#endif

		static string? _GetMessage (IntPtr handle)
		{
			if (handle == IntPtr.Zero)
				return null;

			IntPtr value;
#if JAVA_INTEROP
			const string __id = "getMessage.()Ljava/lang/String;";
			if (Throwable_getMessage == null) {
				Throwable_getMessage = _members.InstanceMethods.GetMethodInfo (__id);
			}
			value = JniEnvironment.InstanceMethods.CallObjectMethod (new JniObjectReference (handle), Throwable_getMessage).Handle;
#else   // !JAVA_INTEROP
				if (id_getMessage == IntPtr.Zero)
					id_getMessage = JNIEnv.GetMethodID (class_ref, "getMessage", "()Ljava/lang/String;");
				value = JNIEnv.CallObjectMethod  (handle, id_getMessage);
#endif	// !JAVA_INTEROP

			return JNIEnv.GetString (value, JniHandleOwnership.TransferLocalRef);
		}

#if JAVA_INTEROP
		static JniMethodInfo?         Throwable_getCause;
#endif

		static global::System.Exception? _GetInnerException (IntPtr handle)
		{
			if (handle == IntPtr.Zero)
				return null;

			IntPtr value;
#if JAVA_INTEROP
			const string __id = "getCause.()Ljava/lang/Throwable;";
			if (Throwable_getCause == null) {
				Throwable_getCause = _members.InstanceMethods.GetMethodInfo (__id);
			}
			value = JniEnvironment.InstanceMethods.CallObjectMethod (new JniObjectReference (handle), Throwable_getCause).Handle;
#else   // !JAVA_INTEROP
			if (id_getCause == IntPtr.Zero)
				id_getCause = JNIEnv.GetMethodID (class_ref, "getCause", "()Ljava/lang/Throwable;");

			value = JNIEnv.CallObjectMethod  (handle, id_getCause);
#endif	// !JAVA_INTEROP

			var cause = global::Java.Lang.Object.GetObject<Java.Lang.Throwable> (
					value,
					JniHandleOwnership.TransferLocalRef);

			var proxy = cause as JavaProxyThrowable;
			if (proxy != null)
				return proxy.InnerException;

			return cause;
		}

		IntPtr IJavaObjectEx.KeyHandle {
			get {return key_handle;}
			set {key_handle = value;}
		}

		bool IJavaObjectEx.IsProxy {
			get {return isProxy;}
			set {isProxy = value;}
		}

		bool IJavaObjectEx.NeedsActivation {
			get {return needsActivation;}
			set {needsActivation = true;}
		}

		IntPtr IJavaObjectEx.ToLocalJniHandle ()
		{
			lock (this) {
				if (handle == IntPtr.Zero)
					return handle;
				return JNIEnv.NewLocalRef (handle);
			}
		}

		public override string StackTrace {
			get {
				return base.StackTrace + ManagedStackTraceAddendum;
			}
		}

		string ManagedStackTraceAddendum {
			get {
				var javaStack = JavaStackTrace;
				if (string.IsNullOrEmpty (javaStack))
					return "";
				return Environment.NewLine +
					"  --- End of managed " +
					GetType ().FullName +
					" stack trace ---" + Environment.NewLine +
					javaStack;
			}
		}

		string? JavaStackTrace {
			get {
				if (!string.IsNullOrEmpty (nativeStack))
					return nativeStack;

				if (handle == IntPtr.Zero)
					return null;

				using (var nativeStackWriter = new Java.IO.StringWriter ())
				using (var nativeStackPw = new Java.IO.PrintWriter (nativeStackWriter)) {
					PrintStackTrace (nativeStackPw);
					nativeStack = nativeStackWriter.ToString ();
				}
				return nativeStack;
			}
		}

		public override string ToString ()
		{
			return base.ToString () + ManagedStackTraceAddendum;
		}

#if JAVA_INTEROP
		[EditorBrowsable (EditorBrowsableState.Never)]
		public int JniIdentityHashCode {
			get {return (int) key_handle;}
		}

		[DebuggerBrowsable (DebuggerBrowsableState.Never)]
		[EditorBrowsable (EditorBrowsableState.Never)]
		public JniObjectReference PeerReference {
			get {
				return new JniObjectReference (handle, (JniObjectReferenceType) handle_type);
			}
		}

		[DebuggerBrowsable (DebuggerBrowsableState.Never)]
		[EditorBrowsable (EditorBrowsableState.Never)]
		public virtual JniPeerMembers JniPeerMembers {
			get { return _members; }
		}
#endif  // JAVA_INTEROP

		[EditorBrowsable (EditorBrowsableState.Never)]
		public IntPtr Handle { get { return handle; } }

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

#if !JAVA_INTEROP
		static IntPtr id_getClass;
#endif  // !JAVA_INTEROP

		public unsafe Java.Lang.Class? Class {
			[Register ("getClass", "()Ljava/lang/Class;", "GetGetClassHandler")]
			get {
				IntPtr value;
#if JAVA_INTEROP
				const string __id = "getClass.()Ljava/lang/Class;";
				value = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null).Handle;
#else   // !JAVA_INTEROP
				if (id_getClass == IntPtr.Zero)
					id_getClass = JNIEnv.GetMethodID (class_ref, "getClass", "()Ljava/lang/Class;");
				value = JNIEnv.CallObjectMethod  (Handle, id_getClass);
#endif  // !JAVA_INTEROP
				return global::Java.Lang.Object.GetObject<Java.Lang.Class> (value, JniHandleOwnership.TransferLocalRef);
			}
		}

		[EditorBrowsable (EditorBrowsableState.Never)]
		protected void SetHandle (IntPtr value, JniHandleOwnership transfer)
		{
			JNIEnvInit.AndroidValueManager?.AddPeer (this, value, transfer, out handle);
			handle_type = JObjectRefType.Global;
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
			refs_added = 0;
			if (Environment.HasShutdownStarted) {
				return;
			}
			JniEnvironment.Runtime.ValueManager.FinalizePeer (this);
		}

#if JAVA_INTEROP
		JniManagedPeerStates IJavaPeerable.JniManagedPeerState {
			get {
				var e = (IJavaObjectEx) this;
				var s = JniManagedPeerStates.None;
				if (e.IsProxy)
					s |= JniManagedPeerStates.Replaceable;
				if (e.NeedsActivation)
					s |= JniManagedPeerStates.Activatable;
				return s;
			}
		}

		void IJavaPeerable.DisposeUnlessReferenced ()
		{
			var p = Object.PeekObject (handle);
			if (p == null) {
				Dispose ();
			}
		}

		public void UnregisterFromRuntime ()
		{
			JNIEnvInit.AndroidValueManager?.RemovePeer (this, key_handle);
		}

		void IJavaPeerable.Disposed ()
		{
			Dispose (disposing: true);
		}

		void IJavaPeerable.Finalized ()
		{
			Dispose (disposing: false);
		}

		void IJavaPeerable.SetJniIdentityHashCode (int value)
		{
			key_handle  = (IntPtr) value;
		}

		void IJavaPeerable.SetJniManagedPeerState (JniManagedPeerStates value)
		{
			var e = (IJavaObjectEx) this;
			if ((value & JniManagedPeerStates.Replaceable) == JniManagedPeerStates.Replaceable)
				e.IsProxy = true;
			if ((value & JniManagedPeerStates.Activatable) == JniManagedPeerStates.Activatable)
				e.NeedsActivation = true;
		}

		void IJavaPeerable.SetPeerReference (JniObjectReference reference)
		{
			this.handle         = reference.Handle;
			this.handle_type    = (JObjectRefType) reference.Type;
		}
#endif  // JAVA_INTEROP

		public void Dispose ()
		{
			JNIEnvInit.AndroidValueManager?.DisposePeer (this);
		}

		protected virtual void Dispose (bool disposing)
		{
		}
	}
}
