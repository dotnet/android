using System;

using Android.Runtime;

namespace Java.Interop {

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

		public static new JavaVM Current {
			get {return current;}
		}

		protected override bool TryGC (IJavaObject value, ref JniReferenceSafeHandle handle)
		{
			System.Diagnostics.Debug.WriteLine ("# AndroidVM.TryGC");
			if (handle == null || handle.IsInvalid)
				return true;
			return false;
		}
	}
}

