using System;

using Java.Interop;

namespace Java.InteropTests
{
	public class CallNonvirtualBase : JavaObject
	{
		readonly static JniPeerMembers _members = new JniPeerMembers ("com/xamarin/interop/CallNonvirtualBase", typeof (CallNonvirtualBase));

		public override JniPeerMembers JniPeerMembers {
			get {return _members;}
		}

		static JniLocalReference _NewObject ()
		{
			var c = _members.GetConstructor ("()V");
			return _members.JniPeerType.NewObject (c);
		}

		public CallNonvirtualBase ()
			: base (_NewObject (), JniHandleOwnership.Transfer)
		{
		}

		protected CallNonvirtualBase (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
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

