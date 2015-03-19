using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Java.Interop;

namespace Java.Interop.PerformanceTests
{
	[JniTypeInfoAttribute (JniTypeName)]
	public class JavaTiming : JavaObject
	{
		const               string          JniTypeName = "com/xamarin/interop/performance/JavaTiming";
		static  readonly    JniPeerMembers  _members    = new JniPeerMembers (JniTypeName, typeof (JavaTiming));

		static JniType _TypeRef;
		internal static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		static JniInstanceMethodID Object_ctor;
		static JniLocalReference _NewObject ()
		{
			TypeRef.GetCachedConstructor (ref Object_ctor, "()V");
			return TypeRef.NewObject (Object_ctor);
		}

		public JavaTiming ()
			: base (_NewObject (), JniHandleOwnership.Transfer)
		{
		}

		static JniStaticMethodID svm;
		public static void StaticVoidMethod ()
		{
			TypeRef.GetCachedStaticMethod (ref svm, "StaticVoidMethod", "()V");
			svm.CallVoidMethod (TypeRef.SafeHandle);
		}

		static JniStaticMethodID sim;
		public static int StaticIntMethod ()
		{
			TypeRef.GetCachedStaticMethod (ref sim, "StaticIntMethod", "()I");
			return sim.CallInt32Method (TypeRef.SafeHandle);
		}

		static JniStaticMethodID som;
		public static IJavaObject StaticObjectMethod ()
		{
			TypeRef.GetCachedStaticMethod (ref som, "StaticObjectMethod", "()Ljava/lang/Object;");
			var lref = som.CallObjectMethod (TypeRef.SafeHandle);
			return JniEnvironment.Current.JavaVM.GetObject (lref, JniHandleOwnership.Transfer);
		}

		static JniInstanceMethodID vvm;
		public virtual void VirtualVoidMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref vvm, "VirtualVoidMethod", "()V");
			vvm.CallVirtualVoidMethod (SafeHandle);
		}

		static JniInstanceMethodID vim;
		public virtual int VirtualIntMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref vim, "VirtualIntMethod", "()I");
			return vim.CallVirtualInt32Method (SafeHandle);
		}

		static JniInstanceMethodID vom;
		public virtual IJavaObject VirtualObjectMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref vom, "VirtualObjectMethod", "()Ljava/lang/Object;");
			var lref = vom.CallVirtualObjectMethod (SafeHandle);
			return JniEnvironment.Current.JavaVM.GetObject (lref, JniHandleOwnership.Transfer);
		}

		static JniInstanceMethodID fvm;
		public void FinalVoidMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref fvm, "FinalVoidMethod", "()V");
			fvm.CallNonvirtualVoidMethod (SafeHandle, TypeRef.SafeHandle);
		}

		static JniInstanceMethodID fim;
		public int FinalIntMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref fim, "FinalIntMethod", "()I");
			return fim.CallNonvirtualInt32Method (SafeHandle, TypeRef.SafeHandle);
		}

		static JniInstanceMethodID fom;
		public IJavaObject FinalObjectMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref fom, "FinalObjectMethod", "()Ljava/lang/Object;");
			var lref = vom.CallNonvirtualObjectMethod (SafeHandle, TypeRef.SafeHandle);
			return JniEnvironment.Current.JavaVM.GetObject (lref, JniHandleOwnership.Transfer);
		}

		static JniInstanceMethodID vim1;
		public int VirtualIntMethod1Args (int value)
		{
			TypeRef.GetCachedInstanceMethod (ref vim1, "VirtualIntMethod1Args", "(I)I");
			int r;
			if (GetType () == _members.ManagedPeerType)
				r = vim1.CallVirtualInt32Method (SafeHandle, new JValue (value));
			else {
				JniInstanceMethodID m = JniPeerMembers.InstanceMethods.GetMethodID ("VirtualIntMethod1Args\u0000(I)I");
				r = m.CallNonvirtualInt32Method (SafeHandle, JniPeerMembers.JniPeerType.SafeHandle, new JValue (value));
			}
			return r;
		}

		public virtual int Timing_VirtualIntMethod_Marshal1Args (int value)
		{
			return _members.InstanceMethods.CallInt32Method ("VirtualIntMethod1Args\u0000(I)I", this, new JValue[]{new JValue (value)});
		}

		public virtual int Timing_VirtualIntMethod_GenericMarshal1Args (int value)
		{
			return _members.InstanceMethods.CallInt32Method ("VirtualIntMethod1Args\u0000(I)I", this, value);
		}

		static JniInstanceMethodID vim1_a;
		public int VirtualIntMethod1Args (int[][][] value)
		{
			TypeRef.GetCachedInstanceMethod (ref vim1_a, "VirtualIntMethod1Args", "([[[I)I");
			int r;
			using (var native_array = new JavaObjectArray<int[][]> (value)) {
				if (GetType () == _members.ManagedPeerType)
					r = vim1_a.CallVirtualInt32Method (SafeHandle, new JValue (native_array));
				else {
					JniInstanceMethodID m = JniPeerMembers.InstanceMethods.GetMethodID ("VirtualIntMethod1Args\u0000([[[I)I");
					r = m.CallNonvirtualInt32Method (SafeHandle, JniPeerMembers.JniPeerType.SafeHandle, new JValue (native_array));
				}
				native_array.CopyTo (value, 0);
			}
			return r;
		}

		public virtual int Timing_VirtualIntMethod_Marshal1Args (int[][][] value)
		{
			using (var native_array = new JavaObjectArray<int[][]> (value)) {
				try {
					return _members.InstanceMethods.CallInt32Method ("VirtualIntMethod1Args\u0000([[[I)I", this, new JValue[]{ new JValue (native_array) });
				} finally {
					native_array.CopyTo (value, 0);
				}
			}
		}

		public virtual int Timing_VirtualIntMethod_GenericMarshal1Args (int[][][] value)
		{
			return _members.InstanceMethods.CallInt32Method ("VirtualIntMethod1Args\u0000([[[I)I", this, value);
		}

		static JniStaticMethodID svm1;
		public static void StaticVoidMethod1Args (IJavaObject obj1)
		{
			TypeRef.GetCachedStaticMethod (ref svm1, "StaticVoidMethod1Args",
					"(Ljava/lang/Object;)V");
			svm1.CallVoidMethod (TypeRef.SafeHandle, new JValue (obj1));
		}

		static JniStaticMethodID svm2;
		public static void StaticVoidMethod2Args (IJavaObject obj1, IJavaObject obj2)
		{
			TypeRef.GetCachedStaticMethod (ref svm2, "StaticVoidMethod2Args",
					"(Ljava/lang/Object;Ljava/lang/Object;)V");
			svm2.CallVoidMethod (TypeRef.SafeHandle, new JValue (obj1), new JValue (obj2));
		}

		static JniStaticMethodID svm3;
		public static void StaticVoidMethod3Args (IJavaObject obj1, IJavaObject obj2, IJavaObject obj3)
		{
			TypeRef.GetCachedStaticMethod (ref svm3, "StaticVoidMethod3Args",
				"(Ljava/lang/Object;Ljava/lang/Object;Ljava/lang/Object;)V");
			svm2.CallVoidMethod (TypeRef.SafeHandle, new JValue (obj1), new JValue (obj2), new JValue (obj3));
		}

		static JniStaticMethodID svmi1;
		public static void StaticVoidMethod1IArgs (int obj1)
		{
			TypeRef.GetCachedStaticMethod (ref svmi1, "StaticVoidMethod1IArgs", "(I)V");
			svmi1.CallVoidMethod (TypeRef.SafeHandle, new JValue (obj1));
		}

		static JniStaticMethodID svmi2;
		public static void StaticVoidMethod2IArgs (int obj1, int obj2)
		{
			TypeRef.GetCachedStaticMethod (ref svmi2, "StaticVoidMethod2IArgs", "(II)V");
			svmi1.CallVoidMethod (TypeRef.SafeHandle, new JValue (obj1), new JValue (obj2));
		}

		static JniStaticMethodID svmi3;
		public static void StaticVoidMethod3IArgs (int obj1, int obj2, int obj3)
		{
			TypeRef.GetCachedStaticMethod (ref svmi3, "StaticVoidMethod3IArgs", "(III)V");
			svmi1.CallVoidMethod (TypeRef.SafeHandle, new JValue (obj1), new JValue (obj2), new JValue (obj3));
		}

		const string toString_name  = "toString";
		const string toString_sig   = "()Ljava/lang/String;";

		static JniInstanceMethodID toString;
		public JniLocalReference Timing_ToString_Traditional ()
		{
			TypeRef.GetCachedInstanceMethod (ref toString, toString_name, toString_sig);
			return toString.CallVirtualObjectMethod (SafeHandle);
		}

		public JniLocalReference Timing_ToString_NoCache ()
		{
			using (var m = TypeRef.GetInstanceMethod (toString_name, toString_sig))
				return m.CallVirtualObjectMethod (SafeHandle);
		}

		static Dictionary<string, JniInstanceMethodID> dictInstanceMethods = new Dictionary<string, JniInstanceMethodID>();
		public JniLocalReference Timing_ToString_DictWithLock ()
		{
			JniInstanceMethodID m;
			lock (dictInstanceMethods) {
				if (!dictInstanceMethods.TryGetValue (toString_name + toString_sig, out m) || m.IsInvalid)
					dictInstanceMethods.Add (toString_name + toString_sig, m = TypeRef.GetInstanceMethod (toString_name, toString_sig));
			}
			return m.CallVirtualObjectMethod (SafeHandle);
		}

		static ConcurrentDictionary<string, JniInstanceMethodID> nolockInstanceMethods = new ConcurrentDictionary<string, JniInstanceMethodID>();
		public JniLocalReference Timing_ToString_DictWithNoLock ()
		{
			var m = nolockInstanceMethods.AddOrUpdate (toString_name + toString_sig,
				s => TypeRef.GetInstanceMethod (toString_name, toString_sig),
				(s, c) => c.IsInvalid ? TypeRef.GetInstanceMethod (toString_name, toString_sig) : c);
			return m.CallVirtualObjectMethod (SafeHandle);
		}
	}
}

