namespace Example;

using System;
using Java.Interop;

[JniTypeSignature (JniTypeName)]
class ManagedType : Java.Lang.Object {
	internal const string JniTypeName = "example/ManagedType";

	[JavaCallableConstructor (SuperConstructorExpression="")]
	public ManagedType (int value)
	{
		this.value = value;
	}

	int value;

	[JavaCallable ("getString")]
	public Java.Lang.String GetString ()
	{
		return new Java.Lang.String ($"Hello from C#, via Java.Interop! Value={value}");
	}

	[System.Runtime.InteropServices.UnmanagedFunctionPointer (System.Runtime.InteropServices.CallingConvention.Winapi)]
	delegate IntPtr _JniMarshal_PP_L (IntPtr jnienv, IntPtr n_self);

	static IntPtr n_GetString (IntPtr jnienv, IntPtr n_self)
	{
		var r_self = new JniObjectReference (n_self);
		var self = JniEnvironment.Runtime.ValueManager.GetValue<ManagedType> (ref r_self, JniObjectReferenceOptions.CopyAndDoNotRegister);
		try {
			var result = self!.GetString ();
			var r = result.PeerReference.NewLocalRef ();
			return JniEnvironment.References.NewReturnToJniRef (r);
		} finally {
			self?.DisposeUnlessReferenced ();
		}
	}

	[JniAddNativeMethodRegistration]
	static void RegisterNativeMembers (JniNativeMethodRegistrationArguments args)
	{
		args.AddRegistrations (new [] {
			new JniNativeMethodRegistration ("n_GetString", "()Ljava/lang/String;", new _JniMarshal_PP_L (n_GetString)),
		});
	}
}
