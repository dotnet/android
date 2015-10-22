using System;
using System.Collections.Generic;

using Android.Runtime;

namespace Java.Interop {

	// FOR TEST PURPOSES ONLY
	public  delegate    IntPtr              SafeHandleDelegate_CallObjectMethodA    (IntPtr   env,    IntPtr    instance,   IntPtr method, JValue[]    args);
	public  delegate    void                SafeHandleDelegate_DeleteLocalRef       (IntPtr   env,    IntPtr    handle);

	class AndroidVMBuilder : JavaVMOptions {

		public AndroidVMBuilder ()
		{
			EnvironmentPointer   = JNIEnv.Handle;
			NewObjectRequired   = ((int) Android.OS.Build.VERSION.SdkInt) <= 10;
			using (var env = new JniEnvironment (JNIEnv.Handle)) {
				IntPtr vm;
				int r = JniEnvironment.Handles.GetJavaVM (out vm);
				if (r < 0)
					throw new InvalidOperationException ("JNIEnv::GetJavaVM() returned: " + r);
				InvocationPointer    = vm;
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

		protected override bool TryGC (IJavaPeerable value, ref JniObjectReference handle)
		{
			if (!handle.IsValid)
				return true;
			var wgref = handle.NewWeakGlobalRef ();
			JniEnvironment.Handles.Dispose (ref handle);
			Java.Lang.Runtime.GetRuntime ().Gc ();
			handle = wgref.NewGlobalRef ();
			JniEnvironment.Handles.Dispose (ref wgref);
			return handle.IsValid;
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

