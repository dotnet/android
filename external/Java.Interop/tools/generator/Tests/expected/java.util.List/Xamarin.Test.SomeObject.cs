using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']"
	[global::Android.Runtime.Register ("xamarin/test/SomeObject", DoNotGenerateAcw=true)]
	public partial class SomeObject : global::Java.Lang.Object {


		static IntPtr myStrings_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/field[@name='myStrings']"
		[Register ("myStrings")]
		public global::System.Collections.Generic.IList<string> MyStrings {
			get {
				if (myStrings_jfieldId == IntPtr.Zero)
					myStrings_jfieldId = JNIEnv.GetFieldID (class_ref, "myStrings", "Ljava/util/List;");
				IntPtr __ret = JNIEnv.GetObjectField (Handle, myStrings_jfieldId);
				return global::Android.Runtime.JavaList<string>.FromJniHandle (__ret, JniHandleOwnership.TransferLocalRef);
			}
			set {
				if (myStrings_jfieldId == IntPtr.Zero)
					myStrings_jfieldId = JNIEnv.GetFieldID (class_ref, "myStrings", "Ljava/util/List;");
				IntPtr native_value = global::Android.Runtime.JavaList<string>.ToLocalJniHandle (value);
				try {
					JNIEnv.SetField (Handle, myStrings_jfieldId, native_value);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

		static IntPtr myInts_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/field[@name='myInts']"
		[Register ("myInts")]
		public global::System.Collections.Generic.IList<int> MyInts {
			get {
				if (myInts_jfieldId == IntPtr.Zero)
					myInts_jfieldId = JNIEnv.GetFieldID (class_ref, "myInts", "Ljava/util/List;");
				IntPtr __ret = JNIEnv.GetObjectField (Handle, myInts_jfieldId);
				return global::Android.Runtime.JavaList<int>.FromJniHandle (__ret, JniHandleOwnership.TransferLocalRef);
			}
			set {
				if (myInts_jfieldId == IntPtr.Zero)
					myInts_jfieldId = JNIEnv.GetFieldID (class_ref, "myInts", "Ljava/util/List;");
				IntPtr native_value = global::Android.Runtime.JavaList<int>.ToLocalJniHandle (value);
				try {
					JNIEnv.SetField (Handle, myInts_jfieldId, native_value);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

		static IntPtr mybools_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/field[@name='mybools']"
		[Register ("mybools")]
		public global::System.Collections.Generic.IList<bool> Mybools {
			get {
				if (mybools_jfieldId == IntPtr.Zero)
					mybools_jfieldId = JNIEnv.GetFieldID (class_ref, "mybools", "Ljava/util/List;");
				IntPtr __ret = JNIEnv.GetObjectField (Handle, mybools_jfieldId);
				return global::Android.Runtime.JavaList<bool>.FromJniHandle (__ret, JniHandleOwnership.TransferLocalRef);
			}
			set {
				if (mybools_jfieldId == IntPtr.Zero)
					mybools_jfieldId = JNIEnv.GetFieldID (class_ref, "mybools", "Ljava/util/List;");
				IntPtr native_value = global::Android.Runtime.JavaList<bool>.ToLocalJniHandle (value);
				try {
					JNIEnv.SetField (Handle, mybools_jfieldId, native_value);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

		static IntPtr myObjects_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/field[@name='myObjects']"
		[Register ("myObjects")]
		public global::System.Collections.Generic.IList<global::Java.Lang.Object> MyObjects {
			get {
				if (myObjects_jfieldId == IntPtr.Zero)
					myObjects_jfieldId = JNIEnv.GetFieldID (class_ref, "myObjects", "Ljava/util/List;");
				IntPtr __ret = JNIEnv.GetObjectField (Handle, myObjects_jfieldId);
				return global::Android.Runtime.JavaList<global::Java.Lang.Object>.FromJniHandle (__ret, JniHandleOwnership.TransferLocalRef);
			}
			set {
				if (myObjects_jfieldId == IntPtr.Zero)
					myObjects_jfieldId = JNIEnv.GetFieldID (class_ref, "myObjects", "Ljava/util/List;");
				IntPtr native_value = global::Android.Runtime.JavaList<global::Java.Lang.Object>.ToLocalJniHandle (value);
				try {
					JNIEnv.SetField (Handle, myObjects_jfieldId, native_value);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

		static IntPtr myfloats_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/field[@name='myfloats']"
		[Register ("myfloats")]
		public global::System.Collections.Generic.IList<float> Myfloats {
			get {
				if (myfloats_jfieldId == IntPtr.Zero)
					myfloats_jfieldId = JNIEnv.GetFieldID (class_ref, "myfloats", "Ljava/util/List;");
				IntPtr __ret = JNIEnv.GetObjectField (Handle, myfloats_jfieldId);
				return global::Android.Runtime.JavaList<float>.FromJniHandle (__ret, JniHandleOwnership.TransferLocalRef);
			}
			set {
				if (myfloats_jfieldId == IntPtr.Zero)
					myfloats_jfieldId = JNIEnv.GetFieldID (class_ref, "myfloats", "Ljava/util/List;");
				IntPtr native_value = global::Android.Runtime.JavaList<float>.ToLocalJniHandle (value);
				try {
					JNIEnv.SetField (Handle, myfloats_jfieldId, native_value);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

		static IntPtr mydoubles_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/field[@name='mydoubles']"
		[Register ("mydoubles")]
		public global::System.Collections.Generic.IList<double> Mydoubles {
			get {
				if (mydoubles_jfieldId == IntPtr.Zero)
					mydoubles_jfieldId = JNIEnv.GetFieldID (class_ref, "mydoubles", "Ljava/util/List;");
				IntPtr __ret = JNIEnv.GetObjectField (Handle, mydoubles_jfieldId);
				return global::Android.Runtime.JavaList<double>.FromJniHandle (__ret, JniHandleOwnership.TransferLocalRef);
			}
			set {
				if (mydoubles_jfieldId == IntPtr.Zero)
					mydoubles_jfieldId = JNIEnv.GetFieldID (class_ref, "mydoubles", "Ljava/util/List;");
				IntPtr native_value = global::Android.Runtime.JavaList<double>.ToLocalJniHandle (value);
				try {
					JNIEnv.SetField (Handle, mydoubles_jfieldId, native_value);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

		static IntPtr mylongs_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/field[@name='mylongs']"
		[Register ("mylongs")]
		public global::System.Collections.Generic.IList<long> Mylongs {
			get {
				if (mylongs_jfieldId == IntPtr.Zero)
					mylongs_jfieldId = JNIEnv.GetFieldID (class_ref, "mylongs", "Ljava/util/List;");
				IntPtr __ret = JNIEnv.GetObjectField (Handle, mylongs_jfieldId);
				return global::Android.Runtime.JavaList<long>.FromJniHandle (__ret, JniHandleOwnership.TransferLocalRef);
			}
			set {
				if (mylongs_jfieldId == IntPtr.Zero)
					mylongs_jfieldId = JNIEnv.GetFieldID (class_ref, "mylongs", "Ljava/util/List;");
				IntPtr native_value = global::Android.Runtime.JavaList<long>.ToLocalJniHandle (value);
				try {
					JNIEnv.SetField (Handle, mylongs_jfieldId, native_value);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}
		internal static IntPtr java_class_handle;
		internal static IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("xamarin/test/SomeObject", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (SomeObject); }
		}

		protected SomeObject (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

	}
}
