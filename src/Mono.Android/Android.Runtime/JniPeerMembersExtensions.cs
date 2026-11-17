namespace Android.Runtime; 

	internal static class JniPeerMembersExtensions {

		internal static IntPtr GetPeerTypeHandle (this JniPeerMembers members)
		{
			return members.JniPeerType.PeerReference.Handle;
		}
	}
