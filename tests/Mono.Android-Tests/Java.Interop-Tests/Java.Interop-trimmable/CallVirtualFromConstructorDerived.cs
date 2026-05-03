#nullable enable

using System;
using System.Runtime.CompilerServices;

using Java.Interop;

namespace Java.InteropTests
{
	[JniTypeSignature (CallVirtualFromConstructorDerived.JniTypeName, GenerateJavaPeer=false)]
	public class CallVirtualFromConstructorDerived : CallVirtualFromConstructorBase {
		new internal    const   string          JniTypeName = "net/dot/jni/test/CallVirtualFromConstructorDerived";
		static  readonly        JniPeerMembers  _members    = new JniPeerMembers (JniTypeName, typeof (CallVirtualFromConstructorDerived));

		[JniAddNativeMethodRegistrationAttribute]
		static void RegisterNativeMembers (JniNativeMethodRegistrationArguments args)
		{
			args.Registrations.Add (new JniNativeMethodRegistration ("calledFromConstructor", "(I)V", (CalledFromConstructorMarshalMethod)CalledFromConstructorHandler));
		}

		public override JniPeerMembers JniPeerMembers {
			get {return _members;}
		}

		int calledValue;

		public  bool    InvokedConstructor;

		[Register (".ctor", "(I)V", "")]
		public CallVirtualFromConstructorDerived (int value)
			: this (value, useNewObject: false)
		{
		}

		public CallVirtualFromConstructorDerived (int value, bool useNewObject)
			: base (value, useNewObject)
		{
			InvokedConstructor = true;

			if (useNewObject && calledValue != 0) {
				// calledValue was set on a *different* instance! So it's 0 here.
				throw new ArgumentException (
						string.Format ("value '{0}' doesn't match expected value '{1}'.", value, 0),
						"value");
			}
			if (!useNewObject && value != calledValue)
				throw new ArgumentException (
						string.Format ("value '{0}' doesn't match expected value '{1}'.", value, calledValue),
						"value");
		}

		public  bool    InvokedActivationConstructor;

		public  static  CallVirtualFromConstructorDerived?  Intermediate_FromCalledFromConstructor;
		public  static  CallVirtualFromConstructorDerived?  Intermediate_FromActivationConstructor;

		public CallVirtualFromConstructorDerived (ref JniObjectReference reference, JniObjectReferenceOptions options)
			: base (ref reference, options)
		{
			InvokedActivationConstructor    = true;

			Intermediate_FromActivationConstructor  = this;
		}

		public bool Called;

		[Register ("calledFromConstructor", "(I)V", "")]
		public override void CalledFromConstructor (int value)
		{
			Called      = true;
			calledValue = value;

			Intermediate_FromCalledFromConstructor  = this;
		}

		public static unsafe CallVirtualFromConstructorDerived NewInstance (int value)
		{
			JniArgumentValue* args = stackalloc JniArgumentValue [1];
			args [0]    = new JniArgumentValue (value);
			var o       = _members.StaticMethods.InvokeObjectMethod ("newInstance.(I)Lnet/dot/jni/test/CallVirtualFromConstructorDerived;", args);
			var result  = JniEnvironment.Runtime.ValueManager.GetValue<CallVirtualFromConstructorDerived> (ref o, JniObjectReferenceOptions.CopyAndDispose);
			if (result == null)
				throw new InvalidOperationException ("newInstance returned null.");
			return result;
		}

		delegate void CalledFromConstructorMarshalMethod (IntPtr jnienv, IntPtr n_self, int value);
		static void CalledFromConstructorHandler (IntPtr jnienv, IntPtr n_self, int value)
		{
			n_CalledFromConstructor (jnienv, n_self, value);
		}

		static void n_CalledFromConstructor (IntPtr jnienv, IntPtr n_self, int value)
		{
			var envp = new JniTransition (jnienv);
			try {
				var r_self  = new JniObjectReference (n_self);
				var self    = JniEnvironment.Runtime.ValueManager.GetValue<CallVirtualFromConstructorDerived>(ref r_self, JniObjectReferenceOptions.Copy);
				if (self == null)
					throw new InvalidOperationException ("calledFromConstructor received null self.");
				self.CalledFromConstructor (value);
				self.InvokedConstructor = true;
				((IJavaPeerable) self).SetJniManagedPeerState (self.JniManagedPeerState | JniManagedPeerStates.Replaceable);
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

	[AttributeUsage (AttributeTargets.Constructor | AttributeTargets.Method)]
	sealed class RegisterAttribute : Attribute {
		public RegisterAttribute (string name, string signature, string connector)
		{
		}
	}
}
