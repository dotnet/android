using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers.Adapters;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tasks;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests;

record TypeMapEntry (string JavaName, string ManagedName, bool SkipInJavaToManaged);

record MethodEntry (string JniName, string JniSignature, string? Connector);

record TypeMethodGroup (string ManagedName, List<MethodEntry> Methods);

static class ScannerRunner
{
	public static (List<TypeMapEntry> entries, Dictionary<string, List<TypeMethodGroup>> methodsByJavaName) RunLegacy (string assemblyPath)
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

		var methodsByJavaName = new Dictionary<string, List<TypeMethodGroup>> ();
		foreach (var typeDef in javaTypes) {
			var javaName = GetCecilJavaName (typeDef);
			if (javaName == null) {
				continue;
			}

			var managedName = GetManagedName (typeDef);
			var methods = ExtractMethodRegistrations (typeDef, cache);

			if (!methodsByJavaName.TryGetValue (javaName, out var groups)) {
				groups = new List<TypeMethodGroup> ();
				methodsByJavaName [javaName] = groups;
			}

			groups.Add (new TypeMethodGroup (
				managedName,
				methods.OrderBy (m => m.JniName, StringComparer.Ordinal)
					.ThenBy (m => m.JniSignature, StringComparer.Ordinal)
					.ToList ()
			));
		}

		foreach (var entry in dataSets.JavaToManaged) {
			if (methodsByJavaName.ContainsKey (entry.JavaName)) {
				continue;
			}

			methodsByJavaName [entry.JavaName] = new List<TypeMethodGroup> {
				new TypeMethodGroup (entry.ManagedName, new List<MethodEntry> ())
			};
		}

		return (entries, methodsByJavaName);
	}

	public static (List<TypeMapEntry> entries, Dictionary<string, List<TypeMethodGroup>> methodsByJavaName) RunNew (string[] assemblyPaths)
	{
		var primaryAssemblyName = Path.GetFileNameWithoutExtension (assemblyPaths [0]);
		using var scanner = new JavaPeerScanner ();
		var allPeers = scanner.Scan (assemblyPaths);
		var peers = allPeers.Where (p => p.AssemblyName == primaryAssemblyName).ToList ();

		var entries = peers
			.Select (p => new TypeMapEntry (
				p.JavaName,
				$"{p.ManagedTypeName}, {p.AssemblyName}",
				p.IsInterface || p.IsGenericDefinition
			))
			.OrderBy (e => e.JavaName, StringComparer.Ordinal)
			.ThenBy (e => e.ManagedName, StringComparer.Ordinal)
			.ToList ();

		var methodsByJavaName = new Dictionary<string, List<TypeMethodGroup>> ();
		foreach (var peer in peers) {
			var managedName = $"{peer.ManagedTypeName}, {peer.AssemblyName}";

			if (!methodsByJavaName.TryGetValue (peer.JavaName, out var groups)) {
				groups = new List<TypeMethodGroup> ();
				methodsByJavaName [peer.JavaName] = groups;
			}

			groups.Add (new TypeMethodGroup (
				managedName,
				peer.MarshalMethods
					.Where (m => !m.IsConstructor)
					.Select (m => new MethodEntry (m.JniName, m.JniSignature, m.Connector))
					.OrderBy (m => m.JniName, StringComparer.Ordinal)
					.ThenBy (m => m.JniSignature, StringComparer.Ordinal)
					.ToList ()
			));
		}

		return (entries, methodsByJavaName);
	}

	public static string? GetCecilJavaName (TypeDefinition typeDef)
	{
		if (!typeDef.HasCustomAttributes) {
			return null;
		}

		foreach (var attr in typeDef.CustomAttributes) {
			if (attr.AttributeType.FullName != "Android.Runtime.RegisterAttribute") {
				continue;
			}

			if (attr.ConstructorArguments.Count > 0) {
				return ((string) attr.ConstructorArguments [0].Value).Replace ('.', '/');
			}
		}

		return null;
	}

	public static string GetManagedName (TypeDefinition typeDef)
	{
		return $"{typeDef.FullName.Replace ('/', '+')}, {typeDef.Module.Assembly.Name.Name}";
	}

	/// <summary>
	/// Extracts marshal methods using the real legacy JCW pipeline via
	/// <see cref="CecilImporter.CreateType"/>. Excludes interface method
	/// implementations since the new scanner places those on the interface
	/// peer type rather than the implementing type.
	/// </summary>
	static List<MethodEntry> ExtractMethodRegistrations (TypeDefinition typeDef, TypeDefinitionCache cache)
	{
		if (typeDef.IsInterface) {
			// CecilImporter throws XA4200 for interfaces.
			// Extract [Register] from interface methods directly.
			return ExtractDirectRegisterAttributes (typeDef);
		}

		// Types with DoNotGenerateAcw=true are never passed to CecilImporter.CreateType
		// in the real build (JavaTypeScanner skips them for JCW generation). Use direct
		// attribute extraction to match what actually happens at build time.
		if (HasDoNotGenerateAcw (typeDef)) {
			return ExtractDirectRegisterAttributes (typeDef);
		}

		// Build a set of method names from implemented interfaces so we can
		// filter them out of the CecilImporter output.
		var interfaceMethodKeys = new HashSet<string> (StringComparer.Ordinal);
		foreach (var ifaceImpl in typeDef.Interfaces) {
			var ifaceType = cache.Resolve (ifaceImpl.InterfaceType);
			if (ifaceType is null) {
				continue;
			}
			foreach (var method in ifaceType.Methods) {
				if (method.IsStatic) {
					continue;
				}
				foreach (var attr in method.CustomAttributes) {
					if (attr.AttributeType.FullName == "Android.Runtime.RegisterAttribute" && attr.ConstructorArguments.Count >= 2) {
						var name = (string) attr.ConstructorArguments [0].Value;
						var sig = (string) attr.ConstructorArguments [1].Value;
						interfaceMethodKeys.Add ($"{name}:{sig}");
					}
				}
			}
		}

		var wrapper = CecilImporter.CreateType (typeDef, cache);
		var methods = new List<MethodEntry> ();

		foreach (var m in wrapper.Methods) {
			var key = $"{m.Name}:{m.JniSignature}";

			// Skip methods that came from interface implementations — the new
			// scanner places these on the interface peer, not the implementing type.
			// Only skip if the type doesn't also have a direct [Register] for this
			// method (which would mean the type explicitly registers it).
			if (interfaceMethodKeys.Contains (key) && !HasDirectRegister (typeDef, m.Name, m.JniSignature)) {
				continue;
			}

			// Extract connector from Method string "n_name:sig:connector"
			string? connector = ParseConnectorFromMethodString (m.Method);
			methods.Add (new MethodEntry (m.Name, m.JniSignature, connector));
		}

		return methods;
	}

	/// <summary>
	/// Checks if the type has a direct [Register] attribute for this method name + signature.
	/// </summary>
	static bool HasDirectRegister (TypeDefinition typeDef, string jniName, string jniSignature)
	{
		foreach (var method in typeDef.Methods) {
			if (!method.HasCustomAttributes) {
				continue;
			}
			foreach (var attr in method.CustomAttributes) {
				if (attr.AttributeType.FullName == "Android.Runtime.RegisterAttribute" &&
				    attr.ConstructorArguments.Count >= 2 &&
				    (string) attr.ConstructorArguments [0].Value == jniName &&
				    (string) attr.ConstructorArguments [1].Value == jniSignature) {
					return true;
				}
			}
		}
		if (typeDef.HasProperties) {
			foreach (var prop in typeDef.Properties) {
				if (!prop.HasCustomAttributes) {
					continue;
				}
				foreach (var attr in prop.CustomAttributes) {
					if (attr.AttributeType.FullName == "Android.Runtime.RegisterAttribute" &&
					    attr.ConstructorArguments.Count >= 2 &&
					    (string) attr.ConstructorArguments [0].Value == jniName &&
					    (string) attr.ConstructorArguments [1].Value == jniSignature) {
						return true;
					}
				}
			}
		}
		return false;
	}

	internal static bool HasDoNotGenerateAcw (TypeDefinition typeDef)
	{
		if (!typeDef.HasCustomAttributes) {
			return false;
		}
		foreach (var attr in typeDef.CustomAttributes) {
			if (attr.AttributeType.FullName != "Android.Runtime.RegisterAttribute") {
				continue;
			}
			var v = attr.Properties.FirstOrDefault (p => p.Name == "DoNotGenerateAcw");
			if (v.Name != null && v.Argument.Value is bool b && b) {
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Fallback: extract [Register] from methods/properties directly (for interfaces
	/// and DoNotGenerateAcw types that are never passed through CecilImporter).
	/// </summary>
	static List<MethodEntry> ExtractDirectRegisterAttributes (TypeDefinition typeDef)
	{
		var methods = new List<MethodEntry> ();
		foreach (var method in typeDef.Methods) {
			if (!method.HasCustomAttributes) {
				continue;
			}
			foreach (var attr in method.CustomAttributes) {
				if (attr.AttributeType.FullName == "Android.Runtime.RegisterAttribute" && attr.ConstructorArguments.Count >= 2) {
					var jniName = (string) attr.ConstructorArguments [0].Value;
					// Skip constructors — compared separately in ExactJavaConstructors
					if (jniName == "<init>" || jniName == ".ctor") {
						continue;
					}
					methods.Add (new MethodEntry (
						jniName,
						(string) attr.ConstructorArguments [1].Value,
						attr.ConstructorArguments.Count > 2 ? (string) attr.ConstructorArguments [2].Value : null
					));
				}
			}
		}
		if (typeDef.HasProperties) {
			foreach (var prop in typeDef.Properties) {
				if (!prop.HasCustomAttributes) {
					continue;
				}
				foreach (var attr in prop.CustomAttributes) {
					if (attr.AttributeType.FullName == "Android.Runtime.RegisterAttribute" && attr.ConstructorArguments.Count >= 2) {
						methods.Add (new MethodEntry (
							(string) attr.ConstructorArguments [0].Value,
							(string) attr.ConstructorArguments [1].Value,
							attr.ConstructorArguments.Count > 2 ? (string) attr.ConstructorArguments [2].Value : null
						));
					}
				}
			}
		}
		return methods;
	}

	/// <summary>
	/// Parses the connector from a CallableWrapperMethod.Method string.
	/// Format: "n_{name}:{signature}:{connector}" where connector has '/' replaced with '+'.
	/// </summary>
	static string? ParseConnectorFromMethodString (string? methodStr)
	{
		if (methodStr is null) {
			return null;
		}
		int firstColon = methodStr.IndexOf (':');
		if (firstColon < 0) {
			return null;
		}
		int secondColon = methodStr.IndexOf (':', firstColon + 1);
		if (secondColon < 0 || secondColon + 1 >= methodStr.Length) {
			return null;
		}
		var connector = methodStr.Substring (secondColon + 1);
		return connector.Replace ('+', '/');
	}
}
