using System;

using Java.Interop;

namespace Java.InteropTests
{
	[JniTypeSignature (CallNonvirtualBase.JniTypeName)]
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

		public virtual unsafe void Method ()
		{
			_members.InstanceMethods.InvokeVirtualVoidMethod ("method\u0000()V", this, null);
		}

		public bool MethodInvoked {
			get {return _members.InstanceFields.GetBooleanValue ("methodInvoked\u0000Z" ,this);}
			set {_members.InstanceFields.SetValue ("methodInvoked\u0000Z", this, value);}
		}
	}
}

