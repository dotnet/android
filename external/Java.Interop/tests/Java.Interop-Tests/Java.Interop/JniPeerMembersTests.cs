using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

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
		public void PeerMemberCachesAreInitiallyNull ()
		{
			var members = new JniPeerMembers (CallNonvirtualBase.JniTypeName, typeof (CallNonvirtualBase));
			try {
				Assert.IsNull (GetInstanceFields (members.InstanceFields));
				Assert.IsNull (GetInstanceMethods (members.InstanceMethods));
				Assert.IsNull (GetSubclassConstructors (members.InstanceMethods));
				Assert.IsNull (GetStaticFields (members.StaticFields));
				Assert.IsNull (GetStaticMethods (members.StaticMethods));
			} finally {
				JniPeerMembers.Dispose (members);
			}
		}

		[Test]
		public void ConstructorTypeCacheIsAllocatedOnlyForManagedSubclasses ()
		{
			var members = new JniPeerMembers (CallNonvirtualBase.JniTypeName, typeof (CallNonvirtualBase));
			try {
				var methods = members.InstanceMethods;

				Assert.AreSame (methods, methods.GetConstructorsForType (typeof (CallNonvirtualBase)));
				Assert.IsNull (GetSubclassConstructors (methods));

				var derivedMethods = methods.GetConstructorsForType (typeof (CallNonvirtualDerived));
				var constructors = GetSubclassConstructors (methods);
				Assert.AreEqual (1, constructors.Count);
				Assert.AreSame (derivedMethods, constructors [typeof (CallNonvirtualDerived)]);

				methods.Dispose ();
				Assert.IsNull (GetSubclassConstructors (methods));
				Assert.Throws<InvalidOperationException> (() => {
					var type = derivedMethods.JniPeerType;
				});
			} finally {
				JniPeerMembers.Dispose (members);
			}
		}

		[Test]
		public void ConcurrentFirstUsePublishesSingleFieldAndStaticMethodCaches ()
		{
			var instanceMembers = new JniPeerMembers (CallNonvirtualBase.JniTypeName, typeof (CallNonvirtualBase));
			try {
				var instanceFields = new JniFieldInfo [16];
				Assert.IsNull (GetInstanceFields (instanceMembers.InstanceFields));
				Parallel.For (0, instanceFields.Length, i => instanceFields [i] = instanceMembers.InstanceFields.GetFieldInfo ("methodInvoked.Z"));
				AssertSingleCachedValue (GetInstanceFields (instanceMembers.InstanceFields), "methodInvoked.Z", instanceFields);
			} finally {
				JniPeerMembers.Dispose (instanceMembers);
			}

			var staticMembers = new JniPeerMembers (JavaLangSystemTestObject.JniTypeName, typeof (JavaLangSystemTestObject));
			try {
				var staticFields = new JniFieldInfo [16];
				Assert.IsNull (GetStaticFields (staticMembers.StaticFields));
				Parallel.For (0, staticFields.Length, i => staticFields [i] = staticMembers.StaticFields.GetFieldInfo ("in.Ljava/io/InputStream;"));
				AssertSingleCachedValue (GetStaticFields (staticMembers.StaticFields), "in.Ljava/io/InputStream;", staticFields);

				var staticMethods = new JniMethodInfo [16];
				Assert.IsNull (GetStaticMethods (staticMembers.StaticMethods));
				Parallel.For (0, staticMethods.Length, i => staticMethods [i] = staticMembers.StaticMethods.GetMethodInfo ("currentTimeMillis.()J"));
				AssertSingleCachedValue (GetStaticMethods (staticMembers.StaticMethods), "currentTimeMillis.()J", staticMethods);
			} finally {
				JniPeerMembers.Dispose (staticMembers);
			}
		}

		static void AssertSingleCachedValue<T> (ConcurrentDictionary<string, T> cache, string key, T [] values)
			where T : class
		{
			Assert.AreEqual (1, cache.Count);
			foreach (var value in values)
				Assert.AreSame (values [0], value);
			Assert.AreSame (cache [key], values [0]);
		}

		static ConcurrentDictionary<string, JniFieldInfo> GetInstanceFields (JniPeerMembers.JniInstanceFields fields)
		{
			var field = typeof (JniPeerMembers.JniInstanceFields).GetField ("instanceFields", BindingFlags.NonPublic | BindingFlags.Instance);
			return GetCache<string, JniFieldInfo> (field, fields);
		}

		static ConcurrentDictionary<string, JniMethodInfo> GetInstanceMethods (JniPeerMembers.JniInstanceMethods methods)
		{
			var field = typeof (JniPeerMembers.JniInstanceMethods).GetField ("instanceMethods", BindingFlags.NonPublic | BindingFlags.Instance);
			return GetCache<string, JniMethodInfo> (field, methods);
		}

		static ConcurrentDictionary<Type, JniPeerMembers.JniInstanceMethods> GetSubclassConstructors (JniPeerMembers.JniInstanceMethods methods)
		{
			var field = typeof (JniPeerMembers.JniInstanceMethods).GetField ("subclassConstructors", BindingFlags.NonPublic | BindingFlags.Instance);
			return GetCache<Type, JniPeerMembers.JniInstanceMethods> (field, methods);
		}

		static ConcurrentDictionary<string, JniFieldInfo> GetStaticFields (JniPeerMembers.JniStaticFields fields)
		{
			var field = typeof (JniPeerMembers.JniStaticFields).GetField ("staticFields", BindingFlags.NonPublic | BindingFlags.Instance);
			return GetCache<string, JniFieldInfo> (field, fields);
		}

		static ConcurrentDictionary<string, JniMethodInfo> GetStaticMethods (JniPeerMembers.JniStaticMethods methods)
		{
			var field = typeof (JniPeerMembers.JniStaticMethods).GetField ("staticMethods", BindingFlags.NonPublic | BindingFlags.Instance);
			return GetCache<string, JniMethodInfo> (field, methods);
		}

		static ConcurrentDictionary<TKey, TValue> GetCache<TKey, TValue> (FieldInfo field, object owner)
		{
			return (ConcurrentDictionary<TKey, TValue>) field.GetValue (owner);
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
			}
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
		public void ReplaceInstanceMethodWithUtf8Signature ()
		{
			using var o = new JavaLangRemappingTestObject ();
			// Shouldn't throw; should instead invoke Object.toString()
			var r = o.remappedToStringWithUtf8Signature ();
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

		[Test]
		public void DesugarInterfaceStaticMethod ()
		{
			var s = IAndroidInterface.getClassName ();
			Assert.AreEqual ("DesugarAndroidInterface$-CC", s);
		}
	}

	[JniTypeSignature (JniTypeName, GenerateJavaPeer=false)]
	abstract class JavaLangSystemTestObject : JavaObject {
		internal const string JniTypeName = "java/lang/System";
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
			const string id = "doesNotExist.()V";
			_members.InstanceMethods.InvokeNonvirtualVoidMethod (id, this, null);
		}

		public unsafe JniObjectReference remappedToToString ()
		{
			const string id = "remappedToToString.()Ljava/lang/String;";
			return _members.InstanceMethods.InvokeNonvirtualObjectMethod (id, this, null);
		}

		public unsafe JniObjectReference remappedToStringWithUtf8Signature ()
		{
			const string id = "remappedToStringWithUtf8Signature.()Ljava/lang/String;";
			return _members.InstanceMethods.InvokeNonvirtualObjectMethod (id, this, null);
		}

		public unsafe int remappedToStaticHashCode ()
		{
			const string id = "remappedToStaticHashCode.()I";
			return _members.InstanceMethods.InvokeVirtualInt32Method (id, this, null);
		}
	}

	[JniTypeSignature (JavaLangRemappingTestRuntime.JniTypeName, GenerateJavaPeer=false)]
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
