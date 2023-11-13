using Java.Interop;

namespace Java.InteropTests;

[JniTypeSignature (TypeSignature)]
class JavaCallableExample : Java.Lang.Object {

	internal const string TypeSignature = "net/dot/jni/test/JavaCallableExample";

	[JavaCallableConstructor(SuperConstructorExpression="")]
	public JavaCallableExample (int a)
	{
		this.a = a;
	}

	int a;

	[JavaCallable ("getA")]
	public int GetA ()
	{
		return a;
	}
}
