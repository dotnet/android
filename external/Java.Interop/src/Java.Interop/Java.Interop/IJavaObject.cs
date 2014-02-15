using System;

namespace Java.Interop
{
	public interface IJavaObject : IDisposable
	{
		JniReferenceSafeHandle  SafeHandle {get;}
		JniPeerMembers          JniMembers {get;}

		void    Register ();
	}
}

