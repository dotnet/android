using System;

namespace Java.Interop
{
	/// <include file="../Documentation/Java.Interop/JniManagedPeerStates.xml" path="/docs/member[@name='T:JniManagedPeerStates']/*" />
	[Flags]
	public enum JniManagedPeerStates {
		None,
		/// <include file="../Documentation/Java.Interop/JniManagedPeerStates.xml" path="/docs/member[@name='F:Activatable']/*" />
		Activatable         = (1 << 0),
		/// <include file="../Documentation/Java.Interop/JniManagedPeerStates.xml" path="/docs/member[@name='F:Replaceable']/*" />
		Replaceable         = (1 << 1),
	}
}
