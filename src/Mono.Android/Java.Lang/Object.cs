using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text;

using Java.Interop;

using Android.Runtime;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Java.Lang {

	[Serializable]
	public partial class Object : IDisposable, IJavaObject, IJavaObjectEx
#if JAVA_INTEROP
		, IJavaPeerable
#endif  // JAVA_INTEROP
	{
		[NonSerialized] IntPtr key_handle;
#pragma warning disable CS0649, CS0169, CS0414 // Suppress fields are never used warnings, these fields are used directly by monodroid-glue.cc
		[NonSerialized] int refs_added;
#pragma warning restore CS0649, CS0169, CS0414
		[NonSerialized] JObjectRefType handle_type;
		[NonSerialized] internal IntPtr handle;
		[NonSerialized] bool             needsActivation;
		[NonSerialized] bool             isProxy;

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

		~Object ()
		{
			// FIXME: need hash cleanup mechanism.
			// Finalization occurs after a test of java persistence.  If the
			// handle still contains a java reference, we can't finalize the
			// object and should "resurrect" it.
			refs_added = 0;
			if (Environment.HasShutdownStarted) {
				return;
			}
			JniEnvironment.Runtime.ValueManager.FinalizePeer (this);
		}

		public Object (IntPtr handle, JniHandleOwnership transfer)
		{
			// Check if handle was preset by our java activation mechanism
			if (this.handle != IntPtr.Zero) {
				needsActivation = true;
				handle = this.handle;
				if (handle_type != 0)
					return;
				transfer  = JniHandleOwnership.DoNotTransfer;
			}

			SetHandle (handle, transfer);
		}

		// Note: must be internal so that e.g. DataContractJsonSerializer will find it
		[OnDeserialized]
		internal void SetHandleOnDeserialized (StreamingContext context)
		{
			if (Handle != IntPtr.Zero)
				return;

			SetHandle (
					JNIEnv.StartCreateInstance (GetType (), "()V"),
					JniHandleOwnership.TransferLocalRef);
			JNIEnv.FinishCreateInstance (Handle, "()V");
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
		public IntPtr Handle {
			get {
				return handle;
			}
		}

		[DebuggerBrowsable (DebuggerBrowsableState.Never)]
		[EditorBrowsable (EditorBrowsableState.Never)]
		protected virtual IntPtr ThresholdClass {
			get { return Class.Object; }
		}

		[DebuggerBrowsable (DebuggerBrowsableState.Never)]
		[EditorBrowsable (EditorBrowsableState.Never)]
		protected virtual System.Type ThresholdType {
			get { return typeof (Java.Lang.Object); }
		}

		internal IntPtr GetThresholdClass ()
		{
			return ThresholdClass;
		}

		internal System.Type GetThresholdType ()
		{
			return ThresholdType;
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
			var p = PeekObject (handle);
			if (p == null) {
				Dispose ();
			}
		}

		[EditorBrowsable (EditorBrowsableState.Never)]
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

		internal static void Dispose (IJavaPeerable instance, ref IntPtr handle, IntPtr key_handle, JObjectRefType handle_type)
		{
			if (handle == IntPtr.Zero)
				return;

			if (Logger.LogGlobalRef) {
				RuntimeNativeMethods._monodroid_gref_log (
						FormattableString.Invariant ($"Disposing handle 0x{handle:x}\n"));
			}

			JNIEnvInit.AndroidValueManager?.RemovePeer (instance, key_handle);

			switch (handle_type) {
				case JObjectRefType.Global:
					lock (instance) {
						JNIEnv.DeleteGlobalRef (handle);
						handle = IntPtr.Zero;
					}
					break;
				case JObjectRefType.WeakGlobal:
					lock (instance) {
						JNIEnv.DeleteWeakGlobalRef (handle);
						handle = IntPtr.Zero;
					}
					break;
				default:
					throw new InvalidOperationException ("Trying to dispose handle of type '" +
							handle_type + "' which is not supported.");
			}
		}

		[EditorBrowsable (EditorBrowsableState.Never)]
		protected void SetHandle (IntPtr value, JniHandleOwnership transfer)
		{
			JNIEnvInit.AndroidValueManager?.AddPeer (this, value, transfer, out handle);
			handle_type = JObjectRefType.Global;
		}

		internal static IJavaPeerable? PeekObject (IntPtr handle, Type? requiredType = null)
		{
			var peeked  = JNIEnvInit.AndroidValueManager?.PeekPeer (new JniObjectReference (handle));
			if (peeked == null)
				return null;
			if (requiredType != null && !requiredType.IsAssignableFrom (peeked.GetType ()))
				return null;
			return peeked;
		}

		internal static T? PeekObject <T> (IntPtr handle)
		{
			return (T?)PeekObject (handle, typeof (T));
		}

		public static T? GetObject<T> (IntPtr jnienv, IntPtr handle, JniHandleOwnership transfer)
			where T : class, IJavaObject
		{
			JNIEnv.CheckHandle (jnienv);
			return GetObject<T> (handle, transfer);
		}

		public static T? GetObject<T> (IntPtr handle, JniHandleOwnership transfer)
			where T : class, IJavaObject
		{
			return _GetObject<T>(handle, transfer);
		}

		internal static T? _GetObject<T> (IntPtr handle, JniHandleOwnership transfer)
		{
			if (handle == IntPtr.Zero)
				return default (T);

			return (T?) GetObject (handle, transfer, typeof (T));
		}

		internal static IJavaPeerable? GetObject (IntPtr handle, JniHandleOwnership transfer, Type? type = null)
		{
			if (handle == IntPtr.Zero)
				return null;

			var r = PeekObject (handle, type);
			if (r != null) {
				JNIEnv.DeleteRef (handle, transfer);
				return r;
			}

			return Java.Interop.TypeManager.CreateInstance (handle, transfer, type);
		}

		[EditorBrowsable (EditorBrowsableState.Never)]
		public T[]? ToArray<T>()
		{
			return JNIEnv.GetArray<T>(Handle);
		}

		public static Java.Lang.Object? FromArray<T>(T[] value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Interoperability", "CA1422:Validate platform compatibility", Justification = "Suggested replacement uses instance sharing")]
		public static implicit operator Java.Lang.Object (bool value)
		{
			return new Java.Lang.Boolean (value);
		}

		[Obsolete ("Use `(Java.Lang.Byte)(sbyte) value`", error: true)]
		public static implicit operator Java.Lang.Object (byte value)
		{
			throw new InvalidOperationException ("Should not be reached");
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Interoperability", "CA1422:Validate platform compatibility", Justification = "Suggested replacement uses instance sharing")]
		public static implicit operator Java.Lang.Object (sbyte value)
		{
			return new Java.Lang.Byte (value);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Interoperability", "CA1422:Validate platform compatibility", Justification = "Suggested replacement uses instance sharing")]
		public static implicit operator Java.Lang.Object (char value)
		{
			return new Java.Lang.Character (value);
		}

		[Obsolete ("Use `(Java.Lang.Integer)(int) value`", error: true)]
		public static implicit operator Java.Lang.Object (uint value)
		{
			throw new InvalidOperationException ("Should not be reached");
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Interoperability", "CA1422:Validate platform compatibility", Justification = "Suggested replacement uses instance sharing")]
		public static implicit operator Java.Lang.Object (int value)
		{
			return new Java.Lang.Integer (value);
		}

		[Obsolete ("Use `(Java.Lang.Long)(long) value`", error: true)]
		public static implicit operator Java.Lang.Object (ulong value)
		{
			throw new InvalidOperationException ("Should not be reached");
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Interoperability", "CA1422:Validate platform compatibility", Justification = "Suggested replacement uses instance sharing")]
		public static implicit operator Java.Lang.Object (long value)
		{
			return new Java.Lang.Long (value);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Interoperability", "CA1422:Validate platform compatibility", Justification = "Suggested replacement uses instance sharing")]
		public static implicit operator Java.Lang.Object (float value)
		{
			return new Java.Lang.Float (value);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Interoperability", "CA1422:Validate platform compatibility", Justification = "Suggested replacement uses instance sharing")]
		public static implicit operator Java.Lang.Object (double value)
		{
			return new Java.Lang.Double (value);
		}

		public static implicit operator Java.Lang.Object? (string? value)
		{
			if (value == null)
				return null;
			return new Java.Lang.ICharSequenceInvoker (JNIEnv.NewString (value), JniHandleOwnership.TransferLocalRef);
		}

		public static explicit operator bool (Java.Lang.Object value)
		{
			return Convert.ToBoolean (value, CultureInfo.InvariantCulture);
		}

		[Obsolete ("Use `(byte)(sbyte) value`", error: true)]
		public static explicit operator byte (Java.Lang.Object value)
		{
			throw new InvalidOperationException ("Should not be reached");
		}

		public static explicit operator sbyte (Java.Lang.Object value)
		{
			return Convert.ToSByte (value, CultureInfo.InvariantCulture);
		}

		public static explicit operator char (Java.Lang.Object value)
		{
			return Convert.ToChar (value, CultureInfo.InvariantCulture);
		}

		[Obsolete ("Use `(uint)(int) value`", error: true)]
		public static explicit operator uint (Java.Lang.Object value)
		{
			throw new InvalidOperationException ("Should not be reached");
		}

		public static explicit operator int (Java.Lang.Object value)
		{
			return Convert.ToInt32 (value, CultureInfo.InvariantCulture);
		}

		[Obsolete ("Use `(ulong)(long) value`", error: true)]
		public static explicit operator ulong (Java.Lang.Object value)
		{
			throw new InvalidOperationException ("Should not be reached");
		}

		public static explicit operator long (Java.Lang.Object value)
		{
			return Convert.ToInt64 (value, CultureInfo.InvariantCulture);
		}

		public static explicit operator float (Java.Lang.Object value)
		{
			return Convert.ToSingle (value, CultureInfo.InvariantCulture);
		}

		public static explicit operator double (Java.Lang.Object value)
		{
			return Convert.ToDouble (value, CultureInfo.InvariantCulture);
		}

		public static explicit operator string? (Java.Lang.Object? value)
		{
			if (value == null)
				return null;
			return Convert.ToString (value, CultureInfo.InvariantCulture);
		}

		[return: NotNullIfNotNull ("value")]
		public static implicit operator Java.Lang.Object? (Java.Lang.Object[]? value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		[return: NotNullIfNotNull ("value")]
		public static implicit operator Java.Lang.Object? (bool[]? value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		[return: NotNullIfNotNull ("value")]
		public static implicit operator Java.Lang.Object? (byte[]? value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		[return: NotNullIfNotNull ("value")]
		public static implicit operator Java.Lang.Object? (char[]? value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		[return: NotNullIfNotNull ("value")]
		public static implicit operator Java.Lang.Object? (int[]? value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		[return: NotNullIfNotNull ("value")]
		public static implicit operator Java.Lang.Object? (long[]? value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		[return: NotNullIfNotNull ("value")]
		public static implicit operator Java.Lang.Object? (float[]? value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		[return: NotNullIfNotNull ("value")]
		public static implicit operator Java.Lang.Object? (double[]? value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		[return: NotNullIfNotNull ("value")]
		public static implicit operator Java.Lang.Object? (string[]? value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		public static explicit operator Java.Lang.Object[]? (Java.Lang.Object? value)
		{
			if (value == null)
				return null;
			return value.ToArray<Java.Lang.Object>();
		}

		public static explicit operator bool[]? (Java.Lang.Object? value)
		{
			if (value == null)
				return null;
			return value.ToArray<bool>();
		}

		public static explicit operator byte[]? (Java.Lang.Object? value)
		{
			if (value == null)
				return null;
			return value.ToArray<byte>();
		}

		public static explicit operator char[]? (Java.Lang.Object? value)
		{
			if (value == null)
				return null;
			return value.ToArray<char>();
		}

		public static explicit operator int[]? (Java.Lang.Object? value)
		{
			if (value == null)
				return null;
			return value.ToArray<int>();
		}

		public static explicit operator long[]? (Java.Lang.Object? value)
		{
			if (value == null)
				return null;
			return value.ToArray<long>();
		}

		public static explicit operator float[]? (Java.Lang.Object? value)
		{
			if (value == null)
				return null;
			return value.ToArray<float>();
		}

		public static explicit operator double[]? (Java.Lang.Object? value)
		{
			if (value == null)
				return null;
			return value.ToArray<double>();
		}

		public static explicit operator string[]? (Java.Lang.Object? value)
		{
			if (value == null)
				return null;
			return value.ToArray<string>();
		}
	}
}
