using System;

using Java.Interop;
using Java.Interop.GenericMarshaler;

namespace Java.InteropTests {

	[JniTypeSignature (CallVirtualFromConstructorBase.JniTypeName, GenerateJavaPeer=false)]
	public class CallVirtualFromConstructorBase : JavaObject {

		internal    const   string          JniTypeName = "net/dot/jni/test/CallVirtualFromConstructorBase";
		readonly    static  JniPeerMembers  _members    = new JniPeerMembers (JniTypeName, typeof (CallVirtualFromConstructorBase));

		public override JniPeerMembers JniPeerMembers {
			get {return _members;}
		}

		public unsafe CallVirtualFromConstructorBase (int value)
			: this (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			if (PeerReference.IsValid)
				return;

			var peer    = JniPeerMembers.InstanceMethods.StartGenericCreateInstance ("(I)V", GetType (), value);
			Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
			JniPeerMembers.InstanceMethods.FinishGenericCreateInstance ("(I)V", this, value);
		}

		public CallVirtualFromConstructorBase (ref JniObjectReference reference, JniObjectReferenceOptions options)
			: base (ref reference, options)
		{
		}

		public virtual void CalledFromConstructor (int value)
		{
			_members.InstanceMethods.InvokeGenericVirtualVoidMethod ("calledFromConstructor.(I)V", this, value);
		}
	}
}

