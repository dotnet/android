using System;

namespace Java.Interop {

	public static class JavaPeerableExtensions {

		public static string GetJniTypeName (this IJavaPeerable self)
		{
			JniPeerMembers.AssertSelf (self);
			return JniEnvironment.Types.GetJniTypeNameFromInstance (self.PeerReference);
		}

		internal static object GetValue (ref JniObjectReference handle, JniObjectReferenceOptions transfer, Type targetType)
		{
			return JniEnvironment.Runtime.ValueMarshaler.GetObject (ref handle, transfer, targetType);
		}

		internal static JniObjectReference CreateLocalRef (object value)
		{
			var o = value as IJavaPeerable;
			if (o == null || !o.PeerReference.IsValid)
				return new JniObjectReference ();
			return o.PeerReference.NewLocalRef ();
		}
	}
}
