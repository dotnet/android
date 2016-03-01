using System;

using Java.Interop;

namespace Java.InteropTests
{
	[JniTypeSignature (CallVirtualFromConstructorDerived.JniTypeName)]
	public class CallVirtualFromConstructorDerived : CallVirtualFromConstructorBase {
		new internal    const   string          JniTypeName = "com/xamarin/interop/CallVirtualFromConstructorDerived";
		static  readonly        JniPeerMembers  _members    = new JniPeerMembers (JniTypeName, typeof (CallVirtualFromConstructorDerived));

		static void __RegisterNativeMembers (JniType type, string members)
		{
			_members.JniPeerType.RegisterNativeMethods (
					new JniNativeMethodRegistration ("calledFromConstructor", "(I)V", (Action<IntPtr, IntPtr, int>) CalledFromConstructorHandler));
		}

		public override JniPeerMembers JniPeerMembers {
			get {return _members;}
		}

		int calledValue;

		public  bool    InvokedConstructor;

		public CallVirtualFromConstructorDerived (int value)
			: base (value)
		{
			InvokedConstructor = true;
			if (value != calledValue)
				throw new ArgumentException (
						string.Format ("value '{0}' doesn't match expected value '{1}'.", value, calledValue),
						"value");
		}

		public  bool    InvokedActivationConstructor;

		public CallVirtualFromConstructorDerived (ref JniObjectReference reference, JniObjectReferenceOptions options)
			: base (ref reference, options)
		{
			InvokedActivationConstructor    = true;
		}

		public bool Called;

		public override void CalledFromConstructor (int value)
		{
			Called      = true;
			calledValue = value;
		}

		public static unsafe CallVirtualFromConstructorDerived NewInstance (int value)
		{
			JniArgumentValue* args = stackalloc JniArgumentValue [1];
			args [0]    = new JniArgumentValue (value);
			var o       = _members.StaticMethods.InvokeObjectMethod ("newInstance.(I)Lcom/xamarin/interop/CallVirtualFromConstructorDerived;", args);
			return JniEnvironment.Runtime.ValueManager.GetValue<CallVirtualFromConstructorDerived> (ref o, JniObjectReferenceOptions.CopyAndDispose);
		}

		static void CalledFromConstructorHandler (IntPtr jnienv, IntPtr n_self, int value)
		{
			var envp = new JniTransition (jnienv);
			try {
				var r_self  = new JniObjectReference (n_self);
				var self    = JniEnvironment.Runtime.ValueManager.GetValue<CallVirtualFromConstructorDerived>(ref r_self, JniObjectReferenceOptions.Copy);
				self.CalledFromConstructor (value);
				self.DisposeUnlessReferenced ();
			}
			catch (Exception e) when (JniEnvironment.Runtime.ExceptionShouldTransitionToJni (e)) {
				envp.SetPendingException (e);
			}
			finally {
				envp.Dispose ();
			}
		}
	}
}

