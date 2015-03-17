using System;
using System.Collections.Generic;

using Android.Runtime;

namespace Java.Interop {

	// FOR TEST PURPOSES ONLY
	public  delegate    JniLocalReference   SafeHandleDelegate_CallObjectMethodA    (JniEnvironmentSafeHandle   env,    JniReferenceSafeHandle  instance,   JniInstanceMethodID method, JValue[]    args);
	public  delegate    void                SafeHandleDelegate_DeleteLocalRef       (JniEnvironmentSafeHandle   env,    IntPtr handle);

	class AndroidVMBuilder : JavaVMOptions {

		public AndroidVMBuilder ()
		{
			EnvironmentHandle   = new JniEnvironmentSafeHandle (JNIEnv.Handle);
			NewObjectRequired   = ((int) Android.OS.Build.VERSION.SdkInt) <= 10;
			using (var env = new JniEnvironment (JNIEnv.Handle)) {
				JavaVMSafeHandle vm;
				int r = JniEnvironment.Handles.GetJavaVM (out vm);
				if (r < 0)
					throw new InvalidOperationException ("JNIEnv::GetJavaVM() returned: " + r);
				VMHandle    = vm;
			}
			JniHandleManager    = Java.InteropTests.LoggingJniHandleManagerDecorator.GetHandleManager (new JniHandleManager ());
		}

		public AndroidVM CreateAndroidVM ()
		{
			return new AndroidVM (this);
		}
	}

	public class AndroidVM : JavaVM {

		internal AndroidVM (AndroidVMBuilder builder)
			: base (builder)
		{
		}

		static  readonly    AndroidVM   current = new AndroidVMBuilder ().CreateAndroidVM ();

		public static new AndroidVM Current {
			get {return current;}
		}

		protected override bool TryGC (IJavaObject value, ref JniReferenceSafeHandle handle)
		{
			System.Diagnostics.Debug.WriteLine ("# AndroidVM.TryGC");
			if (handle == null || handle.IsInvalid)
				return true;
			var wgref = handle.NewWeakGlobalRef ();
			System.Diagnostics.Debug.WriteLine ("# AndroidVM.TryGC: wgref=0x{0}", wgref.DangerousGetHandle().ToString ("x"));;
			handle.Dispose ();
			Java.Lang.Runtime.GetRuntime ().Gc ();
			handle = wgref.NewGlobalRef ();
			System.Diagnostics.Debug.WriteLine ("# AndroidVM.TryGC: handle.IsInvalid={0}", handle.IsInvalid);
			return handle == null || handle.IsInvalid;
		}

		Dictionary<string, Type> typeMappings   = new Dictionary<string, Type> ();

		public void AddTypeMapping (string jniTypeReference, Type type)
		{
			lock (typeMappings) {
				typeMappings [jniTypeReference] = type;
			}
		}

		public override Type GetTypeForJniSimplifiedTypeReference (string jniTypeReference)
		{
			Type target = base.GetTypeForJniSimplifiedTypeReference (jniTypeReference);
			if (target != null)
				return target;
			lock (typeMappings) {
				if (typeMappings != null && typeMappings.TryGetValue (jniTypeReference, out target))
					return target;
			}
			return null;
		}
	}
}

