using System;

using Java.Interop;

namespace Java.InteropTests
{
	public class CallNonvirtualDerived : CallNonvirtualBase
	{
		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, "com/xamarin/interop/CallNonvirtualDerived");}
		}

		public override Type JniThresholdType {
			get {return typeof (CallNonvirtualDerived);}
		}
		public override JniType JniThresholdClass {
			get {return TypeRef;}
		}

		static JniInstanceMethodID Derived_ctor;
		static JniLocalReference _NewObject ()
		{
			TypeRef.GetCachedConstructor (ref Derived_ctor, "()V");
			return TypeRef.NewObject (Derived_ctor);
		}

		public CallNonvirtualDerived ()
			: base (_NewObject (), JniHandleOwnership.Transfer)
		{
		}

		protected CallNonvirtualDerived (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		static JniInstanceFieldID _methodInvoked;
		public new bool MethodInvoked {
			get {
				TypeRef.GetCachedInstanceField (ref _methodInvoked, "methodInvoked", "Z");
				return _methodInvoked.GetBooleanValue (SafeHandle);
			}
			set {
				TypeRef.GetCachedInstanceField (ref _methodInvoked, "methodInvoked", "Z");
				_methodInvoked.SetValue (SafeHandle, value);
			}
		}
	}
}

