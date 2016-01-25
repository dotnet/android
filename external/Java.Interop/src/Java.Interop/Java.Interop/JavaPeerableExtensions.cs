using System;

namespace Java.Interop {

	public static class JavaPeerableExtensions {

		public static string GetJniTypeName (this IJavaPeerable self)
		{
			JniPeerMembers.AssertSelf (self);
			return JniEnvironment.Types.GetJniTypeNameFromInstance (self.PeerReference);
		}

		public static int GetJniIdentityHashCode (this IJavaPeerable self)
		{
			JniPeerMembers.AssertSelf (self);
			return JniSystem.IdentityHashCode (self.PeerReference);
		}
	}
}
