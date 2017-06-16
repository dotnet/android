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
		public IList<string> MyStrings {
			get {
				if (myStrings_jfieldId == IntPtr.Zero)
					myStrings_jfieldId = JNIEnv.GetFieldID (class_ref, "myStrings", "[Ljava/lang/String;");
				return global::Android.Runtime.JavaArray<string>.FromJniHandle (JNIEnv.GetObjectField (((global::Java.Lang.Object) this).Handle, myStrings_jfieldId), JniHandleOwnership.TransferLocalRef);
			}
			set {
				if (myStrings_jfieldId == IntPtr.Zero)
					myStrings_jfieldId = JNIEnv.GetFieldID (class_ref, "myStrings", "[Ljava/lang/String;");
				IntPtr native_value = global::Android.Runtime.JavaArray<string>.ToLocalJniHandle (value);
				try {
					JNIEnv.SetField (((global::Java.Lang.Object) this).Handle, myStrings_jfieldId, native_value);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

		static IntPtr myInts_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/field[@name='myInts']"
		[Register ("myInts")]
		public IList<int> MyInts {
			get {
				if (myInts_jfieldId == IntPtr.Zero)
					myInts_jfieldId = JNIEnv.GetFieldID (class_ref, "myInts", "[I");
				return global::Android.Runtime.JavaArray<int>.FromJniHandle (JNIEnv.GetObjectField (((global::Java.Lang.Object) this).Handle, myInts_jfieldId), JniHandleOwnership.TransferLocalRef);
			}
			set {
				if (myInts_jfieldId == IntPtr.Zero)
					myInts_jfieldId = JNIEnv.GetFieldID (class_ref, "myInts", "[I");
				IntPtr native_value = global::Android.Runtime.JavaArray<int>.ToLocalJniHandle (value);
				try {
					JNIEnv.SetField (((global::Java.Lang.Object) this).Handle, myInts_jfieldId, native_value);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

		static IntPtr mybools_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/field[@name='mybools']"
		[Register ("mybools")]
		public IList<bool> Mybools {
			get {
				if (mybools_jfieldId == IntPtr.Zero)
					mybools_jfieldId = JNIEnv.GetFieldID (class_ref, "mybools", "[Z");
				return global::Android.Runtime.JavaArray<bool>.FromJniHandle (JNIEnv.GetObjectField (((global::Java.Lang.Object) this).Handle, mybools_jfieldId), JniHandleOwnership.TransferLocalRef);
			}
			set {
				if (mybools_jfieldId == IntPtr.Zero)
					mybools_jfieldId = JNIEnv.GetFieldID (class_ref, "mybools", "[Z");
				IntPtr native_value = global::Android.Runtime.JavaArray<bool>.ToLocalJniHandle (value);
				try {
					JNIEnv.SetField (((global::Java.Lang.Object) this).Handle, mybools_jfieldId, native_value);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

		static IntPtr myObjects_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/field[@name='myObjects']"
		[Register ("myObjects")]
		public IList<Java.Lang.Object> MyObjects {
			get {
				if (myObjects_jfieldId == IntPtr.Zero)
					myObjects_jfieldId = JNIEnv.GetFieldID (class_ref, "myObjects", "[Ljava/lang/Object;");
				return global::Android.Runtime.JavaArray<global::Java.Lang.Object>.FromJniHandle (JNIEnv.GetObjectField (((global::Java.Lang.Object) this).Handle, myObjects_jfieldId), JniHandleOwnership.TransferLocalRef);
			}
			set {
				if (myObjects_jfieldId == IntPtr.Zero)
					myObjects_jfieldId = JNIEnv.GetFieldID (class_ref, "myObjects", "[Ljava/lang/Object;");
				IntPtr native_value = global::Android.Runtime.JavaArray<global::Java.Lang.Object>.ToLocalJniHandle (value);
				try {
					JNIEnv.SetField (((global::Java.Lang.Object) this).Handle, myObjects_jfieldId, native_value);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

		static IntPtr myfloats_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/field[@name='myfloats']"
		[Register ("myfloats")]
		public IList<float> Myfloats {
			get {
				if (myfloats_jfieldId == IntPtr.Zero)
					myfloats_jfieldId = JNIEnv.GetFieldID (class_ref, "myfloats", "[F");
				return global::Android.Runtime.JavaArray<float>.FromJniHandle (JNIEnv.GetObjectField (((global::Java.Lang.Object) this).Handle, myfloats_jfieldId), JniHandleOwnership.TransferLocalRef);
			}
			set {
				if (myfloats_jfieldId == IntPtr.Zero)
					myfloats_jfieldId = JNIEnv.GetFieldID (class_ref, "myfloats", "[F");
				IntPtr native_value = global::Android.Runtime.JavaArray<float>.ToLocalJniHandle (value);
				try {
					JNIEnv.SetField (((global::Java.Lang.Object) this).Handle, myfloats_jfieldId, native_value);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

		static IntPtr mydoubles_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/field[@name='mydoubles']"
		[Register ("mydoubles")]
		public IList<double> Mydoubles {
			get {
				if (mydoubles_jfieldId == IntPtr.Zero)
					mydoubles_jfieldId = JNIEnv.GetFieldID (class_ref, "mydoubles", "[D");
				return global::Android.Runtime.JavaArray<double>.FromJniHandle (JNIEnv.GetObjectField (((global::Java.Lang.Object) this).Handle, mydoubles_jfieldId), JniHandleOwnership.TransferLocalRef);
			}
			set {
				if (mydoubles_jfieldId == IntPtr.Zero)
					mydoubles_jfieldId = JNIEnv.GetFieldID (class_ref, "mydoubles", "[D");
				IntPtr native_value = global::Android.Runtime.JavaArray<double>.ToLocalJniHandle (value);
				try {
					JNIEnv.SetField (((global::Java.Lang.Object) this).Handle, mydoubles_jfieldId, native_value);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

		static IntPtr mylongs_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/field[@name='mylongs']"
		[Register ("mylongs")]
		public IList<long> Mylongs {
			get {
				if (mylongs_jfieldId == IntPtr.Zero)
					mylongs_jfieldId = JNIEnv.GetFieldID (class_ref, "mylongs", "[J");
				return global::Android.Runtime.JavaArray<long>.FromJniHandle (JNIEnv.GetObjectField (((global::Java.Lang.Object) this).Handle, mylongs_jfieldId), JniHandleOwnership.TransferLocalRef);
			}
			set {
				if (mylongs_jfieldId == IntPtr.Zero)
					mylongs_jfieldId = JNIEnv.GetFieldID (class_ref, "mylongs", "[J");
				IntPtr native_value = global::Android.Runtime.JavaArray<long>.ToLocalJniHandle (value);
				try {
					JNIEnv.SetField (((global::Java.Lang.Object) this).Handle, mylongs_jfieldId, native_value);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}
		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
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
