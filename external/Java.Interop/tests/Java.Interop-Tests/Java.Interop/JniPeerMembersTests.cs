using System;
using System.Collections.Generic;
using System.Reflection;

using Java.Interop;
using Java.Interop.GenericMarshaler;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniPeerMembersTests : JavaVMFixture
	{
		[Test]
		public void Ctor_CanReferenceNonexistentType ()
		{
			var members = new JniPeerMembers (JavaObjectWithMissingJavaPeer.JniTypeName, typeof(JavaObjectWithMissingJavaPeer));
			JniPeerMembers.Dispose (members);
		}

		[Test]
		public void VirtualInvokeOnBaseInvokesMostDerivedJavaMethod ()
		{
			var registered  = GetInstanceMethods (MyString._members.InstanceMethods);
			Assert.AreEqual (0, registered.Count);
			using (var s = new MyString ("hello!")) {
				Assert.AreEqual (1, registered.Count);  // for the constructor
				Assert.AreEqual ("hello!", s.ToString ());
				Assert.AreEqual (1, registered.Count);
			}
		}

		static Dictionary<string, JniMethodInfo> GetInstanceMethods (JniPeerMembers.JniInstanceMethods methods)
		{
			var f   = typeof (JniPeerMembers.JniInstanceMethods).GetField ("InstanceMethods", BindingFlags.NonPublic | BindingFlags.Instance);
			return (Dictionary<string, JniMethodInfo>) f.GetValue (methods);
		}

#if NET
		[Test]
		public void MethodLookupForNonexistentStaticMethodWillTryFallbacks ()
		{
			try {
				JavaLangRemappingTestRuntime.doesNotExist ();
				Assert.Fail ("java.lang.Runtime.doesNotExist() exists?!  Not expected to exist.");
			}
			catch (Exception e) {
				Console.WriteLine ($"# jonp: MethodLookupForNonexistentStaticMethodWillTryFallbacks: e={e}");
				// On Desktop, expect `e` to be:
				// ```
				// Java.Interop.JavaException: doesNotExist
				//    at Java.Interop.JniEnvironment.StaticMethods.GetMethodID(JniObjectReference type, String name, String signature)
				//    …
				//    at Java.InteropTests.JavaLangRuntime.doesNotExist()
				//    at Java.InteropTests.JniStaticMethodIDTest.MethodLookupForNonexistentMethodWillTryFallbacks()
				//   --- End of managed Java.Interop.JavaException stack trace ---
				// java.lang.NoSuchMethodError: doesNotExist
				// ```
				// On Android, expect `e` to be:
				// ```
				// Java.Lang.NoSuchMethodError: no static method "Ljava/lang/Runtime;.doesNotExist()V"
				//    at Java.Interop.JniEnvironment.StaticMethods.GetStaticMethodID(JniObjectReference type, String name, String signature)
				//    at Java.Interop.JniType.GetStaticMethod(String name, String signature)
				//    at Java.Interop.JniPeerMembers.JniStaticMethods.GetMethodInfo(String method, String signature)
				//    at Java.Interop.JniPeerMembers.JniStaticMethods.GetMethodInfo(String encodedMember)
				//    at Java.Interop.JniPeerMembers.JniStaticMethods.InvokeVoidMethod(String encodedMember, JniArgumentValue* parameters)
				//    at Java.InteropTests.JavaLangRemappingTestRuntime.doesNotExist()
				//    at Java.InteropTests.JniPeerMembersTests.MethodLookupForNonexistentStaticMethodWillTryFallbacks()
				//   --- End of managed Java.Lang.NoSuchMethodError stack trace ---
				// ```
				Assert.IsTrue (e.Message.Contains ("doesNotExist", StringComparison.Ordinal));
#if !ANDROID    // Android doesn't allow providing a custom TypeManager
				Assert.AreEqual ("java/lang/Runtime",
						JavaVMFixture.TypeManager.RequestedFallbackTypesForSimpleReference);
#endif  // !ANDROID
			}
		}

		[Test]
		public void ReplacementTypeUsedForMethodLookup ()
		{
			using var o = new RenameClassDerived ();
			int r = o.hashCode();
			Assert.AreEqual (33, r);
		}

		[Test]
		public void ReplaceInstanceMethodName ()
		{
			using var o = new JavaLangRemappingTestObject ();
			// Shouldn't throw; should instead invoke Object.toString()
			var r = o.remappedToToString ();
			JniObjectReference.Dispose (ref r);
		}

		[Test]
		public void ReplaceStaticMethodName ()
		{
			var r = JavaLangRemappingTestRuntime.remappedToGetRuntime ();
			JniObjectReference.Dispose (ref r);
		}

		[Test]
		public void ReplaceInstanceMethodWithStaticMethod ()
		{
			using var o = new JavaLangRemappingTestObject ();
			// Shouldn't throw; should instead invoke ObjectHelper.getHashCodeHelper(Object)
			o.remappedToStaticHashCode ();
		}
#endif  // NET
	}

	[JniTypeSignature (JniTypeName)]
	class MyString : JavaObject {
		internal    const   string      JniTypeName = "java/lang/String";

		internal    static  readonly    JniPeerMembers  _members    = new JniPeerMembers (JniTypeName, typeof (MyString));

		public override JniPeerMembers JniPeerMembers {
			get {return _members;}
		}

		public unsafe MyString (string value)
			: base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			const   string  id  = "(Ljava/lang/String;)V";
			var peer = _members.InstanceMethods.StartGenericCreateInstance (id, GetType (), value);
			Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
			_members.InstanceMethods.FinishGenericCreateInstance (id, this, value);
		}
	}


	[JniTypeSignature (JniTypeName)]
	class JavaLangRemappingTestObject : JavaObject {
		internal    const    string         JniTypeName = "java/lang/Object";
		static      readonly JniPeerMembers _members    = new JniPeerMembers (JniTypeName, typeof (JavaLangRemappingTestObject));

		public JavaLangRemappingTestObject ()
		{
		}

		public unsafe void doesNotExist ()
		{
			const string id = "doesNotExist.()V";
			_members.InstanceMethods.InvokeNonvirtualVoidMethod (id, this, null);
		}

		public unsafe JniObjectReference remappedToToString ()
		{
			const string id = "remappedToToString.()Ljava/lang/String;";
			return _members.InstanceMethods.InvokeNonvirtualObjectMethod (id, this, null);
		}

		public unsafe int remappedToStaticHashCode ()
		{
			const string id = "remappedToStaticHashCode.()I";
			return _members.InstanceMethods.InvokeVirtualInt32Method (id, this, null);
		}
	}

	[JniTypeSignature (JavaLangRemappingTestRuntime.JniTypeName)]
	internal class JavaLangRemappingTestRuntime : JavaObject {
		internal    const    string         JniTypeName = "java/lang/Runtime";
		static      readonly JniPeerMembers _members    = new JniPeerMembers (JniTypeName, typeof (JavaLangRemappingTestRuntime));

		public static unsafe JniObjectReference remappedToGetRuntime()
		{
			const string id = "remappedToGetRuntime.()Ljava/lang/Runtime;";
			return _members.StaticMethods.InvokeObjectMethod (id, null);
		}

		public static unsafe void doesNotExist ()
		{
			const string id = "doesNotExist.()V";
			_members.StaticMethods.InvokeVoidMethod (id, null);
		}
	}

	[JniTypeSignature (JniTypeName)]
	class RenameClassBase : JavaObject {
		internal    const       string          JniTypeName    = "com/xamarin/interop/RenameClassBase1";
		static      readonly    JniPeerMembers  _members        = new JniPeerMembers (JniTypeName, typeof (RenameClassBase));

		public      override    JniPeerMembers  JniPeerMembers  => _members;

		public RenameClassBase ()
		{
		}

		public virtual unsafe int hashCode ()
		{
			const string id = "hashCode.()I";
			return _members.InstanceMethods.InvokeVirtualInt32Method (id, this, null);
		}
	}

	[JniTypeSignature (JniTypeName)]
	class RenameClassDerived : RenameClassBase {
		internal    new     const       string          JniTypeName    = "com/xamarin/interop/RenameClassDerived";
		public RenameClassDerived ()
		{
		}

		public override unsafe int hashCode ()
		{
			return base.hashCode ();
		}
	}
}
