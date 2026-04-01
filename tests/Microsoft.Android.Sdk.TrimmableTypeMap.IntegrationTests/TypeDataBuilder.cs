using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers.Adapters;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using CecilAssemblyDefinition = Mono.Cecil.AssemblyDefinition;
using CecilTypeDefinition = Mono.Cecil.TypeDefinition;
using Xamarin.Android.Tasks;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests;

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
		using var assembly = CecilAssemblyDefinition.ReadAssembly (assemblyPath, readerParams);

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
		using var scanner = new JavaPeerScanner ();
		var peReaders = new List<PEReader> ();
		var assemblies = new List<(string Name, PEReader Reader)> ();
		foreach (var path in assemblyPaths) {
			var peReader = new PEReader (File.OpenRead (path));
			peReaders.Add (peReader);
			var mdReader = peReader.GetMetadataReader ();
			assemblies.Add ((mdReader.GetString (mdReader.GetAssemblyDefinition ().Name), peReader));
		}
		var primaryAssemblyName = assemblies [0].Name;
		var peers = scanner.Scan (assemblies);
		foreach (var peReader in peReaders) peReader.Dispose ();

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

	static void FindLegacyActivationCtor (CecilTypeDefinition typeDef, TypeDefinitionCache cache,
		out bool found, out string? declaringType, out string? style)
	{
		found = false;
		declaringType = null;
		style = null;

		CecilTypeDefinition? current = typeDef;
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

	static bool GetCecilDoNotGenerateAcw (CecilTypeDefinition typeDef)
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

	static void ExtractDirectRegisterCtors (CecilTypeDefinition typeDef, List<string> javaCtorSignatures)
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
}
