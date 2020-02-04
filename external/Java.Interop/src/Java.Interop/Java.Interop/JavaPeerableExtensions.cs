#nullable enable

using System;

namespace Java.Interop {

	public static class JavaPeerableExtensions {

		public static string? GetJniTypeName (this IJavaPeerable self)
		{
			JniPeerMembers.AssertSelf (self);
			return JniEnvironment.Types.GetJniTypeNameFromInstance (self.PeerReference);
		}
	}
}
