// This really should be auto-generated, but isn't for test purposes.
using System;

using Java.Interop;

namespace Java.InteropTests
{
	public class TestObjectBinding : IDisposable
	{
		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, "java/lang/Object");}
		}

		public JniGlobalReference SafeHandle {get; private set;}

		JniInstanceMethodID Object_ctor;
		public TestObjectBinding ()
		{
			TypeRef.GetCachedConstructor (ref Object_ctor, "()V");
			using (var lref = TypeRef.NewObject (Object_ctor))
				SafeHandle = lref.NewGlobalRef ();
		}

		public void Dispose ()
		{
			if (SafeHandle == null)
				return;
			SafeHandle.Dispose ();
			SafeHandle = null;
		}

		JniInstanceMethodID Object_toString;
		public override string ToString ()
		{
			return JniStrings.ToString (
					TypeRef.GetCachedInstanceMethod (ref Object_toString, "toString", "()Ljava/lang/String;")
						.CallVirtualObjectMethod (SafeHandle),
					JniHandleOwnership.Transfer);
		}
	}
}

