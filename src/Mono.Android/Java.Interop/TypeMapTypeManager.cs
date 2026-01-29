using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using Java.Interop;

namespace Android.Runtime
{
	/// <summary>
	/// Unified JniTypeManager implementation for Android that delegates type mapping to ITypeMap.
	/// This type manager works with both TypeMapAttributeTypeMap (NativeAOT/CoreCLR) and 
	/// LlvmIrTypeMap (Mono/CoreCLR) implementations.
	/// </summary>
	class TypeMapTypeManager : JniRuntime.JniTypeManager
	{
		struct JniRemappingReplacementMethod
		{
			public string target_type;
			public string target_name;
			public bool is_static;
		};

		readonly ITypeMap _typeMap;
		static bool _loggedTypeMapV3Active;

		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
		const DynamicallyAccessedMemberTypes Methods = DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;
		const DynamicallyAccessedMemberTypes MethodsAndPrivateNested = Methods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes;

		public TypeMapTypeManager (ITypeMap typeMap)
		{
			_typeMap = typeMap ?? throw new ArgumentNullException (nameof (typeMap));
			
			// Log once that TypeMap v3 is active
			if (!_loggedTypeMapV3Active) {
				_loggedTypeMapV3Active = true;
				Logger.Log (LogLevel.Info, "monodroid-typemap", 
					"*** TYPEMAP V3 ACTIVE *** TypeMapTypeManager initialized with " + typeMap.GetType ().Name);
			}
		}

		/// <summary>
		/// Override GetTypes to throw when array types are requested.
		/// TypeMap v3 does not support runtime type construction.
		/// </summary>
		public override IEnumerable<Type> GetTypes (JniTypeSignature typeSignature)
		{
			// TypeMap v3: Array types must be pre-registered, runtime construction is not supported
			if (typeSignature.ArrayRank > 0) {
				throw new NotSupportedException (
					$"Array type lookup is not supported with TypeMap v3. " +
					$"Type '{typeSignature.Name}' (rank={typeSignature.ArrayRank}) must be pre-registered.");
			}

			return base.GetTypes (typeSignature);
		}

		protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
		{
			if (_typeMap.TryGetTypesForJniName (jniSimpleReference, out IEnumerable<Type>? types)) {
				foreach (var type in types) {
					yield return type;
				}
			} else {
				if (Logger.LogAssembly) {
					// Miss message is logged in the native runtime
					JNIEnv.LogTypemapTrace (new System.Diagnostics.StackTrace (true));
				}
			}

			foreach (var ti in base.GetTypesForSimpleReference (jniSimpleReference))
				yield return ti;
		}

		protected override string? GetSimpleReference (Type type)
		{
			if (_typeMap.TryGetJniNameForType (type, out string? jniName)) {
				return GetReplacementTypeCore (jniName) ?? jniName;
			}
			return null;
		}

		protected override IEnumerable<string> GetSimpleReferences (Type type)
		{
			var references = _typeMap.GetJniNamesForType (type);
			foreach (var reference in references) {
				var replacement = GetReplacementTypeCore (reference);
				yield return replacement ?? reference;
			}
		}

		protected override IReadOnlyList<string>? GetStaticMethodFallbackTypesCore (string jniSimpleReference)
		{
			ReadOnlySpan<char> name = jniSimpleReference;
			int slash = name.LastIndexOf ('/');
			var desugarType = new StringBuilder (jniSimpleReference.Length + "Desugar".Length);
			if (slash > 0) {
				desugarType.Append (name.Slice (0, slash + 1))
					.Append ("Desugar")
					.Append (name.Slice (slash + 1));
			} else {
				desugarType.Append ("Desugar").Append (name);
			}

			var typeWithPrefix = desugarType.ToString ();
			var typeWithSuffix = $"{jniSimpleReference}$-CC";

			var replacements = new []{
				GetReplacementTypeCore (typeWithPrefix) ?? typeWithPrefix,
				GetReplacementTypeCore (typeWithSuffix) ?? typeWithSuffix,
			};

			if (Logger.LogAssembly) {
				var message = $"Remapping type `{jniSimpleReference}` to one one of {{ `{replacements [0]}`, `{replacements [1]}` }}";
				Logger.Log (LogLevel.Debug, "monodroid-assembly", message);
			}
			return replacements;
		}

		protected override string? GetReplacementTypeCore (string jniSimpleReference)
		{
			if (!JNIEnvInit.jniRemappingInUse) {
				return null;
			}

			IntPtr ret = RuntimeNativeMethods._monodroid_lookup_replacement_type (jniSimpleReference);
			if (ret == IntPtr.Zero) {
				return null;
			}

			return Marshal.PtrToStringAnsi (ret);
		}

		protected override JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfoCore (string jniSourceType, string jniMethodName, string jniMethodSignature)
		{
			if (!JNIEnvInit.jniRemappingInUse) {
				return null;
			}

			IntPtr retInfo = RuntimeNativeMethods._monodroid_lookup_replacement_method_info (jniSourceType, jniMethodName, jniMethodSignature);
			if (retInfo == IntPtr.Zero) {
				return null;
			}

			var method = new JniRemappingReplacementMethod ();
			method = Marshal.PtrToStructure<JniRemappingReplacementMethod> (retInfo);
			var newSignature = jniMethodSignature;

			int? paramCount = null;
			if (method.is_static) {
				paramCount = JniMemberSignature.GetParameterCountFromMethodSignature (jniMethodSignature) + 1;
				newSignature = $"(L{jniSourceType};" + jniMethodSignature.Substring ("(".Length);
			}

			if (Logger.LogAssembly) {
				var message = $"Remapping method `{jniSourceType}.{jniMethodName}{jniMethodSignature}` to " +
					$"`{method.target_type}.{method.target_name}{newSignature}`; " +
					$"param-count: {paramCount}; instance-to-static? {method.is_static}";
				Logger.Log (LogLevel.Debug, "monodroid-assembly", message);
			}

			return new JniRuntime.ReplacementMethodInfo {
				SourceJniType = jniSourceType,
				SourceJniMethodName = jniMethodName,
				SourceJniMethodSignature = jniMethodSignature,
				TargetJniType = method.target_type,
				TargetJniMethodName = method.target_name,
				TargetJniMethodSignature = newSignature,
				TargetJniMethodParameterCount = paramCount,
				TargetJniMethodInstanceToStatic = method.is_static,
			};
		}

		[return: DynamicallyAccessedMembers (Constructors)]
		protected override Type? GetInvokerTypeCore (
			[DynamicallyAccessedMembers (Constructors)]
			Type type)
		{
			throw new NotSupportedException ();
		}

		[Obsolete ("Use RegisterNativeMembers(JniType, Type, ReadOnlySpan<char>) instead.")]
		public override void RegisterNativeMembers (
				JniType nativeClass,
				[DynamicallyAccessedMembers (MethodsAndPrivateNested)]
				Type type,
				string? methods)
		{
			throw new NotSupportedException ();
		}

		public override void RegisterNativeMembers (
				JniType nativeClass,
				[DynamicallyAccessedMembers (MethodsAndPrivateNested)] Type type,
				ReadOnlySpan<char> methods)
		{
			throw new NotSupportedException ();
		}
	}
}
