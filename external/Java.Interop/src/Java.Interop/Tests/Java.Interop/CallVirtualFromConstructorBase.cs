using System;

using Java.Interop;
using Java.Interop.GenericMarshaler;

namespace Java.InteropTests {

	[JniTypeSignature (CallVirtualFromConstructorBase.JniTypeName)]
	public class CallVirtualFromConstructorBase : JavaObject {

		internal    const   string          JniTypeName = "com/xamarin/interop/CallVirtualFromConstructorBase";
		readonly    static  JniPeerMembers  _members    = new JniPeerMembers (JniTypeName, typeof (CallVirtualFromConstructorBase));

		public override JniPeerMembers JniPeerMembers {
			get {return _members;}
		}

		public unsafe CallVirtualFromConstructorBase (int value)
			: base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			var peer    = JniPeerMembers.InstanceMethods.StartGenericCreateInstance ("(I)V", GetType (), value);
			using (SetPeerReference (
						ref peer,
						JniObjectReferenceOptions.CopyAndDispose)) {
				JniPeerMembers.InstanceMethods.FinishGenericCreateInstance ("(I)V", this, value);
			}
		}

		public virtual void CalledFromConstructor (int value)
		{
			_members.InstanceMethods.InvokeGenericVirtualVoidMethod ("calledFromConstructor.(I)V", this, value);
		}
	}
}

