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

	partial class JavaObject {
		const       JniManagedPeerStates        Disposed            = (JniManagedPeerStates) (1 << 2);
	}

	partial class JavaException {
		const       JniManagedPeerStates        Disposed            = (JniManagedPeerStates) (1 << 2);
	}
}
