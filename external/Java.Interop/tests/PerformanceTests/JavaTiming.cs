using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Java.Interop;
using Java.Interop.GenericMarshaler;

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
		static unsafe JniObjectReference _NewObject ()
		{
			TypeRef.GetCachedConstructor (ref Object_ctor, "()V");
			return TypeRef.NewObject (Object_ctor, null);
		}

		public unsafe JavaTiming ()
			: base (ref *InvalidJniObjectReference, JniHandleOwnership.Invalid)
		{
			var peer    = _NewObject ();
			using (SetPeerReference (ref peer, JniHandleOwnership.Transfer)) {
			}
		}

		static JniStaticMethodID svm;
		public static void StaticVoidMethod ()
		{
			TypeRef.GetCachedStaticMethod (ref svm, "StaticVoidMethod", "()V");
			svm.CallVoidMethod (TypeRef.PeerReference);
		}

		static JniStaticMethodID sim;
		public static int StaticIntMethod ()
		{
			TypeRef.GetCachedStaticMethod (ref sim, "StaticIntMethod", "()I");
			return sim.CallInt32Method (TypeRef.PeerReference);
		}

		static JniStaticMethodID som;
		public static IJavaPeerable StaticObjectMethod ()
		{
			TypeRef.GetCachedStaticMethod (ref som, "StaticObjectMethod", "()Ljava/lang/Object;");
			var lref = som.CallObjectMethod (TypeRef.PeerReference);
			return JniEnvironment.Current.JavaVM.GetObject (ref lref, JniHandleOwnership.Transfer);
		}

		static JniInstanceMethodID vvm;
		public virtual void VirtualVoidMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref vvm, "VirtualVoidMethod", "()V");
			vvm.CallVirtualVoidMethod (PeerReference);
		}

		static JniInstanceMethodID vim;
		public virtual int VirtualIntMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref vim, "VirtualIntMethod", "()I");
			return vim.CallVirtualInt32Method (PeerReference);
		}

		static JniInstanceMethodID vom;
		public virtual IJavaPeerable VirtualObjectMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref vom, "VirtualObjectMethod", "()Ljava/lang/Object;");
			var lref = vom.CallVirtualObjectMethod (PeerReference);
			return JniEnvironment.Current.JavaVM.GetObject (ref lref, JniHandleOwnership.Transfer);
		}

		static JniInstanceMethodID fvm;
		public void FinalVoidMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref fvm, "FinalVoidMethod", "()V");
			fvm.CallNonvirtualVoidMethod (PeerReference, TypeRef.PeerReference);
		}

		static JniInstanceMethodID fim;
		public int FinalIntMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref fim, "FinalIntMethod", "()I");
			return fim.CallNonvirtualInt32Method (PeerReference, TypeRef.PeerReference);
		}

		static JniInstanceMethodID fom;
		public IJavaPeerable FinalObjectMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref fom, "FinalObjectMethod", "()Ljava/lang/Object;");
			var lref = vom.CallNonvirtualObjectMethod (PeerReference, TypeRef.PeerReference);
			return JniEnvironment.Current.JavaVM.GetObject (ref lref, JniHandleOwnership.Transfer);
		}

		static JniInstanceMethodID vim1;
		public unsafe int VirtualIntMethod1Args (int value)
		{
			TypeRef.GetCachedInstanceMethod (ref vim1, "VirtualIntMethod1Args", "(I)I");

			var args = stackalloc JValue [1];
			args [0] = new JValue (value);
			int r;

			if (GetType () == _members.ManagedPeerType)
				r = vim1.CallVirtualInt32Method (PeerReference, args);
			else {
				JniInstanceMethodID m = JniPeerMembers.InstanceMethods.GetMethodID ("VirtualIntMethod1Args\u0000(I)I");
				r = m.CallNonvirtualInt32Method (PeerReference, JniPeerMembers.JniPeerType.PeerReference, args);
			}
			return r;
		}

		public virtual unsafe int Timing_VirtualIntMethod_Marshal1Args (int value)
		{
			var args = stackalloc JValue [1];
			args [0] = new JValue (value);

			return _members.InstanceMethods.CallInt32Method ("VirtualIntMethod1Args\u0000(I)I", this, args);
		}

		public virtual int Timing_VirtualIntMethod_GenericMarshal1Args (int value)
		{
			return _members.InstanceMethods.CallGenericInt32Method ("VirtualIntMethod1Args\u0000(I)I", this, value);
		}

		static JniInstanceMethodID vim1_a;
		public unsafe int VirtualIntMethod1Args (int[][][] value)
		{
			TypeRef.GetCachedInstanceMethod (ref vim1_a, "VirtualIntMethod1Args", "([[[I)I");

			int r;

			using (var native_array = new JavaObjectArray<int[][]> (value)) {
				var args = stackalloc JValue [1];
				args [0] = new JValue (native_array);
				if (GetType () == _members.ManagedPeerType)
					r = vim1_a.CallVirtualInt32Method (PeerReference, args);
				else {
					JniInstanceMethodID m = JniPeerMembers.InstanceMethods.GetMethodID ("VirtualIntMethod1Args\u0000([[[I)I");
					r = m.CallNonvirtualInt32Method (PeerReference, JniPeerMembers.JniPeerType.PeerReference, args);
				}
				native_array.CopyTo (value, 0);
			}
			return r;
		}

		public unsafe virtual int Timing_VirtualIntMethod_Marshal1Args (int[][][] value)
		{
			using (var native_array = new JavaObjectArray<int[][]> (value)) {
				var args = stackalloc JValue [1];
				args [0] = new JValue (native_array);
				try {
					return _members.InstanceMethods.CallInt32Method ("VirtualIntMethod1Args\u0000([[[I)I", this, args);
				} finally {
					native_array.CopyTo (value, 0);
				}
			}
		}

		public virtual int Timing_VirtualIntMethod_GenericMarshal1Args (int[][][] value)
		{
			return _members.InstanceMethods.CallGenericInt32Method ("VirtualIntMethod1Args\u0000([[[I)I", this, value);
		}

		static JniStaticMethodID svm1;
		public static unsafe void StaticVoidMethod1Args (IJavaPeerable obj1)
		{
			TypeRef.GetCachedStaticMethod (ref svm1, "StaticVoidMethod1Args",
					"(Ljava/lang/Object;)V");
			var args = stackalloc JValue [1];
			args [0] = new JValue (obj1);
			svm1.CallVoidMethod (TypeRef.PeerReference, args);
		}

		static JniStaticMethodID svm2;
		public static unsafe void StaticVoidMethod2Args (IJavaPeerable obj1, IJavaPeerable obj2)
		{
			TypeRef.GetCachedStaticMethod (ref svm2, "StaticVoidMethod2Args",
					"(Ljava/lang/Object;Ljava/lang/Object;)V");
			var args = stackalloc JValue [2];
			args [0] = new JValue (obj1);
			args [1] = new JValue (obj2);
			svm2.CallVoidMethod (TypeRef.PeerReference, args);
		}

		static JniStaticMethodID svm3;
		public static unsafe void StaticVoidMethod3Args (IJavaPeerable obj1, IJavaPeerable obj2, IJavaPeerable obj3)
		{
			TypeRef.GetCachedStaticMethod (ref svm3, "StaticVoidMethod3Args",
				"(Ljava/lang/Object;Ljava/lang/Object;Ljava/lang/Object;)V");
			var args = stackalloc JValue [3];
			args [0] = new JValue (obj1);
			args [1] = new JValue (obj2);
			args [1] = new JValue (obj3);
			svm2.CallVoidMethod (TypeRef.PeerReference, args);
		}

		static JniStaticMethodID svmi1;
		public static unsafe void StaticVoidMethod1IArgs (int obj1)
		{
			TypeRef.GetCachedStaticMethod (ref svmi1, "StaticVoidMethod1IArgs", "(I)V");
			var args = stackalloc JValue [1];
			args [0] = new JValue (obj1);
			svmi1.CallVoidMethod (TypeRef.PeerReference, args);
		}

		static JniStaticMethodID svmi2;
		public static unsafe void StaticVoidMethod2IArgs (int obj1, int obj2)
		{
			TypeRef.GetCachedStaticMethod (ref svmi2, "StaticVoidMethod2IArgs", "(II)V");
			var args = stackalloc JValue [2];
			args [0] = new JValue (obj1);
			args [1] = new JValue (obj2);
			svmi1.CallVoidMethod (TypeRef.PeerReference, args);
		}

		static JniStaticMethodID svmi3;
		public static unsafe void StaticVoidMethod3IArgs (int obj1, int obj2, int obj3)
		{
			TypeRef.GetCachedStaticMethod (ref svmi3, "StaticVoidMethod3IArgs", "(III)V");
			var args = stackalloc JValue [3];
			args [0] = new JValue (obj1);
			args [1] = new JValue (obj2);
			args [1] = new JValue (obj3);
			svmi1.CallVoidMethod (TypeRef.PeerReference, args);
		}

		const string toString_name  = "toString";
		const string toString_sig   = "()Ljava/lang/String;";

		static JniInstanceMethodID toString;
		public JniObjectReference Timing_ToString_Traditional ()
		{
			TypeRef.GetCachedInstanceMethod (ref toString, toString_name, toString_sig);
			return toString.CallVirtualObjectMethod (PeerReference);
		}

		public JniObjectReference Timing_ToString_NoCache ()
		{
			var m = TypeRef.GetInstanceMethod (toString_name, toString_sig);
			return m.CallVirtualObjectMethod (PeerReference);
		}

		static Dictionary<string, JniInstanceMethodID> dictInstanceMethods = new Dictionary<string, JniInstanceMethodID>();
		public JniObjectReference Timing_ToString_DictWithLock ()
		{
			JniInstanceMethodID m;
			lock (dictInstanceMethods) {
				if (!dictInstanceMethods.TryGetValue (toString_name + toString_sig, out m))
					dictInstanceMethods.Add (toString_name + toString_sig, m = TypeRef.GetInstanceMethod (toString_name, toString_sig));
			}
			return m.CallVirtualObjectMethod (PeerReference);
		}

		static ConcurrentDictionary<string, JniInstanceMethodID> nolockInstanceMethods = new ConcurrentDictionary<string, JniInstanceMethodID>();
		public JniObjectReference Timing_ToString_DictWithNoLock ()
		{
			var m = nolockInstanceMethods.AddOrUpdate (toString_name + toString_sig,
				s => TypeRef.GetInstanceMethod (toString_name, toString_sig),
				(s, c) => c ?? TypeRef.GetInstanceMethod (toString_name, toString_sig));
			return m.CallVirtualObjectMethod (PeerReference);
		}
	}
}

