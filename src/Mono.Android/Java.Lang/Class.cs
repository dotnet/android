using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Android.Runtime;

namespace Java.Lang {

	public partial class Class {

		public static readonly IntPtr Object;
		public static readonly IntPtr String;
		public static readonly IntPtr CharSequence;

		internal static readonly IntPtr CharSequence_toString;

		static Class ()
		{
			Object = JNIEnv.FindClass ("java/lang/Object");
			String = JNIEnv.FindClass ("java/lang/String");
			CharSequence = JNIEnv.FindClass ("java/lang/CharSequence");
			CharSequence_toString = JNIEnv.GetMethodID (CharSequence, "toString", "()Ljava/lang/String;");
		}

		public static Class FromType (System.Type type)
		{
			if (!(typeof (IJavaObject).IsAssignableFrom (type)))
				throw new ArgumentException ("type", "Type is not derived from a java type.");

			return Java.Lang.Object.GetObject<Class> (JNIEnv.FindClass (type), JniHandleOwnership.TransferGlobalRef);
		}
	}
}
