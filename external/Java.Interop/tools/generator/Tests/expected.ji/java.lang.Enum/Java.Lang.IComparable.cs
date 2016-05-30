using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Java.Lang {

	// Metadata.xml XPath interface reference: path="/api/package[@name='java.lang']/interface[@name='Comparable']"
	[Register ("java/lang/Comparable", "", "Java.Lang.IComparableInvoker")]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T"})]
	public partial interface IComparable : IJavaObject {

		// Metadata.xml XPath method reference: path="/api/package[@name='java.lang']/interface[@name='Comparable']/method[@name='compareTo' and count(parameter)=1 and parameter[1][@type='T']]"
		[Register ("compareTo", "(Ljava/lang/Object;)I", "GetCompareTo_Ljava_lang_Object_Handler:Java.Lang.IComparableInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		int CompareTo (global::Java.Lang.Object another);

	}

	[global::Android.Runtime.Register ("java/lang/Comparable", DoNotGenerateAcw=true)]
	internal class IComparableInvoker : global::Java.Lang.Object, IComparable {

		internal    new     static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("java/lang/Comparable", typeof (IComparableInvoker));

		static IntPtr java_class_ref {
			get { return _members.JniPeerType.PeerReference.Handle; }
		}

		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
		}

		IntPtr class_ref;

		public static IComparable GetObject (IntPtr handle, JniHandleOwnership transfer)
		{
			return global::Java.Lang.Object.GetObject<IComparable> (handle, transfer);
		}

		static IntPtr Validate (IntPtr handle)
		{
			if (!JNIEnv.IsInstanceOf (handle, java_class_ref))
				throw new InvalidCastException (string.Format ("Unable to convert instance of type '{0}' to type '{1}'.",
							JNIEnv.GetClassNameFromInstance (handle), "java.lang.Comparable"));
			return handle;
		}

		protected override void Dispose (bool disposing)
		{
			if (this.class_ref != IntPtr.Zero)
				JNIEnv.DeleteGlobalRef (this.class_ref);
			this.class_ref = IntPtr.Zero;
			base.Dispose (disposing);
		}

		public IComparableInvoker (IntPtr handle, JniHandleOwnership transfer) : base (Validate (handle), transfer)
		{
			IntPtr local_ref = JNIEnv.GetObjectClass (((global::Java.Lang.Object) this).Handle);
			this.class_ref = JNIEnv.NewGlobalRef (local_ref);
			JNIEnv.DeleteLocalRef (local_ref);
		}

		static Delegate cb_compareTo_Ljava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetCompareTo_Ljava_lang_Object_Handler ()
		{
			if (cb_compareTo_Ljava_lang_Object_ == null)
				cb_compareTo_Ljava_lang_Object_ = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr, int>) n_CompareTo_Ljava_lang_Object_);
			return cb_compareTo_Ljava_lang_Object_;
		}

		static int n_CompareTo_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_another)
		{
			global::Java.Lang.IComparable __this = global::Java.Lang.Object.GetObject<global::Java.Lang.IComparable> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			global::Java.Lang.Object another = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_another, JniHandleOwnership.DoNotTransfer);
			int __ret = __this.CompareTo (another);
			return __ret;
		}
#pragma warning restore 0169

		IntPtr id_compareTo_Ljava_lang_Object_;
		public unsafe int CompareTo (global::Java.Lang.Object another)
		{
			if (id_compareTo_Ljava_lang_Object_ == IntPtr.Zero)
				id_compareTo_Ljava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "compareTo", "(Ljava/lang/Object;)I");
			IntPtr native_another = JNIEnv.ToLocalJniHandle (another);
			JValue* __args = stackalloc JValue [1];
			__args [0] = new JValue (native_another);
			int __ret = JNIEnv.CallIntMethod (((global::Java.Lang.Object) this).Handle, id_compareTo_Ljava_lang_Object_, __args);
			JNIEnv.DeleteLocalRef (native_another);
			return __ret;
		}

	}

}
