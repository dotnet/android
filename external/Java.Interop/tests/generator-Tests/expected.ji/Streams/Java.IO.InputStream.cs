using System;
using System.Collections.Generic;
using Java.Interop;

namespace Java.IO {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.io']/class[@name='InputStream']"
	[global::Java.Interop.JniTypeSignature ("java/io/InputStream", GenerateJavaPeer=false)]
	public abstract partial class InputStream : global::Java.Lang.Object {
		static readonly JniPeerMembers _members = new JniPeerMembers ("java/io/InputStream", typeof (InputStream));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected InputStream (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='java.io']/class[@name='InputStream']/constructor[@name='InputStream' and count(parameter)=0]"
		public unsafe InputStream () : base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
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

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='available' and count(parameter)=0]"
		public virtual unsafe int Available ()
		{
			const string __id = "available.()I";
			try {
				var __rm = _members.InstanceMethods.InvokeVirtualInt32Method (__id, this, null);
				return __rm;
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='close' and count(parameter)=0]"
		public virtual unsafe void Close ()
		{
			const string __id = "close.()V";
			try {
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, null);
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='mark' and count(parameter)=1 and parameter[1][@type='int']]"
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

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='markSupported' and count(parameter)=0]"
		public virtual unsafe bool MarkSupported ()
		{
			const string __id = "markSupported.()Z";
			try {
				var __rm = _members.InstanceMethods.InvokeVirtualBooleanMethod (__id, this, null);
				return __rm;
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='read' and count(parameter)=0]"
		public abstract int Read ();

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='read' and count(parameter)=1 and parameter[1][@type='byte[]']]"
		public virtual unsafe int Read (global::Java.Interop.JavaSByteArray buffer)
		{
			const string __id = "read.([B)I";
			var native_buffer = global::Java.Interop.JniEnvironment.Arrays.CreateMarshalSByteArray (buffer);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_buffer);
				var __rm = _members.InstanceMethods.InvokeVirtualInt32Method (__id, this, __args);
				return __rm;
			} finally {
				if (native_buffer != null) {
					native_buffer.DisposeUnlessReferenced ();
				}
				global::System.GC.KeepAlive (buffer);
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='read' and count(parameter)=3 and parameter[1][@type='byte[]'] and parameter[2][@type='int'] and parameter[3][@type='int']]"
		public virtual unsafe int Read (global::Java.Interop.JavaSByteArray buffer, int byteOffset, int byteCount)
		{
			const string __id = "read.([BII)I";
			var native_buffer = global::Java.Interop.JniEnvironment.Arrays.CreateMarshalSByteArray (buffer);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [3];
				__args [0] = new JniArgumentValue (native_buffer);
				__args [1] = new JniArgumentValue (byteOffset);
				__args [2] = new JniArgumentValue (byteCount);
				var __rm = _members.InstanceMethods.InvokeVirtualInt32Method (__id, this, __args);
				return __rm;
			} finally {
				if (native_buffer != null) {
					native_buffer.DisposeUnlessReferenced ();
				}
				global::System.GC.KeepAlive (buffer);
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='reset' and count(parameter)=0]"
		public virtual unsafe void Reset ()
		{
			const string __id = "reset.()V";
			try {
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, null);
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='skip' and count(parameter)=1 and parameter[1][@type='long']]"
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

	[global::Java.Interop.JniTypeSignature ("java/io/InputStream", GenerateJavaPeer=false)]
	internal partial class InputStreamInvoker : InputStream {
		public InputStreamInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		static readonly JniPeerMembers _members = new JniPeerMembers ("java/io/InputStream", typeof (InputStreamInvoker));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='InputStream']/method[@name='read' and count(parameter)=0]"
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
