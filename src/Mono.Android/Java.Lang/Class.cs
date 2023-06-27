using System;
using Android.Runtime;
using Java.Interop;
using Java.Lang.Invoke;

namespace Java.Lang {

	public partial class Class {

		public static readonly IntPtr Object;
		public static readonly IntPtr String;
		public static readonly IntPtr CharSequence;

		internal static readonly IntPtr CharSequence_toString;
		internal static JniPeerMembers Members => _members;

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

			return Java.Lang.Object.GetObject<Class> (JNIEnv.FindClass (type), JniHandleOwnership.TransferGlobalRef)!;
		}

#if ANDROID_34 && NET
		// A new interface (Java.Lang.Invoke.ITypeDescriptor.IOfField) was added to this class in API-34.
		// The new required ComponentType () method conflicts with our ComponentType property created from
		// the existing getComponentType method. Explicitly implement this method, which Android has documented
		// as equivalent to the existing getComponentType method.
		Java.Lang.Object? ITypeDescriptor.IOfField.ComponentType ()
			=> ComponentType;
#endif
	}
}
