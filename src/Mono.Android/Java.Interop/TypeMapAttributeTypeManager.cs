using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Android.Runtime;
using Java.Interop.Tools.TypeNameMappings;

namespace Java.Interop
{
	class TypeMapAttributeTypeManager : JniRuntime.JniTypeManager
	{
		struct JniRemappingReplacementMethod
		{
			public string target_type;
			public string target_name;
			public bool is_static;
		};

		bool jniAddNativeMethodRegistrationAttributePresent;
		static IReadOnlyDictionary<Type, Type> ProxyTypeMap { get; } = TypeMapping.GetOrCreateProxyTypeMapping<Java.Lang.Object> ();
		static IReadOnlyDictionary<string, Type> ExternalTypeMap { get; } = TypeMapping.GetOrCreateExternalTypeMapping<Java.Lang.Object> ();

		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
		const DynamicallyAccessedMemberTypes Methods = DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;
		const DynamicallyAccessedMemberTypes MethodsAndPrivateNested = Methods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes;

		public TypeMapAttributeTypeManager (bool jniAddNativeMethodRegistrationAttributePresent)
		{
			this.jniAddNativeMethodRegistrationAttributePresent = jniAddNativeMethodRegistrationAttributePresent;
		}

		protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
		{
			if (!ExternalTypeMap.TryGetValue (jniSimpleReference, out Type? value)) {
				if (Logger.LogAssembly) {
					// Miss message is logged in the native runtime
					JNIEnv.LogTypemapTrace (new System.Diagnostics.StackTrace (true));
				}
			} else {
				yield return value;
			}

			foreach (var ti in base.GetTypesForSimpleReference (jniSimpleReference))
				yield return ti;
		}

		protected override string? GetSimpleReference (Type type)
		{
			if (!ProxyTypeMap.TryGetValue (type, out Type? proxyType))
				return null;
			if (proxyType.GetCustomAttribute<TypeMapProxyAttribute> () is { } attr)
				return GetReplacementTypeCore(attr.JniName) ?? attr.JniName;
			return null;
		}

		protected override IEnumerable<string> GetSimpleReferences (Type type)
		{
			if (GetSimpleReference (type) is { } str)
				return [str];
			return [];
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
			if (type.IsInterface || type.IsAbstract) {
				return JavaObjectExtensions.GetInvokerType (type)
					?? base.GetInvokerTypeCore (type);
			}

			return null;
		}

		delegate Delegate GetCallbackHandler ();

		static MethodInfo? dynamic_callback_gen;

		// See ExportAttribute.cs
		[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Mono.Android.Export.dll is preserved when [Export] is used via [DynamicDependency].")]
		[UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "Mono.Android.Export.dll is preserved when [Export] is used via [DynamicDependency].")]
		static Delegate CreateDynamicCallback (MethodInfo method)
		{
			if (dynamic_callback_gen == null) {
				var assembly = Assembly.Load ("Mono.Android.Export");
				if (assembly == null)
					throw new InvalidOperationException ("To use methods marked with ExportAttribute, Mono.Android.Export.dll needs to be referenced in the application");
				var type = assembly.GetType ("Java.Interop.DynamicCallbackCodeGenerator");
				if (type == null)
					throw new InvalidOperationException ("The referenced Mono.Android.Export.dll does not match the expected version. The required type was not found.");
				dynamic_callback_gen = type.GetMethod ("Create");
				if (dynamic_callback_gen == null)
					throw new InvalidOperationException ("The referenced Mono.Android.Export.dll does not match the expected version. The required method was not found.");
			}
			return (Delegate) dynamic_callback_gen.Invoke (null, new object [] { method })!;
		}

		static List<JniNativeMethodRegistration> sharedRegistrations = new List<JniNativeMethodRegistration> ();

		static bool FastRegisterNativeMembers (JniType nativeClass, Type type, ReadOnlySpan<char> methods)
		{
			if (!MagicRegistrationMap.Filled)
				return false;

			bool lockTaken = false;
			bool rv = false;

			try {
				Monitor.TryEnter (sharedRegistrations, ref lockTaken);
				List<JniNativeMethodRegistration> registrations;
				if (lockTaken) {
					sharedRegistrations.Clear ();
					registrations = sharedRegistrations;
				} else {
					registrations = new List<JniNativeMethodRegistration> ();
				}
				JniNativeMethodRegistrationArguments arguments = new JniNativeMethodRegistrationArguments (registrations, methods.ToString ());
				rv = MagicRegistrationMap.CallRegisterMethod (arguments, type.FullName!);

				if (registrations.Count > 0)
					nativeClass.RegisterNativeMethods (registrations.ToArray ());
			} finally {
				if (lockTaken) {
					Monitor.Exit (sharedRegistrations);
				}
			}

			return rv;
		}

		class MagicRegistrationMap
		{
#pragma warning disable CS0649 // Field is never assigned to;
			// assigned to in generated IL: https://github.com/xamarin/xamarin-android/blob/cbfa7e20acebd37b52ec4de9d5c1a4a66ddda799/src/Xamarin.Android.Build.Tasks/Linker/MonoDroid.Tuner/MonoDroidMarkStep.cs#L204
			static Dictionary<string, int>? typesMap;
#pragma warning restore CS0649

			static void Prefill ()
			{
				// fill code added by the linker
			}

			static MagicRegistrationMap ()
			{
				Prefill ();
			}

			static public bool Filled {
				get {
					return typesMap != null && typesMap.Count > 0;
				}
			}

			internal static bool CallRegisterMethod (JniNativeMethodRegistrationArguments arguments, string typeName)
			{
				int idx = 0;

				if (typeName == null || !(typesMap?.TryGetValue (typeName, out idx) == true))
					return false;

				return CallRegisterMethodByIndex (arguments, idx);
			}

			static bool CallRegisterMethodByIndex (JniNativeMethodRegistrationArguments arguments, int? typeIdx)
			{
				// updated by the linker to register known types
				return false;
			}
		}

		[Obsolete ("Use RegisterNativeMembers(JniType, Type, ReadOnlySpan<char>) instead.")]
		public override void RegisterNativeMembers (
				JniType nativeClass,
				[DynamicallyAccessedMembers (MethodsAndPrivateNested)]
				Type type,
				string? methods) =>
			RegisterNativeMembers (nativeClass, type, methods.AsSpan ());

		[UnconditionalSuppressMessage ("Trimming", "IL2057", Justification = "Type.GetType() can never statically know the string value parsed from parameter 'methods'.")]
		[UnconditionalSuppressMessage ("Trimming", "IL2067", Justification = "Delegate.CreateDelegate() can never statically know the string value parsed from parameter 'methods'.")]
		[UnconditionalSuppressMessage ("Trimming", "IL2072", Justification = "Delegate.CreateDelegate() can never statically know the string value parsed from parameter 'methods'.")]
		public override void RegisterNativeMembers (
				JniType nativeClass,
				[DynamicallyAccessedMembers (MethodsAndPrivateNested)] Type type,
				ReadOnlySpan<char> methods)
		{
			try {
				if (methods.IsEmpty) {
					if (jniAddNativeMethodRegistrationAttributePresent)
						base.RegisterNativeMembers (nativeClass, type, methods);
					return;
				} else if (FastRegisterNativeMembers (nativeClass, type, methods)) {
					return;
				}

				int methodCount = CountMethods (methods);
				if (methodCount < 1) {
					if (jniAddNativeMethodRegistrationAttributePresent) {
						base.RegisterNativeMembers (nativeClass, type, methods);
					}
					return;
				}

				JniNativeMethodRegistration [] natives = new JniNativeMethodRegistration [methodCount];
				int nativesIndex = 0;
				MethodInfo []? typeMethods = null;

				ReadOnlySpan<char> methodsSpan = methods;
				bool needToRegisterNatives = false;

				while (!methodsSpan.IsEmpty) {
					int newLineIndex = methodsSpan.IndexOf ('\n');

					ReadOnlySpan<char> methodLine = methodsSpan.Slice (0, newLineIndex != -1 ? newLineIndex : methodsSpan.Length);
					if (!methodLine.IsEmpty) {
						SplitMethodLine (methodLine,
							out ReadOnlySpan<char> name,
							out ReadOnlySpan<char> signature,
							out ReadOnlySpan<char> callbackString,
							out ReadOnlySpan<char> callbackDeclaringTypeString);

						Delegate? callback = null;
						if (callbackString.SequenceEqual ("__export__")) {
							var mname = name.Slice (2);
							MethodInfo? minfo = null;
							typeMethods ??= type.GetMethods (BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
							foreach (var mi in typeMethods)
								if (mname.SequenceEqual (mi.Name) && signature.SequenceEqual (JavaNativeTypeManager.GetJniSignature (mi))) {
									minfo = mi;
									break;
								}

							if (minfo == null)
								throw new InvalidOperationException (FormattableString.Invariant ($"Specified managed method '{mname.ToString ()}' was not found. Signature: {signature.ToString ()}"));
							callback = CreateDynamicCallback (minfo);
							needToRegisterNatives = true;
						} else {
							Type callbackDeclaringType = type;
							if (!callbackDeclaringTypeString.IsEmpty) {
								callbackDeclaringType = Type.GetType (callbackDeclaringTypeString.ToString (), throwOnError: true)!;
							}
							while (callbackDeclaringType.ContainsGenericParameters) {
								callbackDeclaringType = callbackDeclaringType.BaseType!;
							}

							GetCallbackHandler connector = (GetCallbackHandler) Delegate.CreateDelegate (typeof (GetCallbackHandler),
																										 callbackDeclaringType, callbackString.ToString ());
							callback = connector ();
						}

						if (callback != null) {
							needToRegisterNatives = true;
							natives [nativesIndex++] = new JniNativeMethodRegistration (name.ToString (), signature.ToString (), callback);
						}
					}

					methodsSpan = newLineIndex != -1 ? methodsSpan.Slice (newLineIndex + 1) : default;
				}

				if (needToRegisterNatives) {
					JniEnvironment.Types.RegisterNatives (nativeClass.PeerReference, natives, nativesIndex);
				}
			} catch (Exception e) {
				JniEnvironment.Runtime.RaisePendingException (e);
			}
		}

		static int CountMethods (ReadOnlySpan<char> methodsSpan)
		{
			int count = 0;
			while (!methodsSpan.IsEmpty) {
				count++;

				int newLineIndex = methodsSpan.IndexOf ('\n');
				methodsSpan = newLineIndex != -1 ? methodsSpan.Slice (newLineIndex + 1) : default;
			}
			return count;
		}

		static void SplitMethodLine (
			ReadOnlySpan<char> methodLine,
			out ReadOnlySpan<char> name,
			out ReadOnlySpan<char> signature,
			out ReadOnlySpan<char> callback,
			out ReadOnlySpan<char> callbackDeclaringType)
		{
			int colonIndex = methodLine.IndexOf (':');
			name = methodLine.Slice (0, colonIndex);
			methodLine = methodLine.Slice (colonIndex + 1);

			colonIndex = methodLine.IndexOf (':');
			signature = methodLine.Slice (0, colonIndex);
			methodLine = methodLine.Slice (colonIndex + 1);

			colonIndex = methodLine.IndexOf (':');
			callback = methodLine.Slice (0, colonIndex != -1 ? colonIndex : methodLine.Length);

			callbackDeclaringType = colonIndex != -1 ? methodLine.Slice (colonIndex + 1) : default;
		}
	}
}
