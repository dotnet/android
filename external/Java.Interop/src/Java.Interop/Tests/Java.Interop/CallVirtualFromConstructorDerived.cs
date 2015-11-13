using System;

using Java.Interop;

namespace Java.InteropTests
{
	[JniTypeSignature (CallVirtualFromConstructorDerived.JniTypeName)]
	public class CallVirtualFromConstructorDerived : CallVirtualFromConstructorBase {
		new internal    const   string          JniTypeName = "com/xamarin/interop/CallVirtualFromConstructorDerived";
		static  readonly        JniPeerMembers  _members    = new JniPeerMembers (JniTypeName, typeof (CallVirtualFromConstructorDerived));

		static CallVirtualFromConstructorDerived ()
		{
			_members.JniPeerType.RegisterNativeMethods (
					new JniNativeMethodRegistration ("calledFromConstructor", "(I)V", (Action<IntPtr, IntPtr, int>) CalledFromConstructorHandler));
		}

		public override JniPeerMembers JniPeerMembers {
			get {return _members;}
		}

		int calledValue;

		public CallVirtualFromConstructorDerived (int value)
			: base (value)
		{
			if (value != calledValue)
				throw new ArgumentException (
						string.Format ("value '{0}' doesn't match expected value '{1}'.", value, calledValue),
						"value");
		}

		public bool Called;

		public override void CalledFromConstructor (int value)
		{
			Called      = true;
			calledValue = value;
		}

		static void CalledFromConstructorHandler (IntPtr jnienv, IntPtr n_self, int value)
		{
			var self = JniEnvironment.Runtime.ValueMarshaler.GetObject<CallVirtualFromConstructorDerived>(n_self);
			self.CalledFromConstructor (value);
			self.DisposeUnlessRegistered ();
		}
	}
}

