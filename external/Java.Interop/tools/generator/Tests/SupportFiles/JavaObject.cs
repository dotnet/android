using System;

namespace Android.Runtime {

	[Register ("mono/android/runtime/JavaObject")]
	internal sealed class JavaObject : Java.Lang.Object {

		public static IntPtr GetHandle (object obj)
		{
			throw new NotImplementedException ();
		}

		public static object GetObject (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		static TRet Dispose<T, TRet>(T value, Func<T, TRet> c)
			where T : IDisposable
		{
			throw new NotImplementedException ();
		}

		public JavaObject (object inst)
			//: base (
			//		JNIEnv.StartCreateInstance ("mono/android/runtime/JavaObject", "()V"),
			//		JniHandleOwnership.TransferLocalRef)
		{
			throw new NotImplementedException ();
		}

		public object Instance {
			get;
			set;
		}
		/*
		protected override Java.Lang.Object Clone ()
		{
			throw new NotImplementedException ();
		}

		public override bool Equals (Java.Lang.Object obj)
		{
			throw new NotImplementedException ();
		}

		public override int GetHashCode ()
		{
			throw new NotImplementedException ();
		}

		public override string ToString ()
		{
			throw new NotImplementedException ();
		}*/
	}

	public static class JNINativeWrapper {

		static void get_runtime_types ()
		{
			throw new NotImplementedException ();
		}

		public static Delegate CreateDelegate (Delegate dlg)
		{
			throw new NotImplementedException ();
		}

	}
}
namespace Java.Lang {

	using Android.Runtime;

	public partial class Object : IJavaObject, IDisposable {

		public IntPtr Handle { get; set; }

		protected virtual IntPtr ThresholdClass {
			get;
			set;
		}

		protected virtual global::System.Type ThresholdType {
			get;
			set;
		}

		public Object()
		{
			throw new NotImplementedException ();
		}

		public Object (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		public static T GetObject<T>(IntPtr jnienv, IntPtr native__this, JniHandleOwnership ownership)
		{
			throw new NotImplementedException ();
		}

		public static T GetObject<T>(IntPtr native__this, JniHandleOwnership ownership)
		{
			throw new NotImplementedException ();
		}

		protected void SetHandle(IntPtr instance, JniHandleOwnership ownership)
		{
			throw new NotImplementedException ();
		}

		public void Dispose()
		{
			throw new NotImplementedException ();
		}

		protected virtual void Dispose (bool disposing)
		{
			throw new NotImplementedException ();
		}
	}

	public partial class Throwable : global::System.Exception, IJavaObject, IDisposable {

		public IntPtr Handle { get; set; }

		protected virtual IntPtr ThresholdClass {
			get;
			set;
		}

		protected virtual global::System.Type ThresholdType {
			get;
			set;
		}

		public Throwable ()
		{
			throw new NotImplementedException ();
		}

		public Throwable (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		protected void SetHandle(IntPtr instance, JniHandleOwnership ownership)
		{
			throw new NotImplementedException ();
		}

		public void Dispose()
		{
			throw new NotImplementedException ();
		}

		protected virtual void Dispose (bool disposing)
		{
			throw new NotImplementedException ();
		}
	}
}
namespace Java.Lang {

	using System.Collections.Generic;

	public partial interface ICharSequence : IEnumerable<char> {}

}
namespace Android.Graphics
{
	public struct Color 
	{
		public Color (int argb)
		{
			throw new NotImplementedException ();
		}

		private Color (uint argb)
		{
			throw new NotImplementedException ();
		}

		public int ToArgb ()
		{
			throw new NotImplementedException ();
		}
	}
}
