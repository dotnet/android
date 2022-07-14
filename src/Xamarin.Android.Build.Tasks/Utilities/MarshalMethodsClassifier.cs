using System;
using System.Collections.Generic;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace Xamarin.Android.Tasks
{
#if ENABLE_MARSHAL_METHODS
	public sealed class MarshalMethodEntry
	{
		public TypeDefinition DeclaringType       { get; }
		public MethodDefinition NativeCallback    { get; }
		public MethodDefinition Connector         { get; }
		public MethodDefinition RegisteredMethod  { get; }
		public MethodDefinition ImplementedMethod { get; }
		public FieldDefinition CallbackField      { get; }
		public string JniTypeName                 { get; }
		public string JniMethodName               { get; }
		public string JniMethodSignature          { get; }

		public MarshalMethodEntry (TypeDefinition topType, MethodDefinition nativeCallback, MethodDefinition connector, MethodDefinition
				registeredMethod, MethodDefinition implementedMethod, FieldDefinition callbackField, string jniTypeName,
				string jniName, string jniSignature)
		{
			DeclaringType = topType ?? throw new ArgumentNullException (nameof (topType));
			NativeCallback = nativeCallback ?? throw new ArgumentNullException (nameof (nativeCallback));
			Connector = connector ?? throw new ArgumentNullException (nameof (connector));
			RegisteredMethod = registeredMethod ?? throw new ArgumentNullException (nameof (registeredMethod));
			ImplementedMethod = implementedMethod ?? throw new ArgumentNullException (nameof (implementedMethod));
			CallbackField = callbackField; // we don't require the callback field to exist
			JniTypeName = EnsureNonEmpty (jniTypeName, nameof (jniTypeName));
			JniMethodName = EnsureNonEmpty (jniName, nameof (jniName));
			JniMethodSignature = EnsureNonEmpty (jniSignature, nameof (jniSignature));
		}

		string EnsureNonEmpty (string s, string argName)
		{
			if (String.IsNullOrEmpty (s)) {
				throw new ArgumentException ("must not be null or empty", argName);
			}

			return s;
		}
	}
#endif

	class MarshalMethodsClassifier : JavaCallableMethodClassifier
	{
#if ENABLE_MARSHAL_METHODS
		sealed class ConnectorInfo
		{
			public string MethodName                  { get; }
			public string TypeName                    { get; }
			public AssemblyNameReference AssemblyName { get; }

			public ConnectorInfo (string spec)
			{
				string[] connectorSpec = spec.Split (':');
				MethodName = connectorSpec[0];

				if (connectorSpec.Length < 2) {
					return;
				}

				string fullTypeName = connectorSpec[1];
				int comma = fullTypeName.IndexOf (',');
				TypeName = fullTypeName.Substring (0, comma);
				AssemblyName = AssemblyNameReference.Parse (fullTypeName.Substring (comma + 1).Trim ());
			}
		}

		interface IMethodSignatureMatcher
		{
			bool Matches (MethodDefinition method);
		}

		sealed class NativeCallbackSignature : IMethodSignatureMatcher
		{
			static readonly HashSet<string> verbatimTypes = new HashSet<string> (StringComparer.Ordinal) {
				"System.Boolean",
				"System.Byte",
				"System.Char",
				"System.Double",
				"System.Int16",
				"System.Int32",
				"System.Int64",
				"System.IntPtr",
				"System.SByte",
				"System.Single",
				"System.UInt16",
				"System.UInt32",
				"System.UInt64",
				"System.Void",
			};

			readonly List<string> paramTypes;
			readonly string returnType;
			readonly TaskLoggingHelper log;

			public NativeCallbackSignature (MethodDefinition target, TaskLoggingHelper log)
			{
				this.log = log;
				returnType = MapType (target.ReturnType.FullName);
				paramTypes = new List<string> {
					"System.IntPtr", // jnienv
					"System.IntPtr", // native__this
				};

				foreach (ParameterDefinition pd in target.Parameters) {
					paramTypes.Add (MapType (pd.ParameterType.FullName));
				}
			}

			string MapType (string type)
			{
				if (verbatimTypes.Contains (type)) {
					return type;
				}

				return "System.IntPtr";
			}

			public bool Matches (MethodDefinition method)
			{
				if (method.Parameters.Count != paramTypes.Count || !method.IsStatic) {
					log.LogDebugMessage ($"Method '{method.FullName}' doesn't match native callback signature (invalid parameter count or not static)");
					return false;
				}

				if (String.Compare (returnType, method.ReturnType.FullName, StringComparison.Ordinal) != 0) {
					log.LogDebugMessage ($"Method '{method.FullName}' doesn't match native callback signature (invalid return type)");
					return false;
				}

				for (int i = 0; i < method.Parameters.Count; i++) {
					ParameterDefinition pd = method.Parameters[i];
					if (String.Compare (pd.ParameterType.FullName, paramTypes[i], StringComparison.Ordinal) != 0) {
						log.LogDebugMessage ($"Method '{method.FullName}' doesn't match native callback signature, expected parameter type '{paramTypes[i]}' at position {i}, found '{pd.ParameterType.FullName}'");
						return false;
					}
				}

				return true;
			}
		}

		TypeDefinitionCache tdCache;
		DirectoryAssemblyResolver resolver;
		Dictionary<string, IList<MarshalMethodEntry>> marshalMethods;
		HashSet<AssemblyDefinition> assemblies;
		TaskLoggingHelper log;
		bool haveDynamicMethods;

		public IDictionary<string, IList<MarshalMethodEntry>> MarshalMethods => marshalMethods;
		public ICollection<AssemblyDefinition> Assemblies => assemblies;
		public bool FoundDynamicallyRegisteredMethods => haveDynamicMethods;
#endif

		public MarshalMethodsClassifier (TypeDefinitionCache tdCache, DirectoryAssemblyResolver res, TaskLoggingHelper log)
		{
#if ENABLE_MARSHAL_METHODS
			this.log = log ?? throw new ArgumentNullException (nameof (log));
			this.tdCache = tdCache ?? throw new ArgumentNullException (nameof (tdCache));
			resolver = res ?? throw new ArgumentNullException (nameof (tdCache));
			marshalMethods = new Dictionary<string, IList<MarshalMethodEntry>> (StringComparer.Ordinal);
			assemblies = new HashSet<AssemblyDefinition> ();
#endif
		}

		public override bool ShouldBeDynamicallyRegistered (TypeDefinition topType, MethodDefinition registeredMethod, MethodDefinition implementedMethod, CustomAttribute registerAttribute)
		{
#if ENABLE_MARSHAL_METHODS
			if (registeredMethod == null) {
				throw new ArgumentNullException (nameof (registeredMethod));
			}

			if (implementedMethod == null) {
				throw new ArgumentNullException (nameof (registeredMethod));
			}

			if (registerAttribute == null) {
				throw new ArgumentNullException (nameof (registerAttribute));
			}

			if (!IsDynamicallyRegistered (topType, registeredMethod, implementedMethod, registerAttribute)) {
				return false;
			}

			haveDynamicMethods = true;
#endif // def ENABLE_MARSHAL_METHODS
			return true;
		}

#if ENABLE_MARSHAL_METHODS
		bool IsDynamicallyRegistered (TypeDefinition topType, MethodDefinition registeredMethod, MethodDefinition implementedMethod, CustomAttribute registerAttribute)
		{
			Console.WriteLine ($"Classifying:\n\tmethod: {implementedMethod.FullName}\n\tregistered method: {registeredMethod.FullName})\n\tAttr: {registerAttribute.AttributeType.FullName} (parameter count: {registerAttribute.ConstructorArguments.Count})");
			Console.WriteLine ($"\tTop type: {topType.FullName}\n\tManaged type: {registeredMethod.DeclaringType.FullName}, {registeredMethod.DeclaringType.GetPartialAssemblyName (tdCache)}");
			if (registerAttribute.ConstructorArguments.Count != 3) {
				log.LogWarning ($"Method '{registeredMethod.FullName}' will be registered dynamically, not enough arguments to the [Register] attribute to generate marshal method.");
				return true;
			}

			var connector = new ConnectorInfo ((string)registerAttribute.ConstructorArguments[2].Value);

			Console.WriteLine ($"\tconnector: {connector.MethodName} (from spec: '{(string)registerAttribute.ConstructorArguments[2].Value}')");

			if (IsStandardHandler (topType, connector, registeredMethod, implementedMethod, jniName: (string)registerAttribute.ConstructorArguments[0].Value, jniSignature: (string)registerAttribute.ConstructorArguments[1].Value)) {
				return false;
			}

			log.LogWarning ($"Method '{registeredMethod.FullName}' will be registered dynamically");
			return true;
		}

		// TODO: Probably should check if all the methods and fields are private and static - only then it is safe(ish) to remove them
		bool IsStandardHandler (TypeDefinition topType, ConnectorInfo connector, MethodDefinition registeredMethod, MethodDefinition implementedMethod, string jniName, string jniSignature)
		{
			const string HandlerNameStart = "Get";
			const string HandlerNameEnd = "Handler";

			string connectorName = connector.MethodName;
			if (connectorName.Length < HandlerNameStart.Length + HandlerNameEnd.Length + 1 ||
			    !connectorName.StartsWith (HandlerNameStart, StringComparison.Ordinal) ||
			    !connectorName.EndsWith (HandlerNameEnd, StringComparison.Ordinal)) {
				log.LogWarning ($"\tConnector name '{connectorName}' must start with '{HandlerNameStart}', end with '{HandlerNameEnd}' and have at least one character between the two parts.");
				return false;
			}

			string callbackNameCore = connectorName.Substring (HandlerNameStart.Length, connectorName.Length - HandlerNameStart.Length - HandlerNameEnd.Length);
			string nativeCallbackName = $"n_{callbackNameCore}";
			string delegateFieldName = $"cb_{Char.ToLowerInvariant (callbackNameCore[0])}{callbackNameCore.Substring (1)}";

			TypeDefinition connectorDeclaringType = connector.AssemblyName == null ? registeredMethod.DeclaringType : FindType (resolver.Resolve (connector.AssemblyName), connector.TypeName);
			Console.WriteLine ($"\tconnector name: {connectorName}\n\tnative callback name: {nativeCallbackName}\n\tdelegate field name: {delegateFieldName}");

			MethodDefinition connectorMethod = FindMethod (connectorDeclaringType, connectorName);
			if (connectorMethod == null) {
				log.LogWarning ($"\tConnector method '{connectorName}' not found in type '{connectorDeclaringType.FullName}'");
				return false;
			}

			if (String.Compare ("System.Delegate", connectorMethod.ReturnType.FullName, StringComparison.Ordinal) != 0) {
				log.LogWarning ($"\tConnector '{connectorName}' in type '{connectorDeclaringType.FullName}' has invalid return type, expected 'System.Delegate', found '{connectorMethod.ReturnType.FullName}'");
				return false;
			}

			var ncbs = new NativeCallbackSignature (registeredMethod, log);
			MethodDefinition nativeCallbackMethod = FindMethod (connectorDeclaringType, nativeCallbackName, ncbs);
			if (nativeCallbackMethod == null) {
				log.LogWarning ($"\tUnable to find native callback method matching the '{registeredMethod.FullName}' signature");
				return false;
			}

			// In the standard handler "pattern", the native callback backing field is private, static and thus in the same type
			// as the native callback.
			FieldDefinition delegateField = FindField (nativeCallbackMethod.DeclaringType, delegateFieldName);
			if (delegateField != null) {
				if (String.Compare ("System.Delegate", delegateField.FieldType.FullName, StringComparison.Ordinal) != 0) {
					log.LogWarning ($"\tdelegate field '{delegateFieldName}' in type '{nativeCallbackMethod.DeclaringType.FullName}' has invalid type, expected 'System.Delegate', found '{delegateField.FieldType.FullName}'");
					return false;
				}
			}

			Console.WriteLine ($"##G1: {implementedMethod.DeclaringType.FullName} -> {JavaNativeTypeManager.ToJniName (implementedMethod.DeclaringType, tdCache)}");
			Console.WriteLine ($"##G1: top type: {topType.FullName} -> {JavaNativeTypeManager.ToJniName (topType, tdCache)}");

			StoreMethod (
				connectorName,
				registeredMethod,
				new MarshalMethodEntry (
					topType,
					nativeCallbackMethod,
					connectorMethod,
					registeredMethod,
					implementedMethod,
					delegateField,
					JavaNativeTypeManager.ToJniName (topType, tdCache),
					jniName,
					jniSignature)
			);

			StoreAssembly (connectorMethod.Module.Assembly);
			StoreAssembly (nativeCallbackMethod.Module.Assembly);
			if (delegateField != null) {
				StoreAssembly (delegateField.Module.Assembly);
			}

			return true;
		}

		TypeDefinition FindType (AssemblyDefinition asm, string typeName)
		{
			foreach (ModuleDefinition md in asm.Modules) {
				foreach (TypeDefinition td in md.Types) {
					TypeDefinition match = GetMatchingType (td);
					if (match != null) {
						return match;
					}
				}
			}

			return null;

			TypeDefinition GetMatchingType (TypeDefinition def)
			{
				if (String.Compare (def.FullName, typeName, StringComparison.Ordinal) == 0) {
					return def;
				}

				if (!def.HasNestedTypes) {
					return null;
				}

				TypeDefinition ret;
				foreach (TypeDefinition nested in def.NestedTypes) {
					ret = GetMatchingType (nested);
					if (ret != null) {
						return ret;
					}
				}

				return null;
			}
		}

		MethodDefinition FindMethod (TypeDefinition type, string methodName, IMethodSignatureMatcher signatureMatcher = null)
		{
			foreach (MethodDefinition method in type.Methods) {
				if (!method.IsManaged || method.IsConstructor) {
					continue;
				}

				if (String.Compare (methodName, method.Name, StringComparison.Ordinal) != 0) {
					continue;
				}

				if (signatureMatcher == null || signatureMatcher.Matches (method)) {
					return method;
				}
			}

			if (type.BaseType == null) {
				return null;
			}

			return FindMethod (tdCache.Resolve (type.BaseType), methodName, signatureMatcher);
		}

		FieldDefinition FindField (TypeDefinition type, string fieldName, bool lookForInherited = false)
		{
			foreach (FieldDefinition field in type.Fields) {
				if (String.Compare (field.Name, fieldName, StringComparison.Ordinal) == 0) {
					return field;
				}
			}

			if (!lookForInherited || type.BaseType == null) {
				return null;
			}

			return FindField (tdCache.Resolve (type.BaseType), fieldName, lookForInherited);
		}

		void StoreMethod (string connectorName, MethodDefinition registeredMethod, MarshalMethodEntry entry)
		{
			string typeName = registeredMethod.DeclaringType.FullName.Replace ('/', '+');
			string key = $"{typeName}, {registeredMethod.DeclaringType.GetPartialAssemblyName (tdCache)}\t{connectorName}";

			// Several classes can override the same method, we need to generate the marshal method only once
			if (marshalMethods.ContainsKey (key)) {
				return;
			}

			if (!marshalMethods.TryGetValue (key, out IList<MarshalMethodEntry> list) || list == null) {
				list = new List<MarshalMethodEntry> ();
				marshalMethods.Add (key, list);
			}
			list.Add (entry);
		}

		void StoreAssembly (AssemblyDefinition asm)
		{
			if (assemblies.Contains (asm)) {
				return;
			}

			assemblies.Add (asm);
		}
#endif
	}
}
