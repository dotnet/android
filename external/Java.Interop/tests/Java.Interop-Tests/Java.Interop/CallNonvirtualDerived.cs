using System;

using Java.Interop;

namespace Java.InteropTests
{
	[JniTypeSignature (CallNonvirtualDerived.JniTypeName)]
	public class CallNonvirtualDerived : CallNonvirtualBase
	{
		internal new const string JniTypeName = "net/dot/jni/test/CallNonvirtualDerived";

		readonly static JniPeerMembers _members = new JniPeerMembers (JniTypeName, typeof (CallNonvirtualDerived));

		public override JniPeerMembers JniPeerMembers {
			get {return _members;}
		}

		public CallNonvirtualDerived ()
		{
		}

		public new bool MethodInvoked {
			get {return _members.InstanceFields.GetBooleanValue ("methodInvoked.Z", this);}
			set {_members.InstanceFields.SetValue ("methodInvoked.Z", this, value);}
		}
	}
}

