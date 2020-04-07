using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Java.IO {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.io']/class[@name='InputStream']"
	[global::Android.Runtime.Register ("java/io/InputStream", DoNotGenerateAcw=true)]
	public abstract partial class InputStream : global::Java.Lang.Object {

		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("java/io/InputStream", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (InputStream); }
		}

		protected InputStream (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static IntPtr id_ctor;
		// Metadata.xml XPath constructor reference: path="/api/package[@name='java.io']/class[@name='InputStream']/constructor[@name='InputStream' and count(parameter)=0]"
		[Register (".ctor", "()V", "")]
		public unsafe InputStream ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			try {
				if (((object) this).GetType () != typeof (InputStream)) {
					SetHandle (
							global::Android.Runtime.JNIEnv.StartCreateInstance (((object) this).GetType (), "()V"),
							JniHandleOwnership.TransferLocalRef);
					global::Android.Runtime.JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, "()V");
					return;
				}

				if (id_ctor == IntPtr.Zero)
					id_ctor = JNIEnv.GetMethodID (class_ref, "<init>", "()V");
				SetHandle (
						global::Android.Runtime.JNIEnv.StartCreateInstance (class_ref, id_ctor),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, class_ref, id_ctor);
			} finally {
			}
		}

		static Delegate cb_available;
#pragma warning disable 0169
		static Delegate GetAvailableHandler ()
		{
			if (cb_available == null)
				cb_available = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, int>) n_Available);
			return cb_available;
		}

		static int n_Available (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return __this.Available ();
		}
#pragma warning restore 0169

		static IntPtr id_available;
		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='available' and count(parameter)=0]"
		[Register ("available", "()I", "GetAvailableHandler")]
		public virtual unsafe int Available ()
		{
			if (id_available == IntPtr.Zero)
				id_available = JNIEnv.GetMethodID (class_ref, "available", "()I");
			try {

				if (((object) this).GetType () == ThresholdType)
					return JNIEnv.CallIntMethod (((global::Java.Lang.Object) this).Handle, id_available);
				else
					return JNIEnv.CallNonvirtualIntMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "available", "()I"));
			} finally {
			}
		}

		static Delegate cb_close;
#pragma warning disable 0169
		static Delegate GetCloseHandler ()
		{
			if (cb_close == null)
				cb_close = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr>) n_Close);
			return cb_close;
		}

		static void n_Close (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Close ();
		}
#pragma warning restore 0169

		static IntPtr id_close;
		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='close' and count(parameter)=0]"
		[Register ("close", "()V", "GetCloseHandler")]
		public virtual unsafe void Close ()
		{
			if (id_close == IntPtr.Zero)
				id_close = JNIEnv.GetMethodID (class_ref, "close", "()V");
			try {

				if (((object) this).GetType () == ThresholdType)
					JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_close);
				else
					JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "close", "()V"));
			} finally {
			}
		}

		static Delegate cb_mark_I;
#pragma warning disable 0169
		static Delegate GetMark_IHandler ()
		{
			if (cb_mark_I == null)
				cb_mark_I = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, int>) n_Mark_I);
			return cb_mark_I;
		}

		static void n_Mark_I (IntPtr jnienv, IntPtr native__this, int readlimit)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Mark (readlimit);
		}
#pragma warning restore 0169

		static IntPtr id_mark_I;
		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='mark' and count(parameter)=1 and parameter[1][@type='int']]"
		[Register ("mark", "(I)V", "GetMark_IHandler")]
		public virtual unsafe void Mark (int readlimit)
		{
			if (id_mark_I == IntPtr.Zero)
				id_mark_I = JNIEnv.GetMethodID (class_ref, "mark", "(I)V");
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (readlimit);

				if (((object) this).GetType () == ThresholdType)
					JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_mark_I, __args);
				else
					JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "mark", "(I)V"), __args);
			} finally {
			}
		}

		static Delegate cb_markSupported;
#pragma warning disable 0169
		static Delegate GetMarkSupportedHandler ()
		{
			if (cb_markSupported == null)
				cb_markSupported = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, bool>) n_MarkSupported);
			return cb_markSupported;
		}

		static bool n_MarkSupported (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return __this.MarkSupported ();
		}
#pragma warning restore 0169

		static IntPtr id_markSupported;
		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='markSupported' and count(parameter)=0]"
		[Register ("markSupported", "()Z", "GetMarkSupportedHandler")]
		public virtual unsafe bool MarkSupported ()
		{
			if (id_markSupported == IntPtr.Zero)
				id_markSupported = JNIEnv.GetMethodID (class_ref, "markSupported", "()Z");
			try {

				if (((object) this).GetType () == ThresholdType)
					return JNIEnv.CallBooleanMethod (((global::Java.Lang.Object) this).Handle, id_markSupported);
				else
					return JNIEnv.CallNonvirtualBooleanMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "markSupported", "()Z"));
			} finally {
			}
		}

		static Delegate cb_read;
#pragma warning disable 0169
		static Delegate GetReadHandler ()
		{
			if (cb_read == null)
				cb_read = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, int>) n_Read);
			return cb_read;
		}

		static int n_Read (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return __this.Read ();
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='read' and count(parameter)=0]"
		[Register ("read", "()I", "GetReadHandler")]
		public abstract int Read ();

		static Delegate cb_read_arrayB;
#pragma warning disable 0169
		static Delegate GetRead_arrayBHandler ()
		{
			if (cb_read_arrayB == null)
				cb_read_arrayB = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr, int>) n_Read_arrayB);
			return cb_read_arrayB;
		}

		static int n_Read_arrayB (IntPtr jnienv, IntPtr native__this, IntPtr native_buffer)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var buffer = (byte[]) JNIEnv.GetArray (native_buffer, JniHandleOwnership.DoNotTransfer, typeof (byte));
			int __ret = __this.Read (buffer);
			if (buffer != null)
				JNIEnv.CopyArray (buffer, native_buffer);
			return __ret;
		}
#pragma warning restore 0169

		static IntPtr id_read_arrayB;
		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='read' and count(parameter)=1 and parameter[1][@type='byte[]']]"
		[Register ("read", "([B)I", "GetRead_arrayBHandler")]
		public virtual unsafe int Read (byte[] buffer)
		{
			if (id_read_arrayB == IntPtr.Zero)
				id_read_arrayB = JNIEnv.GetMethodID (class_ref, "read", "([B)I");
			IntPtr native_buffer = JNIEnv.NewArray (buffer);
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (native_buffer);

				int __ret;
				if (((object) this).GetType () == ThresholdType)
					__ret = JNIEnv.CallIntMethod (((global::Java.Lang.Object) this).Handle, id_read_arrayB, __args);
				else
					__ret = JNIEnv.CallNonvirtualIntMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "read", "([B)I"), __args);
				return __ret;
			} finally {
				if (buffer != null) {
					JNIEnv.CopyArray (native_buffer, buffer);
					JNIEnv.DeleteLocalRef (native_buffer);
				}
			}
		}

		static Delegate cb_read_arrayBII;
#pragma warning disable 0169
		static Delegate GetRead_arrayBIIHandler ()
		{
			if (cb_read_arrayBII == null)
				cb_read_arrayBII = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr, int, int, int>) n_Read_arrayBII);
			return cb_read_arrayBII;
		}

		static int n_Read_arrayBII (IntPtr jnienv, IntPtr native__this, IntPtr native_buffer, int byteOffset, int byteCount)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var buffer = (byte[]) JNIEnv.GetArray (native_buffer, JniHandleOwnership.DoNotTransfer, typeof (byte));
			int __ret = __this.Read (buffer, byteOffset, byteCount);
			if (buffer != null)
				JNIEnv.CopyArray (buffer, native_buffer);
			return __ret;
		}
#pragma warning restore 0169

		static IntPtr id_read_arrayBII;
		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='read' and count(parameter)=3 and parameter[1][@type='byte[]'] and parameter[2][@type='int'] and parameter[3][@type='int']]"
		[Register ("read", "([BII)I", "GetRead_arrayBIIHandler")]
		public virtual unsafe int Read (byte[] buffer, int byteOffset, int byteCount)
		{
			if (id_read_arrayBII == IntPtr.Zero)
				id_read_arrayBII = JNIEnv.GetMethodID (class_ref, "read", "([BII)I");
			IntPtr native_buffer = JNIEnv.NewArray (buffer);
			try {
				JValue* __args = stackalloc JValue [3];
				__args [0] = new JValue (native_buffer);
				__args [1] = new JValue (byteOffset);
				__args [2] = new JValue (byteCount);

				int __ret;
				if (((object) this).GetType () == ThresholdType)
					__ret = JNIEnv.CallIntMethod (((global::Java.Lang.Object) this).Handle, id_read_arrayBII, __args);
				else
					__ret = JNIEnv.CallNonvirtualIntMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "read", "([BII)I"), __args);
				return __ret;
			} finally {
				if (buffer != null) {
					JNIEnv.CopyArray (native_buffer, buffer);
					JNIEnv.DeleteLocalRef (native_buffer);
				}
			}
		}

		static Delegate cb_reset;
#pragma warning disable 0169
		static Delegate GetResetHandler ()
		{
			if (cb_reset == null)
				cb_reset = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr>) n_Reset);
			return cb_reset;
		}

		static void n_Reset (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Reset ();
		}
#pragma warning restore 0169

		static IntPtr id_reset;
		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='reset' and count(parameter)=0]"
		[Register ("reset", "()V", "GetResetHandler")]
		public virtual unsafe void Reset ()
		{
			if (id_reset == IntPtr.Zero)
				id_reset = JNIEnv.GetMethodID (class_ref, "reset", "()V");
			try {

				if (((object) this).GetType () == ThresholdType)
					JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_reset);
				else
					JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "reset", "()V"));
			} finally {
			}
		}

		static Delegate cb_skip_J;
#pragma warning disable 0169
		static Delegate GetSkip_JHandler ()
		{
			if (cb_skip_J == null)
				cb_skip_J = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, long, long>) n_Skip_J);
			return cb_skip_J;
		}

		static long n_Skip_J (IntPtr jnienv, IntPtr native__this, long byteCount)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return __this.Skip (byteCount);
		}
#pragma warning restore 0169

		static IntPtr id_skip_J;
		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='skip' and count(parameter)=1 and parameter[1][@type='long']]"
		[Register ("skip", "(J)J", "GetSkip_JHandler")]
		public virtual unsafe long Skip (long byteCount)
		{
			if (id_skip_J == IntPtr.Zero)
				id_skip_J = JNIEnv.GetMethodID (class_ref, "skip", "(J)J");
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (byteCount);

				if (((object) this).GetType () == ThresholdType)
					return JNIEnv.CallLongMethod (((global::Java.Lang.Object) this).Handle, id_skip_J, __args);
				else
					return JNIEnv.CallNonvirtualLongMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "skip", "(J)J"), __args);
			} finally {
			}
		}

	}

	[global::Android.Runtime.Register ("java/io/InputStream", DoNotGenerateAcw=true)]
	internal partial class InputStreamInvoker : InputStream {

		public InputStreamInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

		protected override global::System.Type ThresholdType {
			get { return typeof (InputStreamInvoker); }
		}

		static IntPtr id_read;
		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='read' and count(parameter)=0]"
		[Register ("read", "()I", "GetReadHandler")]
		public override unsafe int Read ()
		{
			if (id_read == IntPtr.Zero)
				id_read = JNIEnv.GetMethodID (class_ref, "read", "()I");
			try {
				return JNIEnv.CallIntMethod (((global::Java.Lang.Object) this).Handle, id_read);
			} finally {
			}
		}

	}

}
