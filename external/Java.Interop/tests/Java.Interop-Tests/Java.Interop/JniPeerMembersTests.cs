using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

using Java.Interop;
using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniPeerMembersTests
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

		static JniType GetStaticRedirect (JniMethodInfo method)
		{
			var field = typeof (JniMethodInfo).GetField ("StaticRedirect", BindingFlags.NonPublic | BindingFlags.Instance);
			return (JniType) field.GetValue (method);
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

#if !__ANDROID__
		// These remapping tests are not supported by the Android type manager.
		[Test]
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public void ReplaceStaticFieldName ()
		{
			// Resolves `java.lang.Math.PI`, not the nonexistent `remappedToPi`.
			var info = JavaLangRemappingTestMath._members.StaticFields.GetFieldInfo ("remappedToPi.D");
			Assert.IsNotNull (info);
			Assert.IsTrue (info.IsStatic);
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public void ReplacedStaticFieldRetainsTargetOwner ()
		{
			Assert.AreEqual (global::System.Math.PI, JavaLangRemappingTestObject.remappedStaticPi ());
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public void ReplacedStaticMethodRetainsTargetOwner ()
		{
			Assert.AreEqual (5, JavaLangRemappingTestObject.remappedStaticAbs (-5));
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public void DisposeReleasesInstanceMethodStaticRedirect ()
		{
			var members = new JniPeerMembers (JavaLangRemappingTestObject.JniTypeName, typeof (JavaLangRemappingTestObject));
			var method = members.InstanceMethods.GetMethodInfo ("remappedToStaticHashCode.()I");
			var redirect = GetStaticRedirect (method);
			Assert.IsTrue (redirect.PeerReference.IsValid);

			JniPeerMembers.Dispose (members);

			Assert.IsFalse (redirect.PeerReference.IsValid);
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public void ConcurrentRemappedMethodLookupDoesNotLeakGlobalReferences ()
		{
			const int iterationCount = 20;
			const int concurrency = 16;

			RunIteration ();
			int grefsBefore = JniEnvironment.Runtime.GlobalReferenceCount;
			for (int i = 0; i < iterationCount; i++)
				RunIteration ();
			int grefsAfter = JniEnvironment.Runtime.GlobalReferenceCount;

			Assert.AreEqual (grefsBefore, grefsAfter);

			static void RunIteration ()
			{
				var members = new JniPeerMembers (JavaLangRemappingTestObject.JniTypeName, typeof (JavaLangRemappingTestObject));
				try {
					Parallel.For (
						0,
						concurrency,
						_ => members.InstanceMethods.GetMethodInfo ("remappedToStaticHashCode.()I"));
				} finally {
					JniPeerMembers.Dispose (members);
				}
			}
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public void ReplaceInstanceFieldName ()
		{
			// Resolves `java.io.ByteArrayInputStream.pos`, not the nonexistent `remappedToPos`.
			var info = JavaIoRemappingTestStream._members.InstanceFields.GetFieldInfo ("remappedToPos.I");
			Assert.IsNotNull (info);
			Assert.IsFalse (info.IsStatic);
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public void DeclaredInstanceFieldHidesBaseFieldRemap ()
		{
			var members = new JniPeerMembers (FieldRemapDerived.JniTypeName, typeof (FieldRemapDerived));
			try {
				using var type = new JniType (FieldRemapDerived.JniTypeName);
				var expected = type.GetInstanceField ("hiddenInstanceField", "Z");
				var remapped = type.GetInstanceField ("remappedInstanceField", "Z");
				var actual = members.InstanceFields.GetFieldInfo ("hiddenInstanceField.Z");

				Assert.AreEqual (expected.ID, actual.ID);
				Assert.AreNotEqual (remapped.ID, actual.ID);
			} finally {
				JniPeerMembers.Dispose (members);
			}
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public void DeclaredStaticFieldHidesBaseFieldRemap ()
		{
			var members = new JniPeerMembers (FieldRemapDerived.JniTypeName, typeof (FieldRemapDerived));
			try {
				using var type = new JniType (FieldRemapDerived.JniTypeName);
				var expected = type.GetStaticField ("hiddenStaticField", "Ljava/lang/String;");
				var remapped = type.GetStaticField ("remappedStaticField", "Ljava/lang/String;");
				var actual = members.StaticFields.GetFieldInfo ("hiddenStaticField.Ljava/lang/String;");

				Assert.AreEqual (expected.ID, actual.ID);
				Assert.AreNotEqual (remapped.ID, actual.ID);
			} finally {
				JniPeerMembers.Dispose (members);
			}
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public void FailedCurrentInstanceFieldRemapFallsBackToBaseRemap ()
		{
			var members = new JniPeerMembers (FieldRemapDerived.JniTypeName, typeof (FieldRemapDerived));
			try {
				using var type = new JniType (FieldRemapBase.RuntimeJniTypeName);
				var expected = type.GetInstanceField ("remappedInheritedInstanceField", "Z");
				var actual = members.InstanceFields.GetFieldInfo ("inheritedInstanceField.Z");

				Assert.AreEqual (expected.ID, actual.ID);
			} finally {
				JniPeerMembers.Dispose (members);
			}
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public void FailedCurrentStaticFieldRemapFallsBackToBaseRemap ()
		{
			var members = new JniPeerMembers (FieldRemapDerived.JniTypeName, typeof (FieldRemapDerived));
			try {
				using var type = new JniType (FieldRemapBase.RuntimeJniTypeName);
				var expected = type.GetStaticField ("remappedInheritedStaticField", "Ljava/lang/String;");
				var actual = members.StaticFields.GetFieldInfo ("inheritedStaticField.Ljava/lang/String;");

				Assert.AreEqual (expected.ID, actual.ID);
			} finally {
				JniPeerMembers.Dispose (members);
			}
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public void DeclaredInstanceMethodHidesBaseMethodRemap ()
		{
			var members = new JniPeerMembers (FieldRemapDerived.JniTypeName, typeof (FieldRemapDerived));
			try {
				using var type = new JniType (FieldRemapDerived.JniTypeName);
				var expected = type.GetInstanceMethod ("hiddenInstanceMethod", "()I");
				var remapped = type.GetInstanceMethod ("remappedInstanceMethod", "()I");
				var actual = members.InstanceMethods.GetMethodInfo ("hiddenInstanceMethod.()I");

				Assert.AreEqual (expected.ID, actual.ID);
				Assert.AreNotEqual (remapped.ID, actual.ID);
			} finally {
				JniPeerMembers.Dispose (members);
			}
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public void DeclaredStaticMethodHidesBaseMethodRemap ()
		{
			var members = new JniPeerMembers (FieldRemapDerived.JniTypeName, typeof (FieldRemapDerived));
			try {
				using var type = new JniType (FieldRemapDerived.JniTypeName);
				var expected = type.GetStaticMethod ("hiddenStaticMethod", "()I");
				var remapped = type.GetStaticMethod ("remappedStaticMethod", "()I");
				var actual = members.StaticMethods.GetMethodInfo ("hiddenStaticMethod.()I");

				Assert.AreEqual (expected.ID, actual.ID);
				Assert.AreNotEqual (remapped.ID, actual.ID);
			} finally {
				JniPeerMembers.Dispose (members);
			}
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public void FailedCurrentInstanceMethodRemapFallsBackToRenamedBaseRemap ()
		{
			var members = new JniPeerMembers (FieldRemapDerived.JniTypeName, typeof (FieldRemapDerived));
			try {
				using var type = new JniType (FieldRemapBase.RuntimeJniTypeName);
				var expected = type.GetInstanceMethod ("remappedInheritedInstanceMethod", "()I");
				var actual = members.InstanceMethods.GetMethodInfo ("inheritedInstanceMethod.()I");

				Assert.AreEqual (expected.ID, actual.ID);
			} finally {
				JniPeerMembers.Dispose (members);
			}
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public void FailedCurrentStaticMethodRemapFallsBackToRenamedBaseRemap ()
		{
			var members = new JniPeerMembers (FieldRemapDerived.JniTypeName, typeof (FieldRemapDerived));
			try {
				using var type = new JniType (FieldRemapBase.RuntimeJniTypeName);
				var expected = type.GetStaticMethod ("remappedInheritedStaticMethod", "()I");
				var actual = members.StaticMethods.GetMethodInfo ("inheritedStaticMethod.()I");

				Assert.AreEqual (expected.ID, actual.ID);
			} finally {
				JniPeerMembers.Dispose (members);
			}
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public unsafe void MethodRemappingPrefersSpecificSignatures ()
		{
			var members = new JniPeerMembers (FieldRemapBase.JniTypeName, typeof (FieldRemapBase));
			try {
				var intArgument = new JniArgumentValue (1);
				Assert.AreEqual (101, members.StaticMethods.InvokeInt32Method ("remappedSpecificity.(I)I", &intArgument));

				intArgument = new JniArgumentValue (2);
				members.StaticMethods.InvokeVoidMethod ("remappedSpecificity.(I)V", &intArgument);
				using var type = new JniType (FieldRemapBase.RuntimeJniTypeName);
				var valueField = type.GetStaticField ("specificityValue", "I");
				Assert.AreEqual (202, JniEnvironment.StaticFields.GetStaticIntField (type.PeerReference, valueField));

				var longArgument = new JniArgumentValue (3L);
				Assert.AreEqual (303, members.StaticMethods.InvokeInt32Method ("remappedSpecificity.(J)I", &longArgument));
			} finally {
				JniPeerMembers.Dispose (members);
			}
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public void ReplacementConstructorUsesTargetSignature ()
		{
			// The declared parameter type does not exist; the replacement pins `(I)V` instead.
			var ctor = JavaLangRemappingTestStringBuilder._members.InstanceMethods.GetConstructor ("(Lnet/dot/jni/test/RenamedInt;)V");
			Assert.IsNotNull (ctor);
		}

		[Test]
		[Category ("NativeAOTIgnore")]
		[Category ("TrimmableTypeMapUnsupported")]
		public void ReplacementMethodUsesTargetSignature ()
		{
			// The declared parameter type does not exist; the replacement pins `(Ljava/lang/String;)I` instead.
			var method = JavaLangRemappingTestStringBuilder._members.InstanceMethods.GetMethodInfo ("indexOf.(Lnet/dot/jni/test/RenamedString;)I");
			Assert.IsNotNull (method);
		}
#endif  // !__ANDROID__

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

		public static unsafe double remappedStaticPi ()
		{
			return _members.StaticFields.GetDoubleValue ("remappedStaticPi.D");
		}

		public static unsafe int remappedStaticAbs (int value)
		{
			var argument = new JniArgumentValue (value);
			return _members.StaticMethods.InvokeInt32Method ("remappedStaticAbs.(I)I", &argument);
		}
	}

	[JniTypeSignature (JniTypeName, GenerateJavaPeer=false)]
	class JavaLangRemappingTestMath : JavaObject {
		internal    const    string         JniTypeName = "java/lang/Math";
		internal    static   readonly JniPeerMembers _members = new JniPeerMembers (JniTypeName, typeof (JavaLangRemappingTestMath));
	}

	[JniTypeSignature (JniTypeName, GenerateJavaPeer=false)]
	class JavaIoRemappingTestStream : JavaObject {
		internal    const    string         JniTypeName = "java/io/ByteArrayInputStream";
		internal    static   readonly JniPeerMembers _members = new JniPeerMembers (JniTypeName, typeof (JavaIoRemappingTestStream));
	}

	[JniTypeSignature (JniTypeName, GenerateJavaPeer=false)]
	class JavaLangRemappingTestStringBuilder : JavaObject {
		internal    const    string         JniTypeName = "java/lang/StringBuilder";
		internal    static   readonly JniPeerMembers _members = new JniPeerMembers (JniTypeName, typeof (JavaLangRemappingTestStringBuilder));
	}

	[JniTypeSignature (JniTypeName, GenerateJavaPeer=false)]
	class FieldRemapBase : JavaObject {
		internal    const    string         JniTypeName = "net/dot/jni/test/FieldRemapBase";
		internal    const    string         RuntimeJniTypeName = "net/dot/jni/test/FieldRemapRenamedBase";
		internal    const    string         FinalJniTypeName = "net/dot/jni/test/FieldRemapFinalBase";
		static      readonly JniPeerMembers _members = new JniPeerMembers (JniTypeName, typeof (FieldRemapBase));

		public override JniPeerMembers JniPeerMembers => _members;
	}

	[JniTypeSignature (JniTypeName, GenerateJavaPeer=false)]
	class FieldRemapDerived : FieldRemapBase {
		internal new const string JniTypeName = "net/dot/jni/test/FieldRemapDerived";
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
