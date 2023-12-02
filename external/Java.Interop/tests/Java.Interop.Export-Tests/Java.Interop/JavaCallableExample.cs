using System;
using System.Text;

using Java.Interop;

namespace Java.InteropTests;

[JniTypeSignature (TypeSignature)]
class JavaCallableExample : Java.Lang.Object {

	internal const string TypeSignature = "net/dot/jni/test/JavaCallableExample";

	[JavaCallableConstructor(SuperConstructorExpression="")]
	public JavaCallableExample (int[] a, JavaInt32Array b)
	{
		this.a = a;
		this.b = b;

		var bDescription    = new StringBuilder ();
		for (int i = 0; i < b.Length; ++i) {
			if (i > 0)
				bDescription.Append (", ");
			bDescription.Append (b [i]);
		}
		Console.WriteLine ($"JavaCallableExample ({{{string.Join (", ", a)}}}, {{{bDescription}}})");
	}

	int[] a;
	JavaInt32Array b;

	[JavaCallable ("getFirstA")]
	public int GetFirstA ()
	{
		return a [0];
	}
}
