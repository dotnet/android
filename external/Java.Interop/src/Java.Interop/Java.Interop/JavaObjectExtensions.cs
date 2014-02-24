using System;

namespace Java.Interop {

	public static class JavaObjectExtensions {

		public static string GetJniTypeName (this IJavaObject self)
		{
			JniPeerMembers.AssertSelf (self);
			return JniEnvironment.Types.GetJniTypeNameFromInstance (self.SafeHandle);
		}
	}
}
