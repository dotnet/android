using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath interface reference: path="/api/package[@name='xamarin.test']/interface[@name='SpinnerAdapter']"
	[Register ("xamarin/test/SpinnerAdapter", "", "Xamarin.Test.ISpinnerAdapterInvoker")]
	public partial interface ISpinnerAdapter : global::Xamarin.Test.IAdapter {

	}

	[global::Android.Runtime.Register ("xamarin/test/SpinnerAdapter", DoNotGenerateAcw=true)]
	internal class ISpinnerAdapterInvoker : global::Java.Lang.Object, ISpinnerAdapter {

		static IntPtr java_class_ref = JNIEnv.FindClass ("xamarin/test/SpinnerAdapter");

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (ISpinnerAdapterInvoker); }
		}

		IntPtr class_ref;

		public static ISpinnerAdapter GetObject (IntPtr handle, JniHandleOwnership transfer)
		{
			return global::Java.Lang.Object.GetObject<ISpinnerAdapter> (handle, transfer);
		}

		static IntPtr Validate (IntPtr handle)
		{
			if (!JNIEnv.IsInstanceOf (handle, java_class_ref))
				throw new InvalidCastException (string.Format ("Unable to convert instance of type '{0}' to type '{1}'.",
							JNIEnv.GetClassNameFromInstance (handle), "xamarin.test.SpinnerAdapter"));
			return handle;
		}

		protected override void Dispose (bool disposing)
		{
			if (this.class_ref != IntPtr.Zero)
				JNIEnv.DeleteGlobalRef (this.class_ref);
			this.class_ref = IntPtr.Zero;
			base.Dispose (disposing);
		}

		public ISpinnerAdapterInvoker (IntPtr handle, JniHandleOwnership transfer) : base (Validate (handle), transfer)
		{
			IntPtr local_ref = JNIEnv.GetObjectClass (((global::Java.Lang.Object) this).Handle);
			this.class_ref = JNIEnv.NewGlobalRef (local_ref);
			JNIEnv.DeleteLocalRef (local_ref);
		}

	}

}
