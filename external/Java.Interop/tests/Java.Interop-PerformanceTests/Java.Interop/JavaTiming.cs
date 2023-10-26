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
		protected   const   string          JniTypeName = "com/xamarin/interop/performance/JavaTiming";
		static  readonly    JniPeerMembers  _members    = new JniPeerMembers (JniTypeName, typeof (JavaTiming));

		public override JniPeerMembers JniPeerMembers {
			get {return _members;}
		}

		static JniType _TypeRef;
		internal static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		static JniMethodInfo Object_ctor;
		static unsafe JniObjectReference _NewObject ()
		{
			TypeRef.GetCachedConstructor (ref Object_ctor, "()V");
			return TypeRef.NewObject (Object_ctor, null);
		}

		public unsafe JavaTiming ()
			: base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			var peer    = _NewObject ();
			Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
		}

		static JniMethodInfo svm;
		public static void StaticVoidMethod ()
		{
			TypeRef.GetCachedStaticMethod (ref svm, "StaticVoidMethod", "()V");
			JniEnvironment.StaticMethods.CallStaticVoidMethod (TypeRef.PeerReference, svm);
		}

		static JniMethodInfo sim;
		public static int StaticIntMethod ()
		{
			TypeRef.GetCachedStaticMethod (ref sim, "StaticIntMethod", "()I");
			return JniEnvironment.StaticMethods.CallStaticIntMethod (TypeRef.PeerReference, sim);
		}

		static JniMethodInfo som;
		public static IJavaPeerable StaticObjectMethod ()
		{
			TypeRef.GetCachedStaticMethod (ref som, "StaticObjectMethod", "()Ljava/lang/Object;");
			var lref = JniEnvironment.StaticMethods.CallStaticObjectMethod (TypeRef.PeerReference, som);
			return JniEnvironment.Runtime.ValueManager.GetValue<IJavaPeerable> (ref lref, JniObjectReferenceOptions.CopyAndDispose);
		}

		static JniMethodInfo vvm;
		public virtual void VirtualVoidMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref vvm, "VirtualVoidMethod", "()V");
			JniEnvironment.InstanceMethods.CallObjectMethod (PeerReference, vvm);
		}

		static JniMethodInfo vim;
		public virtual int VirtualIntMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref vim, "VirtualIntMethod", "()I");
			return JniEnvironment.InstanceMethods.CallIntMethod (PeerReference, vim);
		}

		static JniMethodInfo vom;
		public virtual IJavaPeerable VirtualObjectMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref vom, "VirtualObjectMethod", "()Ljava/lang/Object;");
			var lref = JniEnvironment.InstanceMethods.CallObjectMethod (PeerReference, vom);
			return JniEnvironment.Runtime.ValueManager.GetValue<IJavaPeerable> (ref lref, JniObjectReferenceOptions.CopyAndDispose);
		}

		static JniMethodInfo fvm;
		public void FinalVoidMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref fvm, "FinalVoidMethod", "()V");
			JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (PeerReference, TypeRef.PeerReference, fvm);
		}

		static JniMethodInfo fim;
		public int FinalIntMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref fim, "FinalIntMethod", "()I");
			return JniEnvironment.InstanceMethods.CallNonvirtualIntMethod (PeerReference, TypeRef.PeerReference, fim);
		}

		static JniMethodInfo fom;
		public IJavaPeerable FinalObjectMethod ()
		{
			TypeRef.GetCachedInstanceMethod (ref fom, "FinalObjectMethod", "()Ljava/lang/Object;");
			var lref = JniEnvironment.InstanceMethods.CallNonvirtualObjectMethod (PeerReference, TypeRef.PeerReference, fom);
			return JniEnvironment.Runtime.ValueManager.GetValue<IJavaPeerable> (ref lref, JniObjectReferenceOptions.CopyAndDispose);
		}

		static JniMethodInfo vim1;
		public unsafe int VirtualIntMethod1Args (int value)
		{
			TypeRef.GetCachedInstanceMethod (ref vim1, "VirtualIntMethod1Args", "(I)I");

			var args = stackalloc JniArgumentValue [1];
			args [0] = new JniArgumentValue (value);
			int r;

			if (GetType () == _members.ManagedPeerType)
				r = JniEnvironment.InstanceMethods.CallIntMethod (PeerReference, vim1, args);
			else {
				JniMethodInfo m = JniPeerMembers.InstanceMethods.GetMethodInfo ("VirtualIntMethod1Args.(I)I");
				r = JniEnvironment.InstanceMethods.CallNonvirtualIntMethod (PeerReference, JniPeerMembers.JniPeerType.PeerReference, m, args);
			}
			return r;
		}

		public virtual unsafe int Timing_VirtualIntMethod_Marshal1Args (int value)
		{
			var args = stackalloc JniArgumentValue [1];
			args [0] = new JniArgumentValue (value);

			return _members.InstanceMethods.InvokeVirtualInt32Method ("VirtualIntMethod1Args.(I)I", this, args);
		}

		public virtual int Timing_VirtualIntMethod_GenericMarshal1Args (int value)
		{
			return _members.InstanceMethods.InvokeGenericVirtualInt32Method ("VirtualIntMethod1Args.(I)I", this, value);
		}

		static JniMethodInfo vim1_a;
		public unsafe int VirtualIntMethod1Args (int[][][] value)
		{
			TypeRef.GetCachedInstanceMethod (ref vim1_a, "VirtualIntMethod1Args", "([[[I)I");

			int r;

			using (var native_array = new JavaObjectArray<int[][]> (value)) {
				var args = stackalloc JniArgumentValue [1];
				args [0] = new JniArgumentValue (native_array);
				if (GetType () == _members.ManagedPeerType)
					r = JniEnvironment.InstanceMethods.CallIntMethod (PeerReference, vim1_a, args);
				else {
					JniMethodInfo m = JniPeerMembers.InstanceMethods.GetMethodInfo ("VirtualIntMethod1Args.([[[I)I");
					r = JniEnvironment.InstanceMethods.CallNonvirtualIntMethod (PeerReference, JniPeerMembers.JniPeerType.PeerReference, m, args);
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
					return _members.InstanceMethods.InvokeVirtualInt32Method ("VirtualIntMethod1Args.([[[I)I", this, args);
				} finally {
					native_array.CopyTo (value, 0);
				}
			}
		}

		public virtual int Timing_VirtualIntMethod_GenericMarshal1Args (int[][][] value)
		{
			return _members.InstanceMethods.InvokeGenericVirtualInt32Method ("VirtualIntMethod1Args.([[[I)I", this, value);
		}

		static JniMethodInfo svm1;
		public static unsafe void StaticVoidMethod1Args (IJavaPeerable obj1)
		{
			TypeRef.GetCachedStaticMethod (ref svm1, "StaticVoidMethod1Args",
					"(Ljava/lang/Object;)V");
			var args = stackalloc JniArgumentValue [1];
			args [0] = new JniArgumentValue (obj1);
			JniEnvironment.StaticMethods.CallStaticVoidMethod (TypeRef.PeerReference, svm1, args);
		}

		static JniMethodInfo svm2;
		public static unsafe void StaticVoidMethod2Args (IJavaPeerable obj1, IJavaPeerable obj2)
		{
			TypeRef.GetCachedStaticMethod (ref svm2, "StaticVoidMethod2Args",
					"(Ljava/lang/Object;Ljava/lang/Object;)V");
			var args = stackalloc JniArgumentValue [2];
			args [0] = new JniArgumentValue (obj1);
			args [1] = new JniArgumentValue (obj2);
			JniEnvironment.StaticMethods.CallStaticVoidMethod (TypeRef.PeerReference, svm2, args);
		}

		static JniMethodInfo svm3;
		public static unsafe void StaticVoidMethod3Args (IJavaPeerable obj1, IJavaPeerable obj2, IJavaPeerable obj3)
		{
			TypeRef.GetCachedStaticMethod (ref svm3, "StaticVoidMethod3Args",
				"(Ljava/lang/Object;Ljava/lang/Object;Ljava/lang/Object;)V");
			var args = stackalloc JniArgumentValue [3];
			args [0] = new JniArgumentValue (obj1);
			args [1] = new JniArgumentValue (obj2);
			args [1] = new JniArgumentValue (obj3);
			JniEnvironment.StaticMethods.CallStaticVoidMethod (TypeRef.PeerReference, svm3, args);
		}

		static JniMethodInfo svmi1;
		public static unsafe void StaticVoidMethod1IArgs (int obj1)
		{
			TypeRef.GetCachedStaticMethod (ref svmi1, "StaticVoidMethod1IArgs", "(I)V");
			var args = stackalloc JniArgumentValue [1];
			args [0] = new JniArgumentValue (obj1);
			JniEnvironment.StaticMethods.CallStaticVoidMethod (TypeRef.PeerReference, svmi1, args);
		}

		static JniMethodInfo svmi2;
		public static unsafe void StaticVoidMethod2IArgs (int obj1, int obj2)
		{
			TypeRef.GetCachedStaticMethod (ref svmi2, "StaticVoidMethod2IArgs", "(II)V");
			var args = stackalloc JniArgumentValue [2];
			args [0] = new JniArgumentValue (obj1);
			args [1] = new JniArgumentValue (obj2);
			JniEnvironment.StaticMethods.CallStaticVoidMethod (TypeRef.PeerReference, svmi2, args);
		}

		static JniMethodInfo svmi3;
		public static unsafe void StaticVoidMethod3IArgs (int obj1, int obj2, int obj3)
		{
			TypeRef.GetCachedStaticMethod (ref svmi3, "StaticVoidMethod3IArgs", "(III)V");
			var args = stackalloc JniArgumentValue [3];
			args [0] = new JniArgumentValue (obj1);
			args [1] = new JniArgumentValue (obj2);
			args [1] = new JniArgumentValue (obj3);
			JniEnvironment.StaticMethods.CallStaticVoidMethod (TypeRef.PeerReference, svmi3, args);
		}

		const string toString_name  = "toString";
		const string toString_sig   = "()Ljava/lang/String;";

		static JniMethodInfo toString;
		public JniObjectReference Timing_ToString_Traditional ()
		{
			TypeRef.GetCachedInstanceMethod (ref toString, toString_name, toString_sig);
			return JniEnvironment.InstanceMethods.CallObjectMethod (PeerReference, toString);
		}

		public JniObjectReference Timing_ToString_NoCache ()
		{
			var m = TypeRef.GetInstanceMethod (toString_name, toString_sig);
			return JniEnvironment.InstanceMethods.CallObjectMethod (PeerReference, m);
		}

		static Dictionary<string, JniMethodInfo> dictInstanceMethods = new Dictionary<string, JniMethodInfo>();
		public JniObjectReference Timing_ToString_DictWithLock ()
		{
			JniMethodInfo m;
			lock (dictInstanceMethods) {
				if (!dictInstanceMethods.TryGetValue (toString_name + toString_sig, out m))
					dictInstanceMethods.Add (toString_name + toString_sig, m = TypeRef.GetInstanceMethod (toString_name, toString_sig));
			}
			return JniEnvironment.InstanceMethods.CallObjectMethod (PeerReference, m);
		}

		static ConcurrentDictionary<string, JniMethodInfo> nolockInstanceMethods = new ConcurrentDictionary<string, JniMethodInfo>();
		public JniObjectReference Timing_ToString_DictWithNoLock ()
		{
			var m = nolockInstanceMethods.AddOrUpdate (toString_name + toString_sig,
				s => TypeRef.GetInstanceMethod (toString_name, toString_sig),
				(s, c) => c ?? TypeRef.GetInstanceMethod (toString_name, toString_sig));
			return JniEnvironment.InstanceMethods.CallObjectMethod (PeerReference, m);
		}

		public unsafe JniObjectReference Timing_ToString_JniPeerMembers ()
		{
			const string id = toString_name + "." + toString_sig;
			return _members.InstanceMethods.InvokeVirtualObjectMethod (id, this, null);
		}

		public static unsafe JniObjectReference CreateRunnable ()
		{
			return _members.StaticMethods.InvokeObjectMethod ("CreateRunnable.()Ljava/lang/Runnable;", null);
		}
	}

	[JniTypeSignature (JniTypeName)]
	class DerivedJavaTiming : JavaTiming {
	}
}

