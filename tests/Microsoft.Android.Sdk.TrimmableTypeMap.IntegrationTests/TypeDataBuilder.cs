using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers.Adapters;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tasks;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests;

record ComponentComparisonData (
	string ManagedName,
	string? ComponentKind,
	string? ComponentName,
	IReadOnlyList<string> ComponentProperties
);

record ManifestAttributeComparisonData (
	IReadOnlyList<string> UsesPermissions,
	IReadOnlyList<string> UsesFeatures
);

/// <summary>
/// Encodes a uses-permission as "name" or "name;maxSdkVersion=N" for richer comparison.
/// </summary>
static string EncodePermission (string name, int? maxSdkVersion)
	=> maxSdkVersion.HasValue ? $"{name};maxSdkVersion={maxSdkVersion.Value}" : name;

/// <summary>
/// Encodes a uses-feature as "name;required=true/false" or "glEsVersion=0xNNNN;required=true/false".
/// </summary>
static string EncodeFeature (string? name, int glesVersion, bool required)
{
	var key = name ?? $"glEsVersion=0x{glesVersion:X8}";
	return $"{key};required={required}";
}

record TypeComparisonData (
	string ManagedName,
	string JavaName,
	string? BaseJavaName,
	IReadOnlyList<string> ImplementedInterfaces,
	bool HasActivationCtor,
	string? ActivationCtorDeclaringType,
	string? ActivationCtorStyle,
	IReadOnlyList<string> JavaConstructorSignatures,
	bool IsInterface,
	bool IsAbstract,
	bool IsGenericDefinition,
	bool DoNotGenerateAcw
);

static class TypeDataBuilder
{
	public static (Dictionary<string, TypeComparisonData> perType, List<TypeMapEntry> entries) BuildLegacy (string assemblyPath)
	{
		var cache = new TypeDefinitionCache ();
		var resolver = new DefaultAssemblyResolver ();
		resolver.AddSearchDirectory (Path.GetDirectoryName (assemblyPath)!);

		var runtimeDir = Path.GetDirectoryName (typeof (object).Assembly.Location);
		if (runtimeDir != null) {
			resolver.AddSearchDirectory (runtimeDir);
		}

		var readerParams = new ReaderParameters { AssemblyResolver = resolver };
		using var assembly = AssemblyDefinition.ReadAssembly (assemblyPath, readerParams);

		var scanner = new XAJavaTypeScanner (
			Xamarin.Android.Tools.AndroidTargetArch.Arm64,
			new TaskLoggingHelper (new MockBuildEngine (), "test"),
			cache
		);

		var javaTypes = scanner.GetJavaTypes (assembly);
		var (dataSets, _) = TypeMapCecilAdapter.GetDebugNativeEntries (
			javaTypes, cache, needUniqueAssemblies: false
		);

		var entries = dataSets.JavaToManaged
			.Select (e => new TypeMapEntry (e.JavaName, e.ManagedName, e.SkipInJavaToManaged))
			.OrderBy (e => e.JavaName, StringComparer.Ordinal)
			.ThenBy (e => e.ManagedName, StringComparer.Ordinal)
			.ToList ();

		var perType = new Dictionary<string, TypeComparisonData> (StringComparer.Ordinal);

		foreach (var typeDef in javaTypes) {
			var javaName = ScannerRunner.GetCecilJavaName (typeDef, cache);
			if (javaName == null) {
				continue;
			}

			var managedName = ScannerRunner.GetManagedName (typeDef);

			string? baseJavaName = null;
			var baseType = typeDef.GetBaseType (cache);
			if (baseType != null) {
				var baseJni = JavaNativeTypeManager.ToJniName (baseType, cache);
				if (baseJni != null && baseJni != javaName) {
					baseJavaName = baseJni;
				}
			}

			var implementedInterfaces = new List<string> ();
			if (typeDef.HasInterfaces) {
				foreach (var ifaceImpl in typeDef.Interfaces) {
					var ifaceDef = cache.Resolve (ifaceImpl.InterfaceType);
					if (ifaceDef == null) {
						continue;
					}
					var ifaceRegs = CecilExtensions.GetTypeRegistrationAttributes (ifaceDef);
					var ifaceReg = ifaceRegs.FirstOrDefault ();
					if (ifaceReg != null) {
						implementedInterfaces.Add (ifaceReg.Name.Replace ('.', '/'));
					}
				}
			}
			implementedInterfaces.Sort (StringComparer.Ordinal);

			FindLegacyActivationCtor (typeDef, cache,
				out bool hasActivationCtor, out string? activationCtorDeclaringType, out string? activationCtorStyle);

			// Use the real legacy JCW pipeline (CecilImporter.CreateType) to extract
			// Java constructors, including the base ctor chain and parameterless fallback.
			// This matches what the actual build does, unlike the previous manual [Register]
			// attribute scanning which only found directly-attributed ctors.
			var javaCtorSignatures = new List<string> ();
			if (!typeDef.IsInterface && !ScannerRunner.HasDoNotGenerateAcw (typeDef)) {
				var wrapper = CecilImporter.CreateType (typeDef, cache);
				foreach (var ctor in wrapper.Constructors) {
					if (!string.IsNullOrEmpty (ctor.JniSignature)) {
						javaCtorSignatures.Add (ctor.JniSignature);
					}
				}
			} else {
				ExtractDirectRegisterCtors (typeDef, javaCtorSignatures);
			}
			javaCtorSignatures.Sort (StringComparer.Ordinal);

			perType [managedName] = new TypeComparisonData (
				managedName,
				javaName,
				baseJavaName,
				implementedInterfaces,
				hasActivationCtor,
				activationCtorDeclaringType,
				activationCtorStyle,
				javaCtorSignatures,
				typeDef.IsInterface,
				typeDef.IsAbstract && !typeDef.IsInterface,
				typeDef.HasGenericParameters,
				GetCecilDoNotGenerateAcw (typeDef)
			);
		}

		return (perType, entries);
	}

	public static Dictionary<string, TypeComparisonData> BuildNew (string[] assemblyPaths)
	{
		var primaryAssemblyName = Path.GetFileNameWithoutExtension (assemblyPaths [0]);
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (assemblyPaths);

		var perType = new Dictionary<string, TypeComparisonData> (StringComparer.Ordinal);

		foreach (var peer in peers) {
			if (peer.AssemblyName != primaryAssemblyName) {
				continue;
			}

			var managedName = $"{peer.ManagedTypeName}, {peer.AssemblyName}";

			bool hasActivationCtor = peer.ActivationCtor != null;
			string? activationCtorDeclaringType = null;
			string? activationCtorStyle = null;
			if (peer.ActivationCtor != null) {
				activationCtorDeclaringType = $"{peer.ActivationCtor.DeclaringTypeName}, {peer.ActivationCtor.DeclaringAssemblyName}";
				activationCtorStyle = peer.ActivationCtor.Style.ToString ();
			}

			var javaCtorSignatures = peer.MarshalMethods
				.Where (m => m.IsConstructor)
				.Select (m => m.JniSignature)
				.OrderBy (s => s, StringComparer.Ordinal)
				.ToList ();

			var implementedInterfaces = peer.ImplementedInterfaceJavaNames
				.OrderBy (i => i, StringComparer.Ordinal)
				.ToList ();

			perType [managedName] = new TypeComparisonData (
				managedName,
				peer.JavaName,
				peer.BaseJavaName,
				implementedInterfaces,
				hasActivationCtor,
				activationCtorDeclaringType,
				activationCtorStyle,
				javaCtorSignatures,
				peer.IsInterface,
				peer.IsAbstract && !peer.IsInterface,
				peer.IsGenericDefinition,
				peer.DoNotGenerateAcw
			);
		}

		return perType;
	}

	static void FindLegacyActivationCtor (TypeDefinition typeDef, TypeDefinitionCache cache,
		out bool found, out string? declaringType, out string? style)
	{
		found = false;
		declaringType = null;
		style = null;

		TypeDefinition? current = typeDef;
		while (current != null) {
			foreach (var method in current.Methods) {
				if (!method.IsConstructor || method.IsStatic || method.Parameters.Count != 2) {
					continue;
				}

				var p0 = method.Parameters [0].ParameterType.FullName;
				var p1 = method.Parameters [1].ParameterType.FullName;

				if (p0 == "System.IntPtr" && p1 == "Android.Runtime.JniHandleOwnership") {
					found = true;
					declaringType = ScannerRunner.GetManagedName (current);
					style = "XamarinAndroid";
					return;
				}

				if ((p0 == "Java.Interop.JniObjectReference&" || p0 == "Java.Interop.JniObjectReference") &&
				    p1 == "Java.Interop.JniObjectReferenceOptions") {
					found = true;
					declaringType = ScannerRunner.GetManagedName (current);
					style = "JavaInterop";
					return;
				}
			}

			current = current.GetBaseType (cache);
		}
	}

	static bool GetCecilDoNotGenerateAcw (TypeDefinition typeDef)
	{
		if (!typeDef.HasCustomAttributes) {
			return false;
		}

		foreach (var attr in typeDef.CustomAttributes) {
			if (attr.AttributeType.FullName != "Android.Runtime.RegisterAttribute") {
				continue;
			}
			if (attr.HasProperties) {
				foreach (var prop in attr.Properties.Where (p => p.Name == "DoNotGenerateAcw")) {
					if (prop.Argument.Value is bool val) {
						return val;
					}
				}
			}
			return false;
		}

		return false;
	}

	static void ExtractDirectRegisterCtors (TypeDefinition typeDef, List<string> javaCtorSignatures)
	{
		foreach (var method in typeDef.Methods) {
			if (!method.IsConstructor || method.IsStatic || !method.HasCustomAttributes) {
				continue;
			}
			foreach (var attr in method.CustomAttributes) {
				if (attr.AttributeType.FullName != "Android.Runtime.RegisterAttribute") {
					continue;
				}
				if (attr.ConstructorArguments.Count >= 2) {
					var regName = (string) attr.ConstructorArguments [0].Value;
					if (regName == "<init>" || regName == ".ctor") {
						javaCtorSignatures.Add ((string) attr.ConstructorArguments [1].Value);
					}
				}
			}
		}
	}

	static readonly HashSet<string> ComponentAttributeNames = new (StringComparer.Ordinal) {
		"Android.App.ActivityAttribute",
		"Android.App.ServiceAttribute",
		"Android.Content.BroadcastReceiverAttribute",
		"Android.Content.ContentProviderAttribute",
		"Android.App.ApplicationAttribute",
		"Android.App.InstrumentationAttribute",
	};

	static string GetComponentKindFromAttributeName (string attributeFullName)
	{
		return attributeFullName switch {
			"Android.App.ActivityAttribute" => "Activity",
			"Android.App.ServiceAttribute" => "Service",
			"Android.Content.BroadcastReceiverAttribute" => "BroadcastReceiver",
			"Android.Content.ContentProviderAttribute" => "ContentProvider",
			"Android.App.ApplicationAttribute" => "Application",
			"Android.App.InstrumentationAttribute" => "Instrumentation",
			_ => attributeFullName,
		};
	}

	public static Dictionary<string, ComponentComparisonData> BuildLegacyComponentData (string assemblyPath)
	{
		var cache = new TypeDefinitionCache ();
		var resolver = new DefaultAssemblyResolver ();
		resolver.AddSearchDirectory (Path.GetDirectoryName (assemblyPath)!);

		var runtimeDir = Path.GetDirectoryName (typeof (object).Assembly.Location);
		if (runtimeDir != null) {
			resolver.AddSearchDirectory (runtimeDir);
		}

		var readerParams = new ReaderParameters { AssemblyResolver = resolver };
		using var assembly = AssemblyDefinition.ReadAssembly (assemblyPath, readerParams);

		var scanner = new XAJavaTypeScanner (
			Xamarin.Android.Tools.AndroidTargetArch.Arm64,
			new TaskLoggingHelper (new MockBuildEngine (), "test"),
			cache
		);

		var javaTypes = scanner.GetJavaTypes (assembly);
		var result = new Dictionary<string, ComponentComparisonData> (StringComparer.Ordinal);

		foreach (var typeDef in javaTypes) {
			if (!typeDef.HasCustomAttributes) {
				continue;
			}

			foreach (var attr in typeDef.CustomAttributes) {
				if (!ComponentAttributeNames.Contains (attr.AttributeType.FullName)) {
					continue;
				}

				var managedName = ScannerRunner.GetManagedName (typeDef);
				var kind = GetComponentKindFromAttributeName (attr.AttributeType.FullName);

				string? componentName = null;
				var properties = new List<string> ();

				if (attr.HasProperties) {
					foreach (var prop in attr.Properties.OrderBy (p => p.Name, StringComparer.Ordinal)) {
						if (prop.Name == "Name") {
							componentName = prop.Argument.Value as string;
						} else {
							var valueStr = prop.Argument.Value?.ToString () ?? "(null)";
							properties.Add ($"{prop.Name}={valueStr}");
						}
					}
				}

				properties.Sort (StringComparer.Ordinal);

				result [managedName] = new ComponentComparisonData (
					managedName,
					kind,
					componentName,
					properties
				);
				break;
			}
		}

		return result;
	}

	public static Dictionary<string, ComponentComparisonData> BuildNewComponentData (string[] assemblyPaths)
	{
		var primaryAssemblyName = Path.GetFileNameWithoutExtension (assemblyPaths [0]);
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (assemblyPaths);

		var result = new Dictionary<string, ComponentComparisonData> (StringComparer.Ordinal);

		foreach (var peer in peers) {
			if (peer.AssemblyName != primaryAssemblyName) {
				continue;
			}

			if (peer.ComponentAttribute == null) {
				continue;
			}

			var managedName = $"{peer.ManagedTypeName}, {peer.AssemblyName}";
			var component = peer.ComponentAttribute;
			var kind = component.Kind.ToString ();

			string? componentName = null;
			var properties = new List<string> ();

			foreach (var kvp in component.Properties.OrderBy (p => p.Key, StringComparer.Ordinal)) {
				if (kvp.Key == "Name") {
					componentName = kvp.Value as string;
				} else {
					var valueStr = kvp.Value?.ToString () ?? "(null)";
					properties.Add ($"{kvp.Key}={valueStr}");
				}
			}

			properties.Sort (StringComparer.Ordinal);

			result [managedName] = new ComponentComparisonData (
				managedName,
				kind,
				componentName,
				properties
			);
		}

		return result;
	}

	public static ManifestAttributeComparisonData BuildLegacyManifestData (string assemblyPath)
	{
		var resolver = new DefaultAssemblyResolver ();
		resolver.AddSearchDirectory (Path.GetDirectoryName (assemblyPath)!);

		var runtimeDir = Path.GetDirectoryName (typeof (object).Assembly.Location);
		if (runtimeDir != null) {
			resolver.AddSearchDirectory (runtimeDir);
		}

		var readerParams = new ReaderParameters { AssemblyResolver = resolver };
		using var assembly = AssemblyDefinition.ReadAssembly (assemblyPath, readerParams);

		var permissions = new List<string> ();
		var features = new List<string> ();

		foreach (var attr in assembly.CustomAttributes) {
			var attrName = attr.AttributeType.Name;
			switch (attrName) {
			case "UsesPermissionAttribute":
				if (attr.ConstructorArguments.Count > 0 && attr.ConstructorArguments [0].Value is string permName) {
					int? maxSdk = null;
					foreach (var prop in attr.Properties) {
						if (prop.Name == "MaxSdkVersion" && prop.Argument.Value is int sdk) {
							maxSdk = sdk;
						}
					}
					permissions.Add (EncodePermission (permName, maxSdk));
				}
				break;
			case "UsesFeatureAttribute":
				string? featName = null;
				int glesVersion = 0;
				bool required = true;
				if (attr.ConstructorArguments.Count > 0 && attr.ConstructorArguments [0].Value is string fn) {
					featName = fn;
				}
				foreach (var prop in attr.Properties) {
					if (prop.Name == "GLESVersion" && prop.Argument.Value is int gles) {
						glesVersion = gles;
					} else if (prop.Name == "Required" && prop.Argument.Value is bool req) {
						required = req;
					}
				}
				if (featName != null || glesVersion != 0) {
					features.Add (EncodeFeature (featName, glesVersion, required));
				}
				break;
			}
		}

		permissions.Sort (StringComparer.Ordinal);
		features.Sort (StringComparer.Ordinal);

		return new ManifestAttributeComparisonData (permissions, features);
	}

	public static ManifestAttributeComparisonData BuildNewManifestData (string[] assemblyPaths)
	{
		using var scanner = new JavaPeerScanner ();
		scanner.Scan (assemblyPaths);
		var manifestInfo = scanner.ScanAssemblyManifestInfo ();

		var permissions = manifestInfo.UsesPermissions
			.Select (p => EncodePermission (p.Name, p.MaxSdkVersion))
			.OrderBy (n => n, StringComparer.Ordinal)
			.ToList ();

		var features = manifestInfo.UsesFeatures
			.Select (f => EncodeFeature (f.Name, f.GLESVersion, f.Required))
			.OrderBy (n => n, StringComparer.Ordinal)
			.ToList ();

		return new ManifestAttributeComparisonData (permissions, features);
	}
}
