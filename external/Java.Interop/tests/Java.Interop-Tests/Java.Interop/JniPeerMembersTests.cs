using System;
using System.Collections.Concurrent;
using System.Reflection;

using Java.Interop;
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
		public void Utf8Ctor_CanReferenceNonexistentType ()
		{
			var members = new Utf8PeerMembers ("does/not/Exist"u8.ToArray (), typeof(JavaObjectWithMissingJavaPeer));
			JniPeerMembers.Dispose (members);
		}

		[Test]
		public unsafe void Utf8MemberLookupIsCached ()
		{
			var members = new Utf8PeerMembers ("java/lang/Object"u8.ToArray (), typeof (JavaLangRemappingTestObject));
			try {
				byte [] encodedMethod = "hashCode.()I"u8.ToArray ();
				var first  = members.InstanceMethods.GetMethodInfo (encodedMethod);
				var second = members.InstanceMethods.GetMethodInfo (encodedMethod);
				Assert.AreSame (first, second);

				byte [] constructorSignature = [(byte) '(', (byte) ')', (byte) 'V'];
				var firstConstructor  = members.InstanceMethods.GetConstructor (constructorSignature);
				var secondConstructor = members.InstanceMethods.GetConstructor (constructorSignature);
				Assert.AreSame (firstConstructor, secondConstructor);

				using var value = new JavaLangRemappingTestObject ();
				Assert.AreEqual (value.remappedToStaticHashCode (), members.InstanceMethods.InvokeVirtualInt32Method ("hashCode.()I"u8, value, null));
			} finally {
				JniPeerMembers.Dispose (members);
			}
		}

		[Test]
		public void Utf8FieldLookupIsCached ()
		{
			var members = new Utf8PeerMembers ("java/lang/System"u8.ToArray (), typeof (JavaLangSystem));
			try {
				byte [] encodedField = "out.Ljava/io/PrintStream;"u8.ToArray ();
				var first  = members.StaticFields.GetFieldInfo (encodedField);
				var second = members.StaticFields.GetFieldInfo (encodedField);
				Assert.AreSame (first, second);
			} finally {
				JniPeerMembers.Dispose (members);
			}
		}

		[Test]
		[Category ("TrimmableTypeMapUnsupported")]
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

		static ConcurrentDictionary<string, JniMethodInfo> GetInstanceMethods (JniPeerMembers.JniInstanceMethods methods)
		{
			var f   = typeof (JniPeerMembers.JniInstanceMethods).GetField ("InstanceMethods", BindingFlags.NonPublic | BindingFlags.Instance);
			return (ConcurrentDictionary<string, JniMethodInfo>) f.GetValue (methods);
		}

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
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public void ReplacementTypeUsedForMethodLookup ()
		{
			using var o = new RenameClassDerived ();
			int r = o.hashCode();
			Assert.AreEqual (33, r);
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		public void ReplaceInstanceMethodName ()
		{
			using var o = new JavaLangRemappingTestObject ();
			// Shouldn't throw; should instead invoke Object.toString()
			var r = o.remappedToToString ();
			JniObjectReference.Dispose (ref r);
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		public void ReplaceStaticMethodName ()
		{
			var r = JavaLangRemappingTestRuntime.remappedToGetRuntime ();
			JniObjectReference.Dispose (ref r);
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		public void ReplaceInstanceMethodWithStaticMethod ()
		{
			using var o = new JavaLangRemappingTestObject ();
			// Shouldn't throw; should instead invoke ObjectHelper.getHashCodeHelper(Object)
			o.remappedToStaticHashCode ();
		}

#if !__ANDROID__
		// Note: this test looks up a static method from one class, then
		// calls `JNIEnv::CallStaticObjectMethod()` passing in a jclass
		// for a *different* class.
		//
		// This appears to work on Desktop JVM.
		//
		// On Android, this will ABORT the app:
		//  JNI DETECTED ERROR IN APPLICATION: can't call static int com.xamarin.interop.DesugarAndroidInterface$_CC.getClassName() with class java.lang.Class<com.xamarin.interop.AndroidInterface>
		//      in call to CallStaticObjectMethodA
		//
		// This *also* aborts on JDK-17 + macOS + arm64:
		//  FATAL ERROR in native method: Wrong object class or methodID passed to JNI call
		//  Native frames: (J=compiled Java code, j=interpreted, Vv=VM code, C=native code)
		//  V  [libjvm.dylib+0x4f718c]  jniCheck::validate_call(JavaThread*, _jclass*, _jmethodID*, _jobject*)+0x98
		//  V  [libjvm.dylib+0x506474]  checked_jni_CallStaticObjectMethodA+0x150
		//  C  [native JNI wrapper]  java_interop_jnienv_call_static_object_method_a+0x48
		//  C  0x000000010798a8d8
		//  C  0x000000010798a540
		//  C  0x000000010798fa94
		//  C  [libcoreclr.dylib+0x2db774]  CallDescrWorkerInternal+0x84
		//  C  [libcoreclr.dylib+0x150db4]  CallDescrWorkerWithHandler(CallDescrData*, int)+0x74
		//  C  [libcoreclr.dylib+0x1f92d0]  RuntimeMethodHandle::InvokeMethod(Object*, void**, SignatureNative*, bool)+0x79c
		//  C  0x0000000104020ce4
		//  …
		//
		// *Fascinating* the differences that can appear between JVM implementations
		[Test]
		public unsafe void DoesTheJmethodNeedToMatchDeclaringType ()
		{
			if (Environment.GetEnvironmentVariable ("CPUTYPE") is string cpu && cpu == "arm64") {
				Assert.Ignore ("nope!");
			}
			if (VM.JdkInfo.Version is Version v && v >= new Version (17, 0)) {
				// JDK-17 now crashes on this test as well:
				// FATAL ERROR in native method: Wrong object class or methodID passed to JNI call
				Assert.Ignore ("nope!");
			}
			var iface   = new JniType ("net/dot/jni/test/AndroidInterface");
			var desugar = new JniType ("net/dot/jni/test/DesugarAndroidInterface$_CC");
			var m       = desugar.GetStaticMethod ("getClassName", "()Ljava/lang/String;");

			var r = JniEnvironment.StaticMethods.CallStaticObjectMethod (iface.PeerReference, m, null);
			var s = JniEnvironment.Strings.ToString (ref r, JniObjectReferenceOptions.CopyAndDispose);
			Assert.AreEqual ("DesugarAndroidInterface$-CC", s);
		}
#endif  // !__ANDROID__

		[Test]
		public void DesugarInterfaceStaticMethod ()
		{
			var s = IAndroidInterface.getClassName ();
			Assert.AreEqual ("DesugarAndroidInterface$-CC", s);
		}
	}

	[JniTypeSignature (JniTypeName, GenerateJavaPeer=false)]
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


	[JniTypeSignature (JniTypeName, GenerateJavaPeer=false)]
	class JavaLangRemappingTestObject : JavaObject {
		internal    const    string         JniTypeName = "java/lang/Object";
		static      readonly JniPeerMembers _members    = new JniPeerMembers (JniTypeName, typeof (JavaLangRemappingTestObject));

		public JavaLangRemappingTestObject ()
		{
		}

		public unsafe void doesNotExist ()
		{
			ReadOnlySpan<byte> id = "doesNotExist.()V"u8;
			_members.InstanceMethods.InvokeNonvirtualVoidMethod (id, this, null);
		}

		public unsafe JniObjectReference remappedToToString ()
		{
			ReadOnlySpan<byte> id = "remappedToToString.()Ljava/lang/String;"u8;
			return _members.InstanceMethods.InvokeNonvirtualObjectMethod (id, this, null);
		}

		public unsafe int remappedToStaticHashCode ()
		{
			ReadOnlySpan<byte> id = "remappedToStaticHashCode.()I"u8;
			return _members.InstanceMethods.InvokeVirtualInt32Method (id, this, null);
		}
	}

	[JniTypeSignature (JavaLangRemappingTestRuntime.JniTypeName, GenerateJavaPeer=false)]
	internal class JavaLangRemappingTestRuntime : JavaObject {
		internal    const    string         JniTypeName = "java/lang/Runtime";
		static      readonly JniPeerMembers _members    = new JniPeerMembers (JniTypeName, typeof (JavaLangRemappingTestRuntime));

		public static unsafe JniObjectReference remappedToGetRuntime()
		{
			ReadOnlySpan<byte> id = "remappedToGetRuntime.()Ljava/lang/Runtime;"u8;
			return _members.StaticMethods.InvokeObjectMethod (id, null);
		}

		public static unsafe void doesNotExist ()
		{
			ReadOnlySpan<byte> id = "doesNotExist.()V"u8;
			_members.StaticMethods.InvokeVoidMethod (id, null);
		}
	}

	[JniTypeSignature ("java/lang/System", GenerateJavaPeer=false)]
	internal class JavaLangSystem : JavaObject {
	}

	internal class Utf8PeerMembers : JniPeerMembers {

		public Utf8PeerMembers (ReadOnlyMemory<byte> jniPeerTypeName, Type managedPeerType)
			: base (jniPeerTypeName, managedPeerType)
		{
		}
	}

	[JniTypeSignature (JniTypeName, GenerateJavaPeer=false)]
	class RenameClassBase : JavaObject {
		internal    const       string          JniTypeName    = "net/dot/jni/test/RenameClassBase1";
		static      readonly    JniPeerMembers  _members        = new JniPeerMembers (JniTypeName, typeof (RenameClassBase));

		public      override    JniPeerMembers  JniPeerMembers  => _members;

		public RenameClassBase ()
		{
		}

		public virtual unsafe int hashCode ()
		{
			ReadOnlySpan<byte> id = "hashCode.()I"u8;
			return _members.InstanceMethods.InvokeVirtualInt32Method (id, this, null);
		}
	}

	[JniTypeSignature (JniTypeName, GenerateJavaPeer=false)]
	class RenameClassDerived : RenameClassBase {
		internal    new     const       string          JniTypeName    = "net/dot/jni/test/RenameClassDerived";
		public RenameClassDerived ()
		{
		}

		public override unsafe int hashCode ()
		{
			return base.hashCode ();
		}
	}

	[JniTypeSignature (JniTypeName, GenerateJavaPeer=false)]
	interface IAndroidInterface : IJavaPeerable {
		internal            const       string          JniTypeName    = "net/dot/jni/test/AndroidInterface";

		internal static JniPeerMembers _members = new JniPeerMembers (JniTypeName, typeof (IAndroidInterface), isInterface: true);

		public static unsafe string getClassName ()
		{
			var s = _members.StaticMethods.InvokeObjectMethod ("getClassName.()Ljava/lang/String;", null);
			return JniEnvironment.Strings.ToString (ref s, JniObjectReferenceOptions.CopyAndDispose);
		}
	}

	[JniTypeSignature (IAndroidInterface.JniTypeName, GenerateJavaPeer=false)]
	internal class IAndroidInterfaceInvoker : JavaObject, IAndroidInterface {

		public override JniPeerMembers JniPeerMembers => IAndroidInterface._members;

		public IAndroidInterfaceInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options)
			: base (ref reference, options)
		{
		}
	}
}
