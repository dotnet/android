using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests;

[TestFixture]
class JavaCallableExampleTest : JavaVMFixture
{
	[Test]
	public void JavaCallableWrappers ()
	{
		using var jce = CreateUseJavaCallableExampleType ();
		var m = jce.GetStaticMethod ("test", "()Z");
		var z = JniEnvironment.StaticMethods.CallStaticBooleanMethod (jce.PeerReference, m);
		Assert.IsTrue (z);
	}

	static JniType CreateUseJavaCallableExampleType () =>
		new JniType ("net/dot/jni/test/UseJavaCallableExample");
}
