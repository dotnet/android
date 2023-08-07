using System;

namespace Android.Runtime {

	[Register ("mono/android/runtime/JavaObject")]
	internal sealed class JavaObject : Java.Lang.Object {

		public static IntPtr GetHandle (object obj)
		{
			if (obj == null)
				return IntPtr.Zero;

			if (obj is bool bool_obj)
				return Java.Lang.Boolean.ValueOf (bool_obj).Handle;
			else if (obj is sbyte sbyte_obj)
				return Java.Lang.Byte.ValueOf (sbyte_obj).Handle;
			else if (obj is char char_obj)
				return Java.Lang.Character.ValueOf (char_obj).Handle;
			else if (obj is short short_obj)
				return Java.Lang.Short.ValueOf (short_obj)!.Handle;
			else if (obj is int int_obj)
				return Java.Lang.Integer.ValueOf (int_obj).Handle;
			else if (obj is long long_obj)
				return Java.Lang.Long.ValueOf (long_obj).Handle;
			else if (obj is float float_obj)
				return Java.Lang.Float.ValueOf (float_obj).Handle;
			else if (obj is double double_obj)
				return Java.Lang.Double.ValueOf (double_obj).Handle;
			else if (obj is string string_obj)
				return JNIEnv.NewString (string_obj);
			else if (typeof (IJavaObject).IsAssignableFrom (obj.GetType ()))
				return ((IJavaObject)obj).Handle;
			else
				return new JavaObject (obj).Handle;
		}

		public static object? GetObject (IntPtr handle, JniHandleOwnership transfer)
		{
			if (Java.Lang.Object.GetObject (handle, transfer) is not Java.Lang.Object jlo)
				return null;
			else if (jlo is Java.Lang.Boolean bool_jlo)
				return Dispose (bool_jlo, v => v.BooleanValue ());
			else if (jlo is Java.Lang.Byte byte_jlo)
				return Dispose (byte_jlo, v => v.ByteValue ());
			else if (jlo is Java.Lang.Character chat_jlo)
				return Dispose (chat_jlo, v => v.CharValue ());
			else if (jlo is Java.Lang.Short short_jlo)
				return Dispose (short_jlo, v => v.ShortValue ());
			else if (jlo is Java.Lang.Integer int_jlo)
				return Dispose (int_jlo, v => v.IntValue ());
			else if (jlo is Java.Lang.Long long_jlo)
				return Dispose (long_jlo, v => v.LongValue ());
			else if (jlo is Java.Lang.Float float_jlo)
				return Dispose (float_jlo, v => v.FloatValue ());
			else if (jlo is Java.Lang.Double double_jlo)
				return Dispose (double_jlo, v => v.DoubleValue ());
			else if (jlo is Java.Lang.String)
				return Dispose (jlo, v => JNIEnv.GetString (v.Handle, JniHandleOwnership.DoNotTransfer));
			else if (jlo is JavaObject jobj_jlo)
				return (jobj_jlo).inst;
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

