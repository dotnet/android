using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Java.IO {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.io']/class[@name='InputStream']"
	[global::Android.Runtime.Register ("java/io/InputStream", DoNotGenerateAcw=true)]
	public abstract partial class InputStream : global::Java.Lang.Object {

		internal            static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("java/io/InputStream", typeof (InputStream));
		internal static IntPtr class_ref {
			get {
				return _members.JniPeerType.PeerReference.Handle;
			}
		}

		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected override IntPtr ThresholdClass {
			get { return _members.JniPeerType.PeerReference.Handle; }
		}

		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
		}

		protected InputStream (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='java.io']/class[@name='InputStream']/constructor[@name='InputStream' and count(parameter)=0]"
		[Register (".ctor", "()V", "")]
		public unsafe InputStream ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			const string __id = "()V";

			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			try {
				var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), null);
				SetHandle (__r.Handle, JniHandleOwnership.TransferLocalRef);
				_members.InstanceMethods.FinishCreateInstance (__id, this, null);
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
			global::Java.IO.InputStream __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return __this.Available ();
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='available' and count(parameter)=0]"
		[Register ("available", "()I", "GetAvailableHandler")]
		public virtual unsafe int Available ()
		{
			const string __id = "available.()I";
			try {
				var __rm = _members.InstanceMethods.InvokeVirtualInt32Method (__id, this, null);
				return __rm;
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
			global::Java.IO.InputStream __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Close ();
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='close' and count(parameter)=0]"
		[Register ("close", "()V", "GetCloseHandler")]
		public virtual unsafe void Close ()
		{
			const string __id = "close.()V";
			try {
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, null);
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
			global::Java.IO.InputStream __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Mark (readlimit);
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='mark' and count(parameter)=1 and parameter[1][@type='int']]"
		[Register ("mark", "(I)V", "GetMark_IHandler")]
		public virtual unsafe void Mark (int readlimit)
		{
			const string __id = "mark.(I)V";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (readlimit);
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
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
			global::Java.IO.InputStream __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return __this.MarkSupported ();
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='markSupported' and count(parameter)=0]"
		[Register ("markSupported", "()Z", "GetMarkSupportedHandler")]
		public virtual unsafe bool MarkSupported ()
		{
			const string __id = "markSupported.()Z";
			try {
				var __rm = _members.InstanceMethods.InvokeVirtualBooleanMethod (__id, this, null);
				return __rm;
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
			global::Java.IO.InputStream __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
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
			global::Java.IO.InputStream __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			byte[] buffer = (byte[]) JNIEnv.GetArray (native_buffer, JniHandleOwnership.DoNotTransfer, typeof (byte));
			int __ret = __this.Read (buffer);
			if (buffer != null)
				JNIEnv.CopyArray (buffer, native_buffer);
			return __ret;
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='read' and count(parameter)=1 and parameter[1][@type='byte[]']]"
		[Register ("read", "([B)I", "GetRead_arrayBHandler")]
		public virtual unsafe int Read (byte[] buffer)
		{
			const string __id = "read.([B)I";
			IntPtr native_buffer = JNIEnv.NewArray (buffer);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_buffer);
				var __rm = _members.InstanceMethods.InvokeVirtualInt32Method (__id, this, __args);
				return __rm;
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
			global::Java.IO.InputStream __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			byte[] buffer = (byte[]) JNIEnv.GetArray (native_buffer, JniHandleOwnership.DoNotTransfer, typeof (byte));
			int __ret = __this.Read (buffer, byteOffset, byteCount);
			if (buffer != null)
				JNIEnv.CopyArray (buffer, native_buffer);
			return __ret;
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='read' and count(parameter)=3 and parameter[1][@type='byte[]'] and parameter[2][@type='int'] and parameter[3][@type='int']]"
		[Register ("read", "([BII)I", "GetRead_arrayBIIHandler")]
		public virtual unsafe int Read (byte[] buffer, int byteOffset, int byteCount)
		{
			const string __id = "read.([BII)I";
			IntPtr native_buffer = JNIEnv.NewArray (buffer);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [3];
				__args [0] = new JniArgumentValue (native_buffer);
				__args [1] = new JniArgumentValue (byteOffset);
				__args [2] = new JniArgumentValue (byteCount);
				var __rm = _members.InstanceMethods.InvokeVirtualInt32Method (__id, this, __args);
				return __rm;
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
			global::Java.IO.InputStream __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Reset ();
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='reset' and count(parameter)=0]"
		[Register ("reset", "()V", "GetResetHandler")]
		public virtual unsafe void Reset ()
		{
			const string __id = "reset.()V";
			try {
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, null);
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
			global::Java.IO.InputStream __this = global::Java.Lang.Object.GetObject<global::Java.IO.InputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return __this.Skip (byteCount);
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='skip' and count(parameter)=1 and parameter[1][@type='long']]"
		[Register ("skip", "(J)J", "GetSkip_JHandler")]
		public virtual unsafe long Skip (long byteCount)
		{
			const string __id = "skip.(J)J";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (byteCount);
				var __rm = _members.InstanceMethods.InvokeVirtualInt64Method (__id, this, __args);
				return __rm;
			} finally {
			}
		}

	}

	[global::Android.Runtime.Register ("java/io/InputStream", DoNotGenerateAcw=true)]
	internal partial class InputStreamInvoker : InputStream {

		public InputStreamInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

		internal    new     static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("java/io/InputStream", typeof (InputStreamInvoker));

		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='read' and count(parameter)=0]"
		[Register ("read", "()I", "GetReadHandler")]
		public override unsafe int Read ()
		{
			const string __id = "read.()I";
			try {
				var __rm = _members.InstanceMethods.InvokeAbstractInt32Method (__id, this, null);
				return __rm;
			} finally {
			}
		}

	}

}
