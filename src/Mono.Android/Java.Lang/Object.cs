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
	public partial class Object : global::Java.Interop.JavaObject, IJavaObject, IJavaObjectEx
	{
		IntPtr IJavaObjectEx.ToLocalJniHandle ()
		{
			lock (this) {
				var peerRef = PeerReference;
				if (!peerRef.IsValid)
					return IntPtr.Zero;
				return peerRef.NewLocalRef ().Handle;
			}
		}

		~Object ()
		{
		}

		public unsafe Object (IntPtr handle, JniHandleOwnership transfer)
			: base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
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

		[EditorBrowsable (EditorBrowsableState.Never)]
		public int JniIdentityHashCode => base.JniIdentityHashCode;

		[DebuggerBrowsable (DebuggerBrowsableState.Never)]
		[EditorBrowsable (EditorBrowsableState.Never)]
		public JniObjectReference PeerReference => base.PeerReference;

		[DebuggerBrowsable (DebuggerBrowsableState.Never)]
		[EditorBrowsable (EditorBrowsableState.Never)]
		public override JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		[EditorBrowsable (EditorBrowsableState.Never)]
		public IntPtr Handle {
			get {
				var peerRef = PeerReference;
				if (!peerRef.IsValid)
					return IntPtr.Zero;
				return peerRef.Handle;
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

		[EditorBrowsable (EditorBrowsableState.Never)]
		public new void UnregisterFromRuntime () => base.UnregisterFromRuntime ();

		protected override void Dispose (bool disposing)
		{
		}

		public new void Dispose () => base.Dispose ();

		[EditorBrowsable (EditorBrowsableState.Never)]
		protected void SetHandle (IntPtr value, JniHandleOwnership transfer)
		{
			var reference = new JniObjectReference (value);
			JNIEnvInit.ValueManager?.ConstructPeer (
					this,
					ref reference,
					value == IntPtr.Zero ? JniObjectReferenceOptions.None : JniObjectReferenceOptions.Copy);
			JNIEnv.DeleteRef (value, transfer);
		}

		internal static IJavaPeerable? PeekObject (IntPtr handle, Type? requiredType = null)
		{
			var peeked  = JNIEnvInit.ValueManager?.PeekPeer (new JniObjectReference (handle));
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
