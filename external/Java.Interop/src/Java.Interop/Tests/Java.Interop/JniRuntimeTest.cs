using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniRuntimeTest : JavaVMFixture
	{
		[Test]
		public void CreateJavaVM ()
		{
			Assert.AreSame (JniRuntime.Current, JniRuntime.Current);
			Assert.IsTrue (JniRuntime.Current.InvocationPointer != IntPtr.Zero);
			Assert.IsTrue (JniEnvironment.EnvironmentPointer != IntPtr.Zero);
		}

#if !__ANDROID__
		[Test]
		public void JDK_OnlySupportsOneVM ()
		{
			try {
				var second = new JreRuntimeOptions ().CreateJreVM ();
				// If we reach here, we're in a JVM that supports > 1 VM
				second.Dispose ();
				Assert.Ignore ();
			} catch (NotSupportedException) {
			} catch (Exception e){
				Assert.Fail ("Expected NotSupportedException; got: {0}", e);
			}
		}
#endif  // !__ANDROID__

		[Test, ExpectedException (typeof (ArgumentNullException))]
		public void CreateJavaVMWithNullBuilder ()
		{
			new JavaVMWithNullBuilder ();
		}

		class JavaVMWithNullBuilder : JniRuntime {
			public JavaVMWithNullBuilder ()
				: base ((JniRuntime.CreationOptions) null)
			{
			}

			protected override bool TryGC (IJavaPeerable value, ref JniObjectReference handle)
			{
				throw new NotImplementedException ();
			}
		}

		[Test]
		public void GetRegisteredJavaVM_ExistingInstance ()
		{
			Assert.AreEqual (JniRuntime.Current, JniRuntime.GetRegisteredRuntime (JniRuntime.Current.InvocationPointer));
		}

		[Test]
		public void GetObject_ReturnsAlias ()
		{
			var local   = new JavaObject ();
			local.UnregisterFromRuntime ();
			Assert.IsNull (JniRuntime.Current.PeekObject (local.PeerReference));
			// GetObject must always return a value (unless handle is null, etc.).
			// However, since we called local.UnregisterFromRuntime(),
			// JniRuntime.PeekObject() is null (asserted above), but GetObject() must
			// **still** return _something_.
			// In this case, it returns an _alias_.
			// TODO: "most derived type" alias generation. (Not relevant here, but...)
			var p       = local.PeerReference;
			var alias   = JniRuntime.Current.GetObject (ref p, JniObjectReferenceOptions.CreateNewReference);
			Assert.AreNotSame (local, alias);
			alias.Dispose ();
			local.Dispose ();
		}

		[Test]
		public void GetObject_ReturnsNullWithNullHandle ()
		{
			var o = JniRuntime.Current.GetObject (IntPtr.Zero);
			Assert.IsNull (o);
		}

		[Test]
		public void GetObject_ReturnsRegisteredInstance ()
		{
			JniObjectReference lref;
			using (var o = new JavaObject ()) {
				lref = o.PeerReference.NewLocalRef ();
				Assert.AreSame (o, JniRuntime.Current.PeekObject (lref));
			}
			// At this point, the Java-side object is kept alive by `lref`,
			// but the wrapper instance has been disposed, and thus should
			// be unregistered, and thus unfindable.
			Assert.IsNull (JniRuntime.Current.PeekObject (lref));
			JniEnvironment.References.Dispose (ref lref);
		}

		[Test]
		public void GetObject_ReturnsNullWithInvalidSafeHandle ()
		{
			var invalid = new JniObjectReference ();
			Assert.IsNull (JniRuntime.Current.GetObject (ref invalid, JniObjectReferenceOptions.DisposeSourceReference));
		}

		[Test]
		public unsafe void GetObject_FindBestMatchType ()
		{
			using (var t = new JniType (TestType.JniTypeName)) {
				var c = t.GetConstructor ("()V");
				var o = t.NewObject (c, null);
				using (var w = JniRuntime.Current.GetObject (ref o, JniObjectReferenceOptions.DisposeSourceReference)) {
					Assert.AreEqual (typeof (TestType), w.GetType ());
				}
			}
		}

		[Test]
		public void GetJniMarshalInfoForType ()
		{
			// not yet implemented...
			// TODO: but should we default to using JavaProxyObject, instead of special-casing in JniMarshal?
			//       Probably not? Means that JavaVM.GetJniTypeInfoForType() subclasses can't "just" check for
			//       null values, but maybe they shouldn't anyway?
			AssertGetJniMarshalInfoForType (typeof (AppDomain));

			AssertGetJniMarshalInfoForPrimitiveType<bool> ("JniBoolean");
			AssertGetJniMarshalInfoForPrimitiveType<char> ("JniCharacter");
			AssertGetJniMarshalInfoForPrimitiveType<short> ("JniShort");
			AssertGetJniMarshalInfoForPrimitiveType<int> ("JniInteger");
			AssertGetJniMarshalInfoForPrimitiveType<long> ("JniLong");
			AssertGetJniMarshalInfoForPrimitiveType<float> ("JniFloat");
			AssertGetJniMarshalInfoForPrimitiveType<double> ("JniDouble");

			AssertGetJniMarshalInfoForType (typeof (string),
					getValue:                   "Strings.ToString",
					createLocalRef:             "Strings.NewString");

			AssertGetJniMarshalInfoForType (typeof (JavaObject),
					getValue:                   "JavaPeerableExtensions.GetValue",
					createLocalRef:             "JavaPeerableExtensions.CreateLocalRef");
			AssertGetJniMarshalInfoForType (typeof (JavaException),
					getValue:                   "JavaPeerableExtensions.GetValue",
					createLocalRef:             "JavaPeerableExtensions.CreateLocalRef");

			AssertGetJniMarshalInfoForPrimitiveArray<JavaBooleanArray, bool> ();
			AssertGetJniMarshalInfoForPrimitiveArray<JavaSByteArray, sbyte> ();
			AssertGetJniMarshalInfoForPrimitiveArray<JavaCharArray, char> ();
			AssertGetJniMarshalInfoForPrimitiveArray<JavaInt16Array, short> ();
			AssertGetJniMarshalInfoForPrimitiveArray<JavaInt32Array, int> ();
			AssertGetJniMarshalInfoForPrimitiveArray<JavaInt64Array, long> ();
			AssertGetJniMarshalInfoForPrimitiveArray<JavaSingleArray, float> ();
			AssertGetJniMarshalInfoForPrimitiveArray<JavaDoubleArray, double> ();

			AssertGetJniMarshalInfoForType (typeof (JavaObjectArray<int>),
					getValue:                   "JavaObjectArray`1.GetValue",
					createLocalRef:             "JavaObjectArray`1.CreateLocalRef",
					createMarshalCollection:    "JavaObjectArray`1.CreateMarshalCollection",
					cleanupMarshalCollection:   "JavaObjectArray`1.CleanupMarshalCollection");
			AssertGetJniMarshalInfoForType (typeof (JavaObjectArray<int[]>),
					getValue:                   "JavaObjectArray`1.GetValue",
					createLocalRef:             "JavaObjectArray`1.CreateLocalRef",
					createMarshalCollection:    "JavaObjectArray`1.CreateMarshalCollection",
					cleanupMarshalCollection:   "JavaObjectArray`1.CleanupMarshalCollection");
			AssertGetJniMarshalInfoForType (typeof (JavaObjectArray<int[][]>),
					getValue:                   "JavaObjectArray`1.GetValue",
					createLocalRef:             "JavaObjectArray`1.CreateLocalRef",
					createMarshalCollection:    "JavaObjectArray`1.CreateMarshalCollection",
					cleanupMarshalCollection:   "JavaObjectArray`1.CleanupMarshalCollection");
			AssertGetJniMarshalInfoForType (typeof (JavaObjectArray<JavaInt32Array>),
					getValue:                   "JavaObjectArray`1.GetValue",
					createLocalRef:             "JavaObjectArray`1.CreateLocalRef",
					createMarshalCollection:    "JavaObjectArray`1.CreateMarshalCollection",
					cleanupMarshalCollection:   "JavaObjectArray`1.CleanupMarshalCollection");
		}

		static void AssertGetJniMarshalInfoForType (Type type, string getValue = null, string createJValue = null, string createLocalRef = null, string createMarshalCollection = null, string cleanupMarshalCollection = null)
		{
			Action<string, Delegate, string> assertMethod = (expected, target, message) => {
				if (expected == null)
					Assert.IsNull (target, message);
				else {
					var m = target.Method;
					Assert.AreEqual (
							expected,
							string.Format ("{0}.{1}", m.DeclaringType.Name, m.Name),
							message);
				}
			};
			var info = JniRuntime.Current.GetJniMarshalInfoForType (type);
			assertMethod (getValue,                 info.GetValueFromJni,           "GetValueFromJni");
			assertMethod (createJValue,             info.CreateJValue,              "CreateJValue");
			assertMethod (createLocalRef,           info.CreateLocalRef,            "CreateLocalRef");
			assertMethod (createMarshalCollection,  info.CreateMarshalCollection,   "CreateMarshalCollection");
			assertMethod (cleanupMarshalCollection, info.CleanupMarshalCollection,  "CleanupMarshalCollection");
		}

		static void AssertGetJniMarshalInfoForPrimitiveType<T> (string type)
		{
			AssertGetJniMarshalInfoForType (typeof (T),
					getValue:       type + ".GetValueFromJni",
					createJValue:   type + ".CreateJValue",
					createLocalRef: type + ".CreateLocalRef");
			var info = JniRuntime.Current.GetJniMarshalInfoForType (typeof(T));
			info.CreateJValue (default (T));
			var lref = info.CreateLocalRef (default (T));
			Assert.AreEqual (default (T), info.GetValueFromJni (ref lref, JniObjectReferenceOptions.CreateNewReference, null));
			JniEnvironment.References.Dispose (ref lref);
		}

		static void AssertGetJniMarshalInfoForPrimitiveArray<TArray, TElement> ()
		{
			AssertGetJniMarshalInfoForType (typeof (TElement[]),
					getValue:                   typeof (TArray).Name + ".GetValueFromJni",
					createLocalRef:             typeof (TArray).Name + ".CreateLocalRef",
					createMarshalCollection:    typeof (TArray).Name + ".CreateMarshalCollection",
					cleanupMarshalCollection:   typeof (TArray).Name + ".CleanupMarshalCollection");
			AssertGetJniMarshalInfoForType (typeof (JavaArray<TElement>),
					getValue:                   typeof (TArray).Name + ".GetValueFromJni",
					createLocalRef:             typeof (TArray).Name + ".CreateLocalRef",
					createMarshalCollection:    typeof (TArray).Name + ".CreateMarshalCollection",
					cleanupMarshalCollection:   typeof (TArray).Name + ".CleanupMarshalCollection");
			AssertGetJniMarshalInfoForType (typeof (JavaPrimitiveArray<TElement>),
					getValue:                   typeof (TArray).Name + ".GetValueFromJni",
					createLocalRef:             typeof (TArray).Name + ".CreateLocalRef",
					createMarshalCollection:    typeof (TArray).Name + ".CreateMarshalCollection",
					cleanupMarshalCollection:   typeof (TArray).Name + ".CleanupMarshalCollection");
			AssertGetJniMarshalInfoForType (typeof (TArray),
					getValue:                   typeof (TArray).Name + ".GetValueFromJni",
					createLocalRef:             typeof (TArray).Name + ".CreateLocalRef",
					createMarshalCollection:    typeof (TArray).Name + ".CreateMarshalCollection",
					cleanupMarshalCollection:   typeof (TArray).Name + ".CleanupMarshalCollection");
		}
	}
}

