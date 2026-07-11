using System.Runtime.CompilerServices;

using Android.Runtime;
using Java.Interop;

namespace JniReferenceLeakTests;

[TestClass]
public sealed class GlobalReferenceTests
{
	[TestMethod]
	public void TryFindClassUtf8DoesNotLeakGlobalReferences ()
	{
		ReferenceTestHelpers.AssertNoGlobalReferenceLeak (() => {
			Assert.IsFalse (JniEnvironment.Types.TryFindClass ("does/not/Exist"u8, out var notFound));
			Assert.IsFalse (notFound.IsValid);
		});
	}

	[TestMethod]
	public void TryFindClassStringDoesNotLeakGlobalReferences ()
	{
		ReferenceTestHelpers.AssertNoGlobalReferenceLeak (() => {
			Assert.IsFalse (JniEnvironment.Types.TryFindClass ("does/not/Exist", out var notFound));
			Assert.IsFalse (notFound.IsValid);
		});
	}

	[TestMethod]
	public void JavaObjectArrayOperationsDoNotLeakGlobalReferences ()
	{
		ReferenceTestHelpers.AssertNoGlobalReferenceLeak (CreateAndDisposeObjectArray);
	}

	[TestMethod]
	public void JavaSideActivationDoesNotLeakGlobalReferences ()
	{
		ReferenceTestHelpers.AssertNoGlobalReferenceLeak (() => {
			using var instance = ActivationProbe.CreateFromJava ();
			Assert.IsTrue (instance.DefaultConstructorInvoked);
		});
	}

	[MethodImpl (MethodImplOptions.NoInlining)]
	static void CreateAndDisposeObjectArray ()
	{
		var value = new object ();
		using var array = new JavaObjectArray<object> (1);
		array [0] = value;
	}
}

[Register (JniName)]
public class ActivationProbe : Java.Lang.Object
{
	internal const string JniName = "net/dot/jni/referenceleaktests/ActivationProbe";
	const string FactoryJniName = "net/dot/jni/referenceleaktests/ActivationProbeFactory";

	public bool DefaultConstructorInvoked { get; }

	public ActivationProbe ()
	{
		DefaultConstructorInvoked = true;
	}

	public static unsafe ActivationProbe CreateFromJava ()
	{
		using var type = new JniType (JniName);
		using var factory = new JniType (FactoryJniName);
		var create = factory.GetStaticMethod ("create", "(Ljava/lang/Class;)Ljava/lang/Object;");
		JniArgumentValue* arguments = stackalloc JniArgumentValue [1];
		arguments [0] = new JniArgumentValue (type.PeerReference);
		var reference = JniEnvironment.StaticMethods.CallStaticObjectMethod (factory.PeerReference, create, arguments);
		try {
			if (JniEnvironment.Runtime.ValueManager.PeekPeer (reference) is not ActivationProbe instance) {
				throw new InvalidOperationException ("Java-side activation did not create an ActivationProbe peer.");
			}

			return instance;
		} finally {
			JniObjectReference.Dispose (ref reference);
		}
	}
}
