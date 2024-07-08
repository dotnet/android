using System;
using System.Collections.Generic;
using System.Linq;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests;

[TestFixture]
public class JavaPeerableExtensionsTests {

	[Test]
	public void JavaAs_Exceptions ()
	{
		using var v = new MyJavaInterfaceImpl ();

		// The Java type corresponding to JavaObjectWithMissingJavaPeer doesn't exist
		Assert.Throws<ArgumentException>(() => v.JavaAs<JavaObjectWithMissingJavaPeer>());

		var r = v.PeerReference;
		using var o = new JavaObject (ref r, JniObjectReferenceOptions.Copy);
		// MyJavaInterfaceImpl doesn't provide an activation constructor
		Assert.Throws<NotSupportedException>(() => o.JavaAs<MyJavaInterfaceImpl>());
#if !__ANDROID__
		// JavaObjectWithNoJavaPeer has no Java peer
		Assert.Throws<ArgumentException>(() => v.JavaAs<JavaObjectWithNoJavaPeer>());
#endif  // !__ANDROID__
	}

	[Test]
	public void JavaAs_NullSelfReturnsNull ()
	{
		Assert.AreEqual (null, JavaPeerableExtensions.JavaAs<IAndroidInterface> (null));
	}

	public void JavaAs_InvalidPeerRefReturnsNull ()
	{
		var v   = new MyJavaInterfaceImpl ();
		v.Dispose ();
		Assert.AreEqual (null, JavaPeerableExtensions.JavaAs<IJavaInterface> (v));
	}

	[Test]
	public void JavaAs_InstanceThatDoesNotImplementInterfaceReturnsNull ()
	{
		using var v = new MyJavaInterfaceImpl ();
		Assert.AreEqual (null, JavaPeerableExtensions.JavaAs<IAndroidInterface> (v));
	}

	[Test]
	public void JavaAs ()
	{
		using var impl  = new MyJavaInterfaceImpl ();
		using var iface = impl.JavaAs<IJavaInterface> ();
		Assert.IsNotNull (iface);
		Assert.AreEqual ("Hello from Java!", iface.Value);
	}
}

// Note: Java side implements JavaInterface, while managed binding DOES NOT.
[JniTypeSignature (JniTypeName, GenerateJavaPeer=false)]
public class MyJavaInterfaceImpl : JavaObject {
	internal            const       string          JniTypeName    = "net/dot/jni/test/MyJavaInterfaceImpl";

	internal    static  readonly    JniPeerMembers  _members    = new JniPeerMembers (JniTypeName, typeof (MyJavaInterfaceImpl));

	public override JniPeerMembers JniPeerMembers {
		get {return _members;}
	}

	public unsafe MyJavaInterfaceImpl ()
		: base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
	{
		const   string  id  = "()V";
		var peer = _members.InstanceMethods.StartCreateInstance (id, GetType (), null);
		Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
		_members.InstanceMethods.FinishCreateInstance (id, this, null);
	}
}

[JniTypeSignature (JniTypeName, GenerateJavaPeer=false)]
interface IJavaInterface : IJavaPeerable {
	internal            const       string          JniTypeName    = "net/dot/jni/test/JavaInterface";

	public string Value {
		[JniMethodSignatureAttribute("getValue", "()Ljava/lang/String;")]
		get;
	}
}

[JniTypeSignature (IJavaInterface.JniTypeName, GenerateJavaPeer=false)]
internal class IJavaInterfaceInvoker : JavaObject, IJavaInterface {

	internal    static  readonly    JniPeerMembers  _members    = new JniPeerMembers (IJavaInterface.JniTypeName, typeof (IJavaInterfaceInvoker));

	public override JniPeerMembers JniPeerMembers {
		get {return _members;}
	}

	public IJavaInterfaceInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options)
		: base (ref reference, options)
	{
	}

	public unsafe string Value {
		get {
			const string id = "getValue.()Ljava/lang/String;";
			var r = JniPeerMembers.InstanceMethods.InvokeVirtualObjectMethod (id, this, null);
			return JniEnvironment.Strings.ToString (ref r, JniObjectReferenceOptions.CopyAndDispose);
		}
	}
}
