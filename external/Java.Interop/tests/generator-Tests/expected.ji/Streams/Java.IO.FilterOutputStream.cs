using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Java.IO {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.io']/class[@name='FilterOutputStream']"
	[global::Android.Runtime.Register ("java/io/FilterOutputStream", DoNotGenerateAcw=true)]
	public partial class FilterOutputStream : global::Java.IO.OutputStream {

		static readonly JniPeerMembers _members = new JniPeerMembers ("java/io/FilterOutputStream", typeof (FilterOutputStream));
		internal static new IntPtr class_ref {
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

		protected FilterOutputStream (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='java.io']/class[@name='FilterOutputStream']/constructor[@name='FilterOutputStream' and count(parameter)=1 and parameter[1][@type='java.io.OutputStream']]"
		[Register (".ctor", "(Ljava/io/OutputStream;)V", "")]
		public unsafe FilterOutputStream (global::System.IO.Stream @out)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			const string __id = "(Ljava/io/OutputStream;)V";

			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			IntPtr native__out = global::Android.Runtime.OutputStreamAdapter.ToLocalJniHandle (@out);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native__out);
				var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), __args);
				SetHandle (__r.Handle, JniHandleOwnership.TransferLocalRef);
				_members.InstanceMethods.FinishCreateInstance (__id, this, __args);
			} finally {
				JNIEnv.DeleteLocalRef (native__out);
			}
		}

		static Delegate cb_write_I;
#pragma warning disable 0169
		static Delegate GetWrite_IHandler ()
		{
			if (cb_write_I == null)
				cb_write_I = JNINativeWrapper.CreateDelegate ((_JniMarshal_PPI_V) n_Write_I);
			return cb_write_I;
		}

		static void n_Write_I (IntPtr jnienv, IntPtr native__this, int oneByte)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Java.IO.FilterOutputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Write (oneByte);
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='FilterOutputStream']/method[@name='write' and count(parameter)=1 and parameter[1][@type='int']]"
		[Register ("write", "(I)V", "GetWrite_IHandler")]
		public override unsafe void Write (int oneByte)
		{
			const string __id = "write.(I)V";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (oneByte);
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
			} finally {
			}
		}

	}
}
