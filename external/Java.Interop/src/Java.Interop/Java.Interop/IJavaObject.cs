using System;

namespace Java.Interop
{
	public interface IJavaObject : IDisposable
	{
		JniObjectReference      PeerReference  {get;}
		JniPeerMembers          JniPeerMembers {get;}

		void    RegisterWithVM ();
		void    DisposeUnlessRegistered ();
	}
}

