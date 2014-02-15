using System;

using Java.Interop;

namespace Java.InteropTests
{
	public class CallNonvirtualDerived2 : CallNonvirtualDerived
	{
		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, "com/xamarin/interop/CallNonvirtualDerived2");}
		}

		static JniInstanceMethodID Derived_ctor;
		static JniLocalReference _NewObject ()
		{
			TypeRef.GetCachedConstructor (ref Derived_ctor, "()V");
			return TypeRef.NewObject (Derived_ctor);
		}

		public CallNonvirtualDerived2 ()
			: base (_NewObject (), JniHandleOwnership.Transfer)
		{
		}
	}
}

