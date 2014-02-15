using System;

using Java.Interop;

namespace Java.InteropTests
{
	public class CallNonvirtualDerived : CallNonvirtualBase
	{
		readonly static JniPeerMembers _members = new JniPeerMembers ("com/xamarin/interop/CallNonvirtualDerived", typeof (CallNonvirtualBase));

		public override JniPeerMembers JniMembers {
			get {return _members;}
		}

		static JniLocalReference _NewObject ()
		{
			var c = _members.GetConstructor ("()V");
			return _members.JniPeerType.NewObject (c);
		}

		public CallNonvirtualDerived ()
			: base (_NewObject (), JniHandleOwnership.Transfer)
		{
		}

		protected CallNonvirtualDerived (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public new bool MethodInvoked {
			get {return _members.GetBooleanInstanceFieldValue (this, "methodInvoked");}
			set {_members.SetInstanceFieldValue (this, "methodInvoked", value);}
		}
	}
}

