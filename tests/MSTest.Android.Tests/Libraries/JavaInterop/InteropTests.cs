using AndroidMSTestProof.TestLibrary;
using Java.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]

namespace AndroidMSTestProof.JavaInterop;

[TestClass]
public class InteropTests : LifecycleBase
{
	[TestMethod]
	[TestCategory ("proof")]
	[TestCategory ("jni")]
	public void ReferencedJNI ()
	{
		AssertInitialized ();
		using var value = new Java.Lang.String ("referenced JNI");
		Assert.AreEqual ("referenced JNI", value.ToString ());
		Assert.AreNotEqual (IntPtr.Zero, JniEnvironment.EnvironmentPointer);
		Context.WriteLine ("[D ASSEMBLY] " + typeof (InteropTests).Assembly.GetName ().Name);
	}

#pragma warning disable MSTEST0087 // Matching display names deliberately test distinct JNI row identities.
	[TestMethod]
	[TestCategory ("proof")]
	[TestCategory ("jni")]
	[DataRow ("first", DisplayName = "same JNI display")]
	[DataRow ("second", DisplayName = "same JNI display")]
	public void StringRoundtrip (string text)
	{
		AssertInitialized ();
		var reference = JniEnvironment.Strings.NewString (text);
		try {
			Assert.IsTrue (reference.IsValid);
			Assert.AreEqual (text, JniEnvironment.Strings.ToString (reference));
		} finally {
			JniObjectReference.Dispose (ref reference);
		}
	}
#pragma warning restore MSTEST0087

	[TestMethod]
	[TestCategory ("diagnostic")]
	public void GeneratedInteropFailure ()
	{
		AssertInitialized ();
		Assert.Fail ("intentional referenced-JavaInterop generated invocation failure");
	}
}
