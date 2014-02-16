using System;

using Java.Interop;

namespace Java.InteropTests
{
	[JniTypeInfo (CallNonvirtualDerived.JniTypeName)]
	public class CallNonvirtualDerived : CallNonvirtualBase
	{
		internal new const string JniTypeName = "com/xamarin/interop/CallNonvirtualDerived";

		readonly static JniPeerMembers _members = new JniPeerMembers (JniTypeName, typeof (CallNonvirtualBase));

		public override JniPeerMembers JniPeerMembers {
			get {return _members;}
		}

		public CallNonvirtualDerived ()
		{
		}

		public new bool MethodInvoked {
			get {return _members.GetBooleanInstanceFieldValue (this, "methodInvoked");}
			set {_members.SetInstanceFieldValue (this, "methodInvoked", value);}
		}
	}
}

