#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

using Java.Interop;

namespace Java.InteropTests {

	partial class JavaVMFixture {

		internal static TestJVM?                    VM;
		internal static JavaVMFixtureTypeManager?   TypeManager;

		static partial void CreateJavaVM ()
		{
			var o = new TestJVMOptions {
				JarFilePaths    = {
					"interop-test.jar",
				},
				TypeManager	    = new JavaVMFixtureTypeManager (),
			};
			VM          = new TestJVM (o);
			TypeManager	= (JavaVMFixtureTypeManager) VM.TypeManager;
			JniRuntime.SetCurrent (VM);
		}
	}

	[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "JavaVMFixtureTypeManager intentionally uses reflection-backed type manager behavior for tests.")]
	[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "JavaVMFixtureTypeManager intentionally uses reflection-backed type manager behavior for tests.")]
	class JavaVMFixtureTypeManager : JniRuntime.ReflectionJniTypeManager {

		[Flags]
		enum ReplacementMethodStorage {
			Strings       = 0,
			TypeUtf8      = 1,
			MethodUtf8    = 2,
			SignatureUtf8 = 4,
		}

		Dictionary<string, Type> TypeMappings = new() {
#if !NO_MARSHAL_MEMBER_BUILDER_SUPPORT
			[TestType.JniTypeName]              = typeof (TestType),
#endif  // !NO_MARSHAL_MEMBER_BUILDER_SUPPORT
			[GenericHolder<int>.JniTypeName]    = typeof (GenericHolder<>),
			[RenameClassBase.JniTypeName]       = typeof (RenameClassBase),
			[RenameClassDerived.JniTypeName]    = typeof (RenameClassDerived),
			[AnotherJavaInterfaceImpl.JniTypeName]          = typeof (AnotherJavaInterfaceImpl),
			[CallVirtualFromConstructorBase.JniTypeName]    = typeof (CallVirtualFromConstructorBase),
			[CallVirtualFromConstructorDerived.JniTypeName] = typeof (CallVirtualFromConstructorDerived),
			[CrossReferenceBridge.JniTypeName]              = typeof (CrossReferenceBridge),
			[GetThis.JniTypeName]                           = typeof (GetThis),
			[IAndroidInterface.JniTypeName]                 = typeof (IAndroidInterface),
			[IJavaInterface.JniTypeName]                    = typeof (IJavaInterface),
			[JavaDisposedObject.JniTypeName]                = typeof (JavaDisposedObject),
			[JavaObjectWithMissingJavaPeer.JniTypeName]     = typeof (JavaObjectWithMissingJavaPeer),
			[MyDisposableObject.JniTypeName]                = typeof (JavaDisposedObject),
			[MyJavaInterfaceImpl.JniTypeName]               = typeof (MyJavaInterfaceImpl),
		};
		readonly Dictionary<string, IntPtr> utf8Values = new (StringComparer.Ordinal);
		readonly object utf8ValuesLock = new ();

		public JavaVMFixtureTypeManager ()
		{
		}

		protected override void Dispose (bool disposing)
		{
			lock (utf8ValuesLock) {
				foreach (var value in utf8Values.Values)
					Marshal.ZeroFreeCoTaskMemUTF8 (value);
				utf8Values.Clear ();
			}
			base.Dispose (disposing);
		}

		protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
		{
			foreach (var t in base.GetTypesForSimpleReference (jniSimpleReference))
				yield return t;
			Type target;
#pragma warning disable CS8600	// huh?
			if (TypeMappings.TryGetValue (jniSimpleReference, out target))
				yield return target;
#pragma warning restore CS8600
		}

		protected override Type? GetTypeForSimpleReference (string jniSimpleReference)
		{
			return base.GetTypeForSimpleReference (jniSimpleReference) ??
				(TypeMappings.TryGetValue (jniSimpleReference, out var target) ? target : null);
		}

		protected override IEnumerable<string> GetSimpleReferences (Type type)
		{
			return base.GetSimpleReferences (type)
				.Concat (CreateSimpleReferencesEnumerator (type));
		}

		IEnumerable<string> CreateSimpleReferencesEnumerator (Type type)
		{
			foreach (var e in TypeMappings) {
				if (e.Value == type) {
					if (ReplacmentTypes.TryGetValue (e.Key, out var alt)) {
						yield return alt;
						continue;
					}
					yield return e.Key;
				}
			}
		}

		public string? RequestedFallbackTypesForSimpleReference;
		protected override IReadOnlyList<string>? GetStaticMethodFallbackTypesCore (string jniSimpleReference)
		{
			RequestedFallbackTypesForSimpleReference = jniSimpleReference;
			Debug.WriteLine ($"# GetStaticMethodFallbackTypes (jniSimpleReference={jniSimpleReference})");

			var slash       = jniSimpleReference.LastIndexOf ('/');
			var desugarType = slash <= 0
				? "Desugar" + jniSimpleReference
				: jniSimpleReference.Substring (0, slash+1) + "Desugar" + jniSimpleReference.Substring (slash+1);

			// These types likely won't ever exist on Desktop, but providing
			// "potentially non-existent" types ensures that we don't throw
			// from places we don't want to internally throw.
			return new[]{
				$"{desugarType}$_CC",           // For JniPeerMembersTests.DesugarInterfaceStaticMethod()
				$"{jniSimpleReference}$-CC",
			};
		}

		Dictionary<string, string> ReplacmentTypes = new() {
			["net/dot/jni/test/RenameClassBase1"] = "net/dot/jni/test/RenameClassBase2",
		};

		string? trackedReplacementType;
		int replacementTypeStringLookupCount;
		int replacementTypeUtf8LookupCount;

		public void TrackReplacementTypeLookups (string jniSimpleReference)
		{
			trackedReplacementType = jniSimpleReference;
			replacementTypeStringLookupCount = 0;
			replacementTypeUtf8LookupCount = 0;
		}

		public (int String, int Utf8) GetReplacementTypeLookupCounts ()
			=> (replacementTypeStringLookupCount, replacementTypeUtf8LookupCount);

		protected override string? GetReplacementTypeCore (string jniSimpleReference)
		{
			if (jniSimpleReference == trackedReplacementType)
				Interlocked.Increment (ref replacementTypeStringLookupCount);
			return ReplacmentTypes.TryGetValue (jniSimpleReference, out var value)
				? value
				: null;
		}

		protected override void GetReplacementTypeInfoCore (string jniSimpleReference, out string? replacement, out IntPtr replacementUtf8)
		{
			if (jniSimpleReference == trackedReplacementType)
				Interlocked.Increment (ref replacementTypeUtf8LookupCount);
			replacement = null;
			replacementUtf8 = ReplacmentTypes.TryGetValue (jniSimpleReference, out var value)
				? GetUtf8Value (value)
				: IntPtr.Zero;
		}

		Dictionary<(string SourceType, string SourceName, string? SourceSignature), (string? TargetType, string? TargetName, string? TargetSignature, int? ParamCount, bool TurnStatic, ReplacementMethodStorage Storage)> ReplacementMethods = new() {
			[("java/lang/Object",                       "remappedToToString",                  "()Ljava/lang/String;")]    = (null, "toString", null, null, false, ReplacementMethodStorage.TypeUtf8 | ReplacementMethodStorage.MethodUtf8),
			[("java/lang/Object",                       "remappedToStringWithUtf8Signature",    "()Ljava/lang/String;")]    = (null, "toString", "()Ljava/lang/String;", null, false, ReplacementMethodStorage.SignatureUtf8),
			[("java/lang/Object",                       "remappedToStaticHashCode",            null)]                      = ("net/dot/jni/test/ObjectHelper", "getHashCodeHelper", null, null, true, ReplacementMethodStorage.TypeUtf8 | ReplacementMethodStorage.MethodUtf8 | ReplacementMethodStorage.SignatureUtf8),
			[("java/lang/Runtime",                      "remappedToGetRuntime",                null)]                      = (null, "getRuntime", null, null, false, ReplacementMethodStorage.Strings),

			// NOTE: key must use *post-renamed* value, not pre-renamed value
			// NOTE: SourceSignature lacking return type; "closer in spirit" to what `remapping-config.json` allows
			[("net/dot/jni/test/RenameClassBase2",   "hashCode",                            "()")]                      = ("net/dot/jni/test/RenameClassBase2", "myNewHashCode", null, null, false, ReplacementMethodStorage.TypeUtf8 | ReplacementMethodStorage.MethodUtf8),

			// Renamed parameter types: the target descriptor is pinned explicitly, which is what
			// `target-method-signature` carries.
			[("java/lang/StringBuilder",   "<init>",   "(Lnet/dot/jni/test/RenamedInt;)V")]     = (null, "<init>", "(I)V", null, false, ReplacementMethodStorage.Strings),
			[("java/lang/StringBuilder",   "indexOf",  "(Lnet/dot/jni/test/RenamedString;)I")]  = (null, "indexOf", "(Ljava/lang/String;)I", null, false, ReplacementMethodStorage.Strings),
			[(FieldRemapBase.JniTypeName,  "hiddenInstanceMethod", "()I")] = (null, "remappedInstanceMethod", null, null, false, ReplacementMethodStorage.Strings),
			[(FieldRemapBase.JniTypeName,  "hiddenStaticMethod",   "()I")] = (null, "remappedStaticMethod", null, null, false, ReplacementMethodStorage.Strings),
			[(FieldRemapBase.JniTypeName,  "inheritedStaticMethod", "()I")] = ("net/dot/jni/test/ObjectHelper", "remappedInheritedStaticMethod", null, null, false, ReplacementMethodStorage.Strings),
			[(FieldRemapDerived.JniTypeName, "inheritedStaticMethod", "()I")] = (null, "missingStaticMethod", null, null, false, ReplacementMethodStorage.Strings),
			[(FieldRemapBase.JniTypeName,  "remappedSpecificity",  "(I)I")] = (null, "specificityExact", "(I)I", null, false, ReplacementMethodStorage.Strings),
			[(FieldRemapBase.JniTypeName,  "remappedSpecificity",  "(I)")] = (null, "specificityParameters", "(I)V", null, false, ReplacementMethodStorage.Strings),
			[(FieldRemapBase.JniTypeName,  "remappedSpecificity",  null)] = (null, "specificityWildcard", null, null, false, ReplacementMethodStorage.Strings),
		};

		Dictionary<(string SourceType, string SourceName, string? SourceSignature), (string? TargetType, string? TargetName, string? TargetSignature)> ReplacementFields = new() {
			[("java/lang/Math",                 "remappedToPi",         "D")]   = (null, "PI", null),
			[("java/io/ByteArrayInputStream",   "remappedToPos",        "I")]   = (null, "pos", null),
			[(FieldRemapBase.JniTypeName,       "hiddenInstanceField",  "Z")]   = (null, "remappedInstanceField", null),
			[(FieldRemapBase.JniTypeName,       "hiddenStaticField",    "Ljava/lang/String;")] = (null, "remappedStaticField", null),
			[(FieldRemapBase.JniTypeName,       "inheritedInstanceField", "Z")] = (null, "remappedInheritedInstanceField", null),
			[(FieldRemapBase.JniTypeName,       "inheritedStaticField", "Ljava/lang/String;")] = ("net/dot/jni/test/ObjectHelper", "remappedInheritedStaticField", null),
			[(FieldRemapDerived.JniTypeName,    "inheritedInstanceField", "Z")] = (null, "missingInstanceField", null),
			[(FieldRemapDerived.JniTypeName,    "inheritedStaticField", "Ljava/lang/String;")] = (null, "missingStaticField", null),
		};

		protected override JniRuntime.ReplacementFieldInfo? GetReplacementFieldInfoCore (string jniSourceType, string jniFieldName, string jniFieldSignature)
		{
			if (!ReplacementFields.TryGetValue ((jniSourceType, jniFieldName, jniFieldSignature), out var r) &&
					!ReplacementFields.TryGetValue ((jniSourceType, jniFieldName, null), out r)) {
				return null;
			}
			return new JniRuntime.ReplacementFieldInfo {
					SourceJniType           = jniSourceType,
					SourceJniFieldName      = jniFieldName,
					SourceJniFieldSignature = jniFieldSignature,
					TargetJniType           = r.TargetType ?? jniSourceType,
					TargetJniFieldName      = r.TargetName ?? jniFieldName,
					TargetJniFieldSignature = r.TargetSignature ?? jniFieldSignature,
			};
		}

		protected override JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfoCore (string jniSourceType, string jniMethodName, string jniMethodSignature)
		{
			// Console.Error.WriteLine ($"# jonp: looking for replacement method for (\"{jniSourceType}\", \"{jniMethodName}\", \"{jniMethodSignature}\")");
			if (!ReplacementMethods.TryGetValue ((jniSourceType, jniMethodName, jniMethodSignature), out var r) &&
					!ReplacementMethods.TryGetValue ((jniSourceType, jniMethodName, GetAlternateMethodSignature ()), out r) &&
					!ReplacementMethods.TryGetValue ((jniSourceType, jniMethodName, null), out r)) {
				return null;
			}
			var targetSig   = r.TargetSignature;
			var paramCount  = r.ParamCount;
			if (targetSig == null && r.TurnStatic) {
				targetSig   = $"(L{jniSourceType};" + jniMethodSignature.Substring ("(".Length);
				paramCount  = paramCount ?? JniMemberSignature.GetParameterCountFromMethodSignature (jniMethodSignature);
				paramCount++;
			}
			// Console.Error.WriteLine ($"# jonp: found replacement: ({GetValue (r.TargetType)}, {GetValue (r.TargetName)}, {GetValue (r.TargetSignature)}, {r.ParamCount?.ToString () ?? "null"}, {r.IsStatic})");
			var targetType = r.TargetType ?? jniSourceType;
			var targetName = r.TargetName ?? jniMethodName;
			var targetSignature = r.Storage == ReplacementMethodStorage.Strings ? targetSig ?? jniMethodSignature : targetSig;
			return new JniRuntime.ReplacementMethodInfo {
					TargetJniType                   = r.Storage.HasFlag (ReplacementMethodStorage.TypeUtf8) ? null : targetType,
					TargetJniMethodName             = r.Storage.HasFlag (ReplacementMethodStorage.MethodUtf8) ? null : targetName,
					TargetJniMethodSignature        = r.Storage.HasFlag (ReplacementMethodStorage.SignatureUtf8) ? null : targetSignature,
					TargetJniTypeUtf8               = r.Storage.HasFlag (ReplacementMethodStorage.TypeUtf8) ? GetUtf8Value (targetType) : IntPtr.Zero,
					TargetJniMethodNameUtf8         = r.Storage.HasFlag (ReplacementMethodStorage.MethodUtf8) ? GetUtf8Value (targetName) : IntPtr.Zero,
					TargetJniMethodSignatureUtf8    = r.Storage.HasFlag (ReplacementMethodStorage.SignatureUtf8) && targetSig != null ? GetUtf8Value (targetSig) : IntPtr.Zero,
					TargetJniMethodParameterCount   = paramCount,
					TargetJniMethodInstanceToStatic = r.TurnStatic,
			};

			string GetAlternateMethodSignature ()
			{
				int i = jniMethodSignature.IndexOf (')');
				return jniMethodSignature.Substring (0, i+1);
			}

			// string GetValue (string? value)
			// {
			// 	return value == null ? "null" : $"\"{value}\"";
			// }
		}

		protected override JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfoCore (IntPtr jniSourceTypeUtf8, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
		{
			var jniSourceType = Marshal.PtrToStringUTF8 (jniSourceTypeUtf8);
			if (jniSourceType == null)
				throw new InvalidOperationException ("The test remapping source type is null.");
			return GetReplacementMethodInfoCore (jniSourceType, jniMethodName.ToString (), jniMethodSignature.ToString ());
		}

		IntPtr GetUtf8Value (string value)
		{
			lock (utf8ValuesLock) {
				if (utf8Values.TryGetValue (value, out var pointer))
					return pointer;
				pointer = Marshal.StringToCoTaskMemUTF8 (value);
				utf8Values.Add (value, pointer);
				return pointer;
			}
		}
	}
}
