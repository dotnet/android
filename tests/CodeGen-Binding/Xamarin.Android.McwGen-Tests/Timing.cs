using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Android.Runtime;

using Java.Interop;

namespace Com.Xamarin.Android {

	// Provoke https://github.com/dotnet/android/issues/4660
	// We need this type to appear *before* `Timing` in `monodis --typedef` output
	[Register ("com/xamarin/android/Timing", DoNotGenerateAcw = true)]
	public partial class Timinf<T> : Timing {
	}

	public partial class Timing {

		static IntPtr jonp_id_VirtualVoidMethod;
		[Register ("VirtualVoidMethod", "()V", "GetVirtualVoidMethodHandler")]
		public virtual unsafe void VirtualVoidMethod_Timing_Traditional ()
		{
			if (jonp_id_VirtualVoidMethod == IntPtr.Zero)
				jonp_id_VirtualVoidMethod = JNIEnv.GetMethodID (class_ref, "VirtualVoidMethod", "()V");

			if (GetType () == typeof (Timing))
				JNIEnv.CallVoidMethod  (Handle, jonp_id_VirtualVoidMethod);
			else {
				JNIEnv.CallNonvirtualVoidMethod  (Handle, class_ref, JNIEnv.GetMethodID (class_ref, "VirtualVoidMethod", "()V"));
			}
		}

		static Dictionary<IntPtr, IntPtr> vvmMethodCache = new Dictionary<IntPtr, IntPtr> ();
		public virtual unsafe void VirtualVoidMethod_Timing_TraditionalWithCaching ()
		{
			if (jonp_id_VirtualVoidMethod == IntPtr.Zero)
				jonp_id_VirtualVoidMethod = JNIEnv.GetMethodID (class_ref, "VirtualVoidMethod", "()V");

			if (GetType () == typeof (Timing))
				JNIEnv.CallVoidMethod  (Handle, jonp_id_VirtualVoidMethod);
			else {
				IntPtr m;
				lock (vvmMethodCache) {
					if (!vvmMethodCache.TryGetValue (class_ref, out m))
						vvmMethodCache.Add (class_ref, m = JNIEnv.GetMethodID (class_ref, "VirtualVoidMethod", "()V"));
				}
				JNIEnv.CallNonvirtualVoidMethod  (Handle, class_ref, m);
			}
		}

		public unsafe void VirtualVoidMethod_Timing_NoCache ()
		{
			var m = JNIEnv.GetMethodID (class_ref, "VirtualVoidMethod", "()V");

			if (GetType () == typeof (Timing))
				JNIEnv.CallVoidMethod  (Handle, m);
			else
				JNIEnv.CallNonvirtualVoidMethod  (Handle, class_ref, JNIEnv.GetMethodID (class_ref, "VirtualVoidMethod", "()V"));
		}

		static Dictionary<string, IntPtr> dictInstanceMethods = new Dictionary<string, IntPtr>();
		public unsafe void VirtualVoidMethod_Timing_DictWithLock ()
		{
			const string id = "VirtualVoidMethod.()V";
			IntPtr m;
			lock (dictInstanceMethods) {
				if (!dictInstanceMethods.TryGetValue (id, out m))
					dictInstanceMethods.Add (id, m = JNIEnv.GetMethodID (class_ref, "VirtualVoidMethod", "()V"));
			}

			if (GetType () == typeof (Timing))
				JNIEnv.CallVoidMethod  (Handle, m);
			else
				JNIEnv.CallNonvirtualVoidMethod  (Handle, class_ref, JNIEnv.GetMethodID (class_ref, "VirtualVoidMethod", "()V"));
		}

		static ConcurrentDictionary<string, IntPtr> concurrentInstanceMethods = new ConcurrentDictionary<string, IntPtr>();
		public unsafe void VirtualVoidMethod_Timing_ConcurrentDict ()
		{
			const string id = "VirtualVoidMethod.()V";

			var m = concurrentInstanceMethods.AddOrUpdate (id,
				s => JNIEnv.GetMethodID (class_ref, "VirtualVoidMethod", "()V"),
				(s, c) => c != IntPtr.Zero ? c : JNIEnv.GetMethodID (class_ref, "VirtualVoidMethod", "()V"));

			if (GetType () == typeof (Timing))
				JNIEnv.CallVoidMethod  (Handle, m);
			else
				JNIEnv.CallNonvirtualVoidMethod  (Handle, class_ref, JNIEnv.GetMethodID (class_ref, "VirtualVoidMethod", "()V"));
		}

		internal     static  readonly    JniPeerMembers  _jonp_members    = new JniPeerMembers ("com/xamarin/android/Timing", typeof (Timing));
		public unsafe void VirtualVoidMethod_Timing_JniPeerMembers ()
		{
			const string id = "VirtualVoidMethod.()V";
			_jonp_members.InstanceMethods.InvokeVirtualVoidMethod (id, this, null);
		}
	}
}
