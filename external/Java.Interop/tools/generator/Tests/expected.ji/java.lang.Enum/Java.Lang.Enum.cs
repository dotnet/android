using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Java.Lang {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.lang']/class[@name='Enum']"
	[global::Android.Runtime.Register ("java/lang/Enum", DoNotGenerateAcw=true)]
	[global::Java.Interop.JavaTypeParameters (new string [] {"E extends java.lang.Enum<E>"})]
	public abstract partial class Enum : global::Java.Lang.Object, global::Java.Lang.IComparable {

		internal    new     static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("java/lang/Enum", typeof (Enum));
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

		protected Enum (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		// Metadata.xml XPath method reference: path="/api/package[@name='java.lang']/class[@name='Enum']/method[@name='compareTo' and count(parameter)=1 and parameter[1][@type='E']]"
		[Register ("compareTo", "(Ljava/lang/Enum;)I", "")]
		public unsafe int CompareTo (global::Java.Lang.Object o)
		{
			const string __id = "compareTo.(Ljava/lang/Enum;)I";
			IntPtr native_o = JNIEnv.ToLocalJniHandle (o);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_o);
				var __rm = _members.InstanceMethods.InvokeNonvirtualInt32Method (__id, this, __args);
				return __rm;
			} finally {
				JNIEnv.DeleteLocalRef (native_o);
			}
		}

	}

	[global::Android.Runtime.Register ("java/lang/Enum", DoNotGenerateAcw=true)]
	internal partial class EnumInvoker : Enum, global::Java.Lang.IComparable {

		public EnumInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

		internal    new     static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("java/lang/Enum", typeof (EnumInvoker));

		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
		}

	}

}
