#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;

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

	class JavaVMFixtureTypeManager : JniRuntime.JniTypeManager {

		Dictionary<string, Type> TypeMappings = new() {
#if !NO_MARSHAL_MEMBER_BUILDER_SUPPORT
			[TestType.JniTypeName]              = typeof (TestType),
#endif  // !NO_MARSHAL_MEMBER_BUILDER_SUPPORT
			[GenericHolder<int>.JniTypeName]    = typeof (GenericHolder<>),
			[RenameClassBase.JniTypeName]       = typeof (RenameClassBase),
			[RenameClassDerived.JniTypeName]    = typeof (RenameClassDerived),
		};

		public JavaVMFixtureTypeManager ()
		{
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

		protected override IEnumerable<string> GetSimpleReferences (Type type)
		{
			return base.GetSimpleReferences (type)
				.Concat (CreateSimpleReferencesEnumerator (type));
		}

		IEnumerable<string> CreateSimpleReferencesEnumerator (Type type)
		{
			foreach (var e in TypeMappings) {
				if (e.Value == type) {
#if NET
					if (ReplacmentTypes.TryGetValue (e.Key, out var alt)) {
						yield return alt;
						continue;
					}
#endif  // NET
					yield return e.Key;
				}
			}
		}

#if NET
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
			["com/xamarin/interop/RenameClassBase1"] = "com/xamarin/interop/RenameClassBase2",
		};

		protected override string? GetReplacementTypeCore (string jniSimpleReference) =>
			ReplacmentTypes.TryGetValue (jniSimpleReference, out var v)
			? v
			: null;

		Dictionary<(string SourceType, string SourceName, string? SourceSignature), (string? TargetType, string? TargetName, string? TargetSignature, int? ParamCount, bool TurnStatic)> ReplacementMethods = new() {
			[("java/lang/Object",                       "remappedToToString",       "()Ljava/lang/String;")]    = (null, "toString", null, null, false),
			[("java/lang/Object",                       "remappedToStaticHashCode", null)]                      = ("com/xamarin/interop/ObjectHelper", "getHashCodeHelper", null, null, true),
			[("java/lang/Runtime",                      "remappedToGetRuntime",     null)]                      = (null, "getRuntime", null, null, false),

			// NOTE: key must use *post-renamed* value, not pre-renamed value
			// NOTE: SourceSignature lacking return type; "closer in spirit" to what `remapping-config.json` allows
			[("com/xamarin/interop/RenameClassBase2",   "hashCode",                 "()")]                      = ("com/xamarin/interop/RenameClassBase2", "myNewHashCode", null, null, false),
		};

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
			return new JniRuntime.ReplacementMethodInfo {
					SourceJniType                   = jniSourceType,
					SourceJniMethodName             = jniMethodName,
					SourceJniMethodSignature        = jniMethodSignature,
					TargetJniType                   = r.TargetType ?? jniSourceType,
					TargetJniMethodName             = r.TargetName ?? jniMethodName,
					TargetJniMethodSignature        = targetSig    ?? jniMethodSignature,
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
#endif  // NET
	}
}

