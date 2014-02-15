using System;

using Java.Interop;

namespace Java.InteropTests
{
	public class CallNonvirtualBase : JavaObject
	{
		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, "com/xamarin/interop/CallNonvirtualBase");}
		}

		public override Type JniThresholdType {
			get {return typeof (CallNonvirtualBase);}
		}
		public override JniType JniThresholdClass {
			get {return TypeRef;}
		}

		static JniInstanceMethodID Base_ctor;
		static JniLocalReference _NewObject ()
		{
			TypeRef.GetCachedConstructor (ref Base_ctor, "()V");
			return TypeRef.NewObject (Base_ctor);
		}

		public CallNonvirtualBase ()
			: base (_NewObject (), JniHandleOwnership.Transfer)
		{
		}

		protected CallNonvirtualBase (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		static JniInstanceMethodID _method;
		public virtual void Method ()
		{
			TypeRef.GetCachedInstanceMethod (ref _method, "method", "()V");
			if (GetType () == JniThresholdType)
				_method.CallVirtualVoidMethod (SafeHandle);
			else {
				// Ugh. Just...Ugh. No caching at all!
				JniThresholdClass.GetInstanceMethod ("method", "()V")
					.CallNonvirtualVoidMethod (SafeHandle, JniThresholdClass.SafeHandle);
			}
		}

		static JniInstanceFieldID _methodInvoked;
		public bool MethodInvoked {
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

