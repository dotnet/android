using System;
using System.Collections.Generic;
using Java.Interop;

namespace Java.IO {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.io']/class[@name='OutputStream']"
	[global::Java.Interop.JniTypeSignature ("java/io/OutputStream", GenerateJavaPeer=false)]
	public abstract partial class OutputStream : global::Java.Lang.Object {
		static readonly JniPeerMembers _members = new JniPeerMembers ("java/io/OutputStream", typeof (OutputStream));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected OutputStream (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='java.io']/class[@name='OutputStream']/constructor[@name='OutputStream' and count(parameter)=0]"
		public unsafe OutputStream () : base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			const string __id = "()V";

			if (PeerReference.IsValid)
				return;

			try {
				var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), null);
				Construct (ref __r, JniObjectReferenceOptions.CopyAndDispose);
				_members.InstanceMethods.FinishCreateInstance (__id, this, null);
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='OutputStream']/method[@name='close' and count(parameter)=0]"
		public virtual unsafe void Close ()
		{
			const string __id = "close.()V";
			try {
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, null);
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='OutputStream']/method[@name='flush' and count(parameter)=0]"
		public virtual unsafe void Flush ()
		{
			const string __id = "flush.()V";
			try {
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, null);
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='OutputStream']/method[@name='write' and count(parameter)=1 and parameter[1][@type='byte[]']]"
		public virtual unsafe void Write (global::Java.Interop.JavaSByteArray buffer)
		{
			const string __id = "write.([B)V";
			var native_buffer = global::Java.Interop.JniEnvironment.Arrays.CreateMarshalSByteArray (buffer);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_buffer);
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
			} finally {
				if (native_buffer != null) {
					native_buffer.DisposeUnlessReferenced ();
				}
				global::System.GC.KeepAlive (buffer);
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='OutputStream']/method[@name='write' and count(parameter)=3 and parameter[1][@type='byte[]'] and parameter[2][@type='int'] and parameter[3][@type='int']]"
		public virtual unsafe void Write (global::Java.Interop.JavaSByteArray buffer, int offset, int count)
		{
			const string __id = "write.([BII)V";
			var native_buffer = global::Java.Interop.JniEnvironment.Arrays.CreateMarshalSByteArray (buffer);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [3];
				__args [0] = new JniArgumentValue (native_buffer);
				__args [1] = new JniArgumentValue (offset);
				__args [2] = new JniArgumentValue (count);
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
			} finally {
				if (native_buffer != null) {
					native_buffer.DisposeUnlessReferenced ();
				}
				global::System.GC.KeepAlive (buffer);
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='OutputStream']/method[@name='write' and count(parameter)=1 and parameter[1][@type='int']]"
		public abstract void Write (int oneByte);

	}

	[global::Java.Interop.JniTypeSignature ("java/io/OutputStream", GenerateJavaPeer=false)]
	internal partial class OutputStreamInvoker : OutputStream {
		public OutputStreamInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		static readonly JniPeerMembers _members = new JniPeerMembers ("java/io/OutputStream", typeof (OutputStreamInvoker));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='OutputStream']/method[@name='write' and count(parameter)=1 and parameter[1][@type='int']]"
		public override unsafe void Write (int oneByte)
		{
			const string __id = "write.(I)V";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (oneByte);
				_members.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
			} finally {
			}
		}

	}
}
