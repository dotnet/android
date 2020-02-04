#nullable enable

using System;

namespace Java.Interop
{
	/// <include file="../Documentation/Java.Interop/IJavaPeerable.xml" path="/docs/member[@name='T:IJavaPeerable']/*" />
	public interface IJavaPeerable : IDisposable
	{
		/// <include file="../Documentation/Java.Interop/IJavaPeerable.xml" path="/docs/member[@name='P:JniIdentityHashCode']/*" />
		int                     JniIdentityHashCode {get;}
		/// <include file="../Documentation/Java.Interop/IJavaPeerable.xml" path="/docs/member[@name='M:SetJniIdentityHashCode']/*" />
		void                    SetJniIdentityHashCode  (int value);

		/// <include file="../Documentation/Java.Interop/IJavaPeerable.xml" path="/docs/member[@name='P:PeerReference']/*" />
		JniObjectReference      PeerReference  {get;}

		/// <include file="../Documentation/Java.Interop/IJavaPeerable.xml" path="/docs/member[@name='M:SetPeerReference']/*" />
		void                    SetPeerReference    (JniObjectReference reference);

		/// <include file="../Documentation/Java.Interop/IJavaPeerable.xml" path="/docs/member[@name='P:JniPeerMembers']/*" />
		JniPeerMembers          JniPeerMembers {get;}

		/// <include file="../Documentation/Java.Interop/IJavaPeerable.xml" path="/docs/member[@name='P:JniManagedPeerState']/*" />
		JniManagedPeerStates    JniManagedPeerState {get;}

		/// <include file="../Documentation/Java.Interop/IJavaPeerable.xml" path="/docs/member[@name='M:SetJniManagedPeerState']/*" />
		void                    SetJniManagedPeerState (JniManagedPeerStates value);

		// Lifetime management
		/// <include file="../Documentation/Java.Interop/IJavaPeerable.xml" path="/docs/member[@name='M:UnregisterFromRuntime']/*" />
		void    UnregisterFromRuntime ();
		/// <include file="../Documentation/Java.Interop/IJavaPeerable.xml" path="/docs/member[@name='M:DisposeUnlessReferenced']/*" />
		void    DisposeUnlessReferenced ();

		/// <include file="../Documentation/Java.Interop/IJavaPeerable.xml" path="/docs/member[@name='M:Disposed']/*" />
		void    Disposed ();
		/// <include file="../Documentation/Java.Interop/IJavaPeerable.xml" path="/docs/member[@name='M:Finalized']/*" />
		void    Finalized ();
	}
}

