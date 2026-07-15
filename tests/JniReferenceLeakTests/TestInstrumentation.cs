using System.Reflection;
using Android.Runtime;
using Xamarin.Android.UnitTests;

namespace JniReferenceLeakTests;

[Instrumentation (Name = "net.dot.jni.referenceleaktests.TestInstrumentation")]
public class TestInstrumentation : Xamarin.Android.UnitTests.TestInstrumentation
{
	public TestInstrumentation (IntPtr handle, JniHandleOwnership transfer)
		: base (handle, transfer)
	{
	}

	protected override IEnumerable<Assembly> GetTestAssemblies ()
	{
		return [Assembly.GetExecutingAssembly ()];
	}
}
