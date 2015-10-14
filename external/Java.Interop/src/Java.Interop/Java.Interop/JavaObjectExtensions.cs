using System;

namespace Java.Interop {

	public static class JavaObjectExtensions {

		public static string GetJniTypeName (this IJavaObject self)
		{
			JniPeerMembers.AssertSelf (self);
			return JniEnvironment.Types.GetJniTypeNameFromInstance (self.PeerReference);
		}

		internal static object GetValue (ref JniObjectReference handle, JniHandleOwnership transfer, Type targetType)
		{
			return JniEnvironment.Current.JavaVM.GetObject (ref handle, transfer, targetType);
		}

		internal static JniObjectReference CreateLocalRef (object value)
		{
			var o = value as IJavaObject;
			if (o == null || !o.PeerReference.IsValid)
				return new JniObjectReference ();
			return o.PeerReference.NewLocalRef ();
		}
	}
}
