using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Java.Interop;
using Java.Interop.GenericMarshaler;

namespace Java.Interop.PerformanceTests
{
	[JniTypeSignature (JniTypeName)]
	public class JavaTiming : JavaObject
	{
		const               string          JniTypeName = "com/xamarin/interop/performance/JavaTiming";
		static  readonly    JniPeerMembers  _members    = new JniPeerMembers (JniTypeName, typeof (JavaTiming));

		static JniType _TypeRef;
		internal static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		static JniInstanceMethodInfo Object_ctor;
		static unsafe JniObjectReference _NewObject ()
		{
			TypeRef.GetCachedConstructor (ref Object_ctor, "()V");
			return TypeRef.NewObject (Object_ctor, null);
		}

		public unsafe JavaTiming ()
			: base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.Invalid)
		{
			var peer    = _NewObject ();
			using (SetPeerReference (ref peer, JniObjectReferenceOptions.DisposeSourceReference)) {
			}
		}

		static JniStaticMethodInfo svm;
		public static void StaticVoidMethod ()
		{
			TypeRef.GetCachedStaticMethod (ref svm, "StaticVoidMethod", "()V");
			svm.CallVoidMethod (TypeRef.PeerReference);
		}

		static JniStaticMethodInfo sim;
		public static int StaticIntMethod ()
		{
			TypeRef.GetCachedStaticMethod (ref sim, "StaticIntMethod", "()I");
			return sim.CallInt32Method (TypeRef.PeerReference);
		}

		static JniStaticMethodInfo som;
		public static IJavaPeerable StaticObjectMethod ()
		{
			TypeRef.GetCachedStaticMethod (ref som, "StaticObjectMethod", "()Ljava/lang/Object;");
			var lref = som.CallObjectMethod (TypeRef.PeerReference);
			return JniEnvironment.Runtime.GetObject (ref lref, JniObjectReferenceOptions.DisposeSourceReference);
		}

		static JniInstanceMethodInfo vvm;
		public virtual void VirtualVoidMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref vvm, "VirtualVoidMethod", "()V");
			vvm.InvokeVirtualVoidMethod (PeerReference);
		}

		static JniInstanceMethodInfo vim;
		public virtual int VirtualIntMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref vim, "VirtualIntMethod", "()I");
			return vim.InvokeVirtualInt32Method (PeerReference);
		}

		static JniInstanceMethodInfo vom;
		public virtual IJavaPeerable VirtualObjectMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref vom, "VirtualObjectMethod", "()Ljava/lang/Object;");
			var lref = vom.InvokeVirtualObjectMethod (PeerReference);
			return JniEnvironment.Runtime.GetObject (ref lref, JniObjectReferenceOptions.DisposeSourceReference);
		}

		static JniInstanceMethodInfo fvm;
		public void FinalVoidMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref fvm, "FinalVoidMethod", "()V");
			fvm.InvokeNonvirtualVoidMethod (PeerReference, TypeRef.PeerReference);
		}

		static JniInstanceMethodInfo fim;
		public int FinalIntMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref fim, "FinalIntMethod", "()I");
			return fim.InvokeNonvirtualInt32Method (PeerReference, TypeRef.PeerReference);
		}

		static JniInstanceMethodInfo fom;
		public IJavaPeerable FinalObjectMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref fom, "FinalObjectMethod", "()Ljava/lang/Object;");
			var lref = vom.InvokeNonvirtualObjectMethod (PeerReference, TypeRef.PeerReference);
			return JniEnvironment.Runtime.GetObject (ref lref, JniObjectReferenceOptions.DisposeSourceReference);
		}

		static JniInstanceMethodInfo vim1;
		public unsafe int VirtualIntMethod1Args (int value)
		{
			TypeRef.GetCachedInstanceMethod (ref vim1, "VirtualIntMethod1Args", "(I)I");

			var args = stackalloc JniArgumentValue [1];
			args [0] = new JniArgumentValue (value);
			int r;

			if (GetType () == _members.ManagedPeerType)
				r = vim1.InvokeVirtualInt32Method (PeerReference, args);
			else {
				JniInstanceMethodInfo m = JniPeerMembers.InstanceMethods.GetMethodInfo ("VirtualIntMethod1Args\u0000(I)I");
				r = m.InvokeNonvirtualInt32Method (PeerReference, JniPeerMembers.JniPeerType.PeerReference, args);
			}
			return r;
		}

		public virtual unsafe int Timing_VirtualIntMethod_Marshal1Args (int value)
		{
			var args = stackalloc JniArgumentValue [1];
			args [0] = new JniArgumentValue (value);

			return _members.InstanceMethods.InvokeVirtualInt32Method ("VirtualIntMethod1Args\u0000(I)I", this, args);
		}

		public virtual int Timing_VirtualIntMethod_GenericMarshal1Args (int value)
		{
			return _members.InstanceMethods.InvokeGenericVirtualInt32Method ("VirtualIntMethod1Args\u0000(I)I", this, value);
		}

		static JniInstanceMethodInfo vim1_a;
		public unsafe int VirtualIntMethod1Args (int[][][] value)
		{
			TypeRef.GetCachedInstanceMethod (ref vim1_a, "VirtualIntMethod1Args", "([[[I)I");

			int r;

			using (var native_array = new JavaObjectArray<int[][]> (value)) {
				var args = stackalloc JniArgumentValue [1];
				args [0] = new JniArgumentValue (native_array);
				if (GetType () == _members.ManagedPeerType)
					r = vim1_a.InvokeVirtualInt32Method (PeerReference, args);
				else {
					JniInstanceMethodInfo m = JniPeerMembers.InstanceMethods.GetMethodInfo ("VirtualIntMethod1Args\u0000([[[I)I");
					r = m.InvokeNonvirtualInt32Method (PeerReference, JniPeerMembers.JniPeerType.PeerReference, args);
				}
				native_array.CopyTo (value, 0);
			}
			return r;
		}

		public unsafe virtual int Timing_VirtualIntMethod_Marshal1Args (int[][][] value)
		{
			using (var native_array = new JavaObjectArray<int[][]> (value)) {
				var args = stackalloc JniArgumentValue [1];
				args [0] = new JniArgumentValue (native_array);
				try {
					return _members.InstanceMethods.InvokeVirtualInt32Method ("VirtualIntMethod1Args\u0000([[[I)I", this, args);
				} finally {
					native_array.CopyTo (value, 0);
				}
			}
		}

		public virtual int Timing_VirtualIntMethod_GenericMarshal1Args (int[][][] value)
		{
			return _members.InstanceMethods.InvokeGenericVirtualInt32Method ("VirtualIntMethod1Args\u0000([[[I)I", this, value);
		}

		static JniStaticMethodInfo svm1;
		public static unsafe void StaticVoidMethod1Args (IJavaPeerable obj1)
		{
			TypeRef.GetCachedStaticMethod (ref svm1, "StaticVoidMethod1Args",
					"(Ljava/lang/Object;)V");
			var args = stackalloc JniArgumentValue [1];
			args [0] = new JniArgumentValue (obj1);
			svm1.CallVoidMethod (TypeRef.PeerReference, args);
		}

		static JniStaticMethodInfo svm2;
		public static unsafe void StaticVoidMethod2Args (IJavaPeerable obj1, IJavaPeerable obj2)
		{
			TypeRef.GetCachedStaticMethod (ref svm2, "StaticVoidMethod2Args",
					"(Ljava/lang/Object;Ljava/lang/Object;)V");
			var args = stackalloc JniArgumentValue [2];
			args [0] = new JniArgumentValue (obj1);
			args [1] = new JniArgumentValue (obj2);
			svm2.CallVoidMethod (TypeRef.PeerReference, args);
		}

		static JniStaticMethodInfo svm3;
		public static unsafe void StaticVoidMethod3Args (IJavaPeerable obj1, IJavaPeerable obj2, IJavaPeerable obj3)
		{
			TypeRef.GetCachedStaticMethod (ref svm3, "StaticVoidMethod3Args",
				"(Ljava/lang/Object;Ljava/lang/Object;Ljava/lang/Object;)V");
			var args = stackalloc JniArgumentValue [3];
			args [0] = new JniArgumentValue (obj1);
			args [1] = new JniArgumentValue (obj2);
			args [1] = new JniArgumentValue (obj3);
			svm2.CallVoidMethod (TypeRef.PeerReference, args);
		}

		static JniStaticMethodInfo svmi1;
		public static unsafe void StaticVoidMethod1IArgs (int obj1)
		{
			TypeRef.GetCachedStaticMethod (ref svmi1, "StaticVoidMethod1IArgs", "(I)V");
			var args = stackalloc JniArgumentValue [1];
			args [0] = new JniArgumentValue (obj1);
			svmi1.CallVoidMethod (TypeRef.PeerReference, args);
		}

		static JniStaticMethodInfo svmi2;
		public static unsafe void StaticVoidMethod2IArgs (int obj1, int obj2)
		{
			TypeRef.GetCachedStaticMethod (ref svmi2, "StaticVoidMethod2IArgs", "(II)V");
			var args = stackalloc JniArgumentValue [2];
			args [0] = new JniArgumentValue (obj1);
			args [1] = new JniArgumentValue (obj2);
			svmi1.CallVoidMethod (TypeRef.PeerReference, args);
		}

		static JniStaticMethodInfo svmi3;
		public static unsafe void StaticVoidMethod3IArgs (int obj1, int obj2, int obj3)
		{
			TypeRef.GetCachedStaticMethod (ref svmi3, "StaticVoidMethod3IArgs", "(III)V");
			var args = stackalloc JniArgumentValue [3];
			args [0] = new JniArgumentValue (obj1);
			args [1] = new JniArgumentValue (obj2);
			args [1] = new JniArgumentValue (obj3);
			svmi1.CallVoidMethod (TypeRef.PeerReference, args);
		}

		const string toString_name  = "toString";
		const string toString_sig   = "()Ljava/lang/String;";

		static JniInstanceMethodInfo toString;
		public JniObjectReference Timing_ToString_Traditional ()
		{
			TypeRef.GetCachedInstanceMethod (ref toString, toString_name, toString_sig);
			return toString.InvokeVirtualObjectMethod (PeerReference);
		}

		public JniObjectReference Timing_ToString_NoCache ()
		{
			var m = TypeRef.GetInstanceMethod (toString_name, toString_sig);
			return m.InvokeVirtualObjectMethod (PeerReference);
		}

		static Dictionary<string, JniInstanceMethodInfo> dictInstanceMethods = new Dictionary<string, JniInstanceMethodInfo>();
		public JniObjectReference Timing_ToString_DictWithLock ()
		{
			JniInstanceMethodInfo m;
			lock (dictInstanceMethods) {
				if (!dictInstanceMethods.TryGetValue (toString_name + toString_sig, out m))
					dictInstanceMethods.Add (toString_name + toString_sig, m = TypeRef.GetInstanceMethod (toString_name, toString_sig));
			}
			return m.InvokeVirtualObjectMethod (PeerReference);
		}

		static ConcurrentDictionary<string, JniInstanceMethodInfo> nolockInstanceMethods = new ConcurrentDictionary<string, JniInstanceMethodInfo>();
		public JniObjectReference Timing_ToString_DictWithNoLock ()
		{
			var m = nolockInstanceMethods.AddOrUpdate (toString_name + toString_sig,
				s => TypeRef.GetInstanceMethod (toString_name, toString_sig),
				(s, c) => c ?? TypeRef.GetInstanceMethod (toString_name, toString_sig));
			return m.InvokeVirtualObjectMethod (PeerReference);
		}
	}
}

