using System;
using System.Runtime.CompilerServices;

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
			: this (value, useNewObject: false)
		{
		}

		public unsafe CallVirtualFromConstructorBase (int value, bool useNewObject)
			: this (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			if (PeerReference.IsValid)
				return;

			const string __id = "(I)V";

			if (useNewObject) {
				var ctors   = JniPeerMembers.InstanceMethods.GetConstructorsForType (GetType ());
				var init    = ctors.GetConstructor (__id);

				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (value);
				var lref = JniEnvironment.Object.NewObject (ctors.JniPeerType.PeerReference, init, __args);
				Construct (ref lref, JniObjectReferenceOptions.CopyAndDispose);
				return;
			}
			var peer    = JniPeerMembers.InstanceMethods.StartGenericCreateInstance (__id, GetType (), value);
			Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
			JniPeerMembers.InstanceMethods.FinishGenericCreateInstance (__id, this, value);
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

