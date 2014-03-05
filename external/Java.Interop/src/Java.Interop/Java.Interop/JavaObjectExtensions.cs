using System;

namespace Java.Interop {

	public static class JavaObjectExtensions {

		public static string GetJniTypeName (this IJavaObject self)
		{
			JniPeerMembers.AssertSelf (self);
			return JniEnvironment.Types.GetJniTypeNameFromInstance (self.SafeHandle);
		}

		internal static object GetValue (JniReferenceSafeHandle handle, JniHandleOwnership transfer, Type targetType)
		{
			return JniEnvironment.Current.JavaVM.GetObject (handle, transfer, targetType);
		}

		internal static JniLocalReference CreateLocalRef (object value)
		{
			var o = value as IJavaObject;
			if (o == null || o.SafeHandle == null || o.SafeHandle.IsInvalid)
				return new JniLocalReference ();
			return o.SafeHandle.NewLocalRef ();
		}
	}
}
