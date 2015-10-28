using System;
using System.Collections.Generic;
using System.Reflection;

using Android.Runtime;

namespace Java.Interop {

	// FOR TEST PURPOSES ONLY
	public  delegate    IntPtr              SafeHandleDelegate_CallObjectMethodA    (IntPtr   env,    IntPtr    instance,   IntPtr method, JValue[]    args);
	public  delegate    void                SafeHandleDelegate_DeleteLocalRef       (IntPtr   env,    IntPtr    handle);

	delegate int JNIEnv_GetJavaVM (IntPtr jnienv, out IntPtr vm);


	class AndroidVMBuilder : JniRuntime.CreationOptions {

		public AndroidVMBuilder ()
		{
			var GetJavaVM = (JNIEnv_GetJavaVM) Delegate.CreateDelegate (
					typeof(JNIEnv_GetJavaVM),
					typeof(JNIEnv).GetMethod ("GetJavaVM", BindingFlags.NonPublic | BindingFlags.Static));
			IntPtr invocationPointer;
			GetJavaVM (JNIEnv.Handle, out invocationPointer);

			EnvironmentPointer   = JNIEnv.Handle;
			NewObjectRequired   = ((int) Android.OS.Build.VERSION.SdkInt) <= 10;
			InvocationPointer   = invocationPointer;
			ObjectReferenceManager      = Java.InteropTests.LoggingJniObjectReferenceManagerDecorator.GetObjectReferenceManager (new JniObjectReferenceManager ());
		}

		public AndroidVM CreateAndroidVM ()
		{
			return new AndroidVM (this);
		}
	}

	public class AndroidVM : JniRuntime {

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
			JniEnvironment.References.Dispose (ref handle);
			Java.Lang.Runtime.GetRuntime ().Gc ();
			handle = wgref.NewGlobalRef ();
			JniEnvironment.References.Dispose (ref wgref);
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

