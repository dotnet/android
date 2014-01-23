// This really should be auto-generated, but isn't for test purposes.
using System;

using Java.Interop;

namespace Java.InteropTests
{
	public partial class TestObjectBinding : IDisposable
	{
		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, "com/xamarin/interop/TestType");}
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
			return JniEnvironment.Strings.ToString (
					TypeRef.GetCachedInstanceMethod (ref Object_toString, "toString", "()Ljava/lang/String;")
						.CallVirtualObjectMethod (SafeHandle),
					JniHandleOwnership.Transfer);
		}
	}

	partial class TestObjectBinding {
		static JniNativeMethodRegistration[] Methods = new JniNativeMethodRegistration[] {
			new JniNativeMethodRegistration ("getInt32Value", "()I", GetInt32ValueHandler ()),
			new JniNativeMethodRegistration ("getStringValue", "(I)Ljava/lang/String;", GetStringValueHandler ()),
		};

		static TestObjectBinding ()
		{
			TypeRef.RegisterNativeMethods (Methods);
		}

		static Delegate GetInt32ValueHandler ()
		{
			Func<IntPtr, IntPtr, int> h = (jnienv, self) => {
				return 54;
			};
			return h;
		}

		static Delegate GetStringValueHandler ()
		{
			Func<IntPtr, IntPtr, int, IntPtr> h = (jnienv, self, value) => {
				return IntPtr.Zero;
			};
			return h;
		}

		public int GetInt32Value ()
		{
			return 42;
		}

		public JniLocalReference GetStringValue ()
		{
			return null;
		}
	}
}

