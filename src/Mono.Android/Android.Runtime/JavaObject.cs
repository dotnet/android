using System;

namespace Android.Runtime {

	[Register ("mono/android/runtime/JavaObject")]
	internal sealed class JavaObject : Java.Lang.Object {

		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Interoperability", "CA1422:Validate platform compatibility", Justification = "Suggested replacement uses instance sharing")]
		public static IntPtr GetHandle (object obj)
		{
			if (obj == null)
				return IntPtr.Zero;

			Type type = obj.GetType ();
			if (type == typeof (bool))
				return new Java.Lang.Boolean ((bool)obj).Handle;
			else if (type == typeof (sbyte))
				return new Java.Lang.Byte ((sbyte)obj).Handle;
			else if (type == typeof (char))
				return new Java.Lang.Character ((char)obj).Handle;
			else if (type == typeof (short))
				return new Java.Lang.Short ((short)obj).Handle;
			else if (type == typeof (int))
				return new Java.Lang.Integer ((int)obj).Handle;
			else if (type == typeof (long))
				return new Java.Lang.Long ((long)obj).Handle;
			else if (type == typeof (float))
				return new Java.Lang.Float ((float)obj).Handle;
			else if (type == typeof (double))
				return new Java.Lang.Double ((double)obj).Handle;
			else if (type == typeof (string))
				return JNIEnv.NewString ((string)obj);
			else if (typeof (IJavaObject).IsAssignableFrom (type))
				return ((IJavaObject)obj).Handle;
			else
				return new JavaObject (obj).Handle;
		}

		public static object? GetObject (IntPtr handle, JniHandleOwnership transfer)
		{
			Java.Lang.Object? jlo = Java.Lang.Object.GetObject (handle, transfer) as Java.Lang.Object;
			if (jlo == null)
				return null;
			else if (jlo is Java.Lang.Boolean)
				return Dispose ((Java.Lang.Boolean) jlo, v => v.BooleanValue ());
			else if (jlo is Java.Lang.Byte)
				return Dispose ((Java.Lang.Byte) jlo, v => v.ByteValue ());
			else if (jlo is Java.Lang.Character)
				return Dispose ((Java.Lang.Character) jlo, v => v.CharValue ());
			else if (jlo is Java.Lang.Short)
				return Dispose ((Java.Lang.Short) jlo, v => v.ShortValue ());
			else if (jlo is Java.Lang.Integer)
				return Dispose ((Java.Lang.Integer) jlo, v => v.IntValue ());
			else if (jlo is Java.Lang.Long)
				return Dispose ((Java.Lang.Long) jlo, v => v.LongValue ());
			else if (jlo is Java.Lang.Float)
				return Dispose ((Java.Lang.Float) jlo, v => v.FloatValue ());
			else if (jlo is Java.Lang.Double)
				return Dispose ((Java.Lang.Double) jlo, v => v.DoubleValue ());
			else if (jlo is Java.Lang.String)
				return Dispose (jlo, v => JNIEnv.GetString (v.Handle, JniHandleOwnership.DoNotTransfer));
			else if (jlo is JavaObject)
				return ((JavaObject) jlo).inst;
			else
				return jlo;
		}

		static TRet Dispose<T, TRet>(T value, Func<T, TRet> c)
			where T : IDisposable
		{
			try {
				return c (value);
			} finally {
				value.Dispose ();
			}
		}

		object inst;

		public JavaObject (object inst)
			: base (
					JNIEnv.StartCreateInstance ("mono/android/runtime/JavaObject", "()V"),
					JniHandleOwnership.TransferLocalRef)
		{
			JNIEnv.FinishCreateInstance (Handle, "()V");

			this.inst = inst;
		}

		public object Instance {
			get { return inst; }
		}

		public override bool Equals (Java.Lang.Object? obj)
		{
			if (obj is JavaObject jobj) {
				return jobj.inst == inst;
			}
			return false;
		}

		public override int GetHashCode ()
		{
			return inst.GetHashCode ();
		}

		public override string? ToString ()
		{
			if (inst == null)
				return "";
			return inst.ToString ();
		}
	}
}

