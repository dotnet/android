namespace Example;

using Java.Interop;

[JniTypeSignature (JniTypeName)]
class ManagedType : Java.Lang.Object {
	internal const string JniTypeName = "example/ManagedType";

	[JavaCallableConstructor(SuperConstructorExpression="")]
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
}
