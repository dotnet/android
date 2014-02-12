using System;

namespace Java.Interop
{
	public class JavaObjectArray<T> : JavaArray<T>
		where T : class, IJavaObject
	{
		public JavaObjectArray (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		static JniLocalReference _NewArray (int length)
		{
			var info = JniEnvironment.Current.JavaVM.GetJniTypeInfoForType (typeof (T));
			if (info.JniTypeName == null)
				info.JniTypeName = "java/lang/Object";
			using (var t = new JniType (info.ToString ())) {
				return JniEnvironment.Arrays.NewObjectArray (length, t.SafeHandle, JniReferenceSafeHandle.Null);
			}
		}

		public JavaObjectArray (int length)
			: this (_NewArray (length), JniHandleOwnership.Transfer)
		{
		}

		// TODO: remove `IJavaObject` constraint
		public override T this [int index] {
			get {
				if (index < 0 || index >= Length)
					throw new ArgumentOutOfRangeException ("index", "index < 0 || index >= Length");
				var lref = JniEnvironment.Arrays.GetObjectArrayElement (SafeHandle, index);
				return (T) JniEnvironment.Current.JavaVM.GetObject (lref, JniHandleOwnership.Transfer, typeof (T));
			}
			set {
				if (index < 0 || index >= Length)
					throw new ArgumentOutOfRangeException ("index", "index < 0 || index >= Length");
				if (value != null && !value.SafeHandle.IsInvalid)
					value.Register ();
				JniEnvironment.Arrays.SetObjectArrayElement (SafeHandle, index,
						value == null || value.SafeHandle.IsInvalid
						? JniReferenceSafeHandle.Null
						: value.SafeHandle);
			}
		}
	}
}

