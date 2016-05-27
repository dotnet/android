using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Java.Lang {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.lang']/class[@name='Enum']"
	[global::Android.Runtime.Register ("java/lang/Enum", DoNotGenerateAcw=true)]
	[global::Java.Interop.JavaTypeParameters (new string [] {"E extends java.lang.Enum<E>"})]
	public abstract partial class Enum : global::Java.Lang.Object, global::Java.Lang.IComparable {

		internal static IntPtr java_class_handle;
		internal static IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("java/lang/Enum", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (Enum); }
		}

		protected Enum (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static IntPtr id_compareTo_Ljava_lang_Enum_;
		// Metadata.xml XPath method reference: path="/api/package[@name='java.lang']/class[@name='Enum']/method[@name='compareTo' and count(parameter)=1 and parameter[1][@type='E']]"
		[Register ("compareTo", "(Ljava/lang/Enum;)I", "")]
		public unsafe int CompareTo (global::Java.Lang.Object o)
		{
			if (id_compareTo_Ljava_lang_Enum_ == IntPtr.Zero)
				id_compareTo_Ljava_lang_Enum_ = JNIEnv.GetMethodID (class_ref, "compareTo", "(Ljava/lang/Enum;)I");
			IntPtr native_o = JNIEnv.ToLocalJniHandle (o);
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (native_o);
				int __ret = JNIEnv.CallIntMethod (((global::Java.Lang.Object) this).Handle, id_compareTo_Ljava_lang_Enum_, __args);
				return __ret;
			} finally {
				JNIEnv.DeleteLocalRef (native_o);
			}
		}

	}

	[global::Android.Runtime.Register ("java/lang/Enum", DoNotGenerateAcw=true)]
	internal partial class EnumInvoker : Enum, global::Java.Lang.IComparable {

		public EnumInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

		protected override global::System.Type ThresholdType {
			get { return typeof (EnumInvoker); }
		}

	}

}
