using System;

using Java.Interop;

namespace Java.InteropTests
{
	[JniTypeInfo (CallNonvirtualBase.JniTypeName)]
	public class CallNonvirtualBase : JavaObject
	{
		internal const string JniTypeName = "com/xamarin/interop/CallNonvirtualBase";

		readonly static JniPeerMembers _members = new JniPeerMembers (JniTypeName, typeof (CallNonvirtualBase));

		public override JniPeerMembers JniPeerMembers {
			get {return _members;}
		}

		public CallNonvirtualBase ()
		{
		}

		public virtual void Method ()
		{
			_members.CallInstanceVoidMethod ("method", "()V", "method()V", this);
		}

		public bool MethodInvoked {
			get {return _members.GetBooleanInstanceFieldValue (this, "methodInvoked");}
			set {_members.SetInstanceFieldValue (this, "methodInvoked", value);}
		}
	}
}

