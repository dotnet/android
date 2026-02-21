using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Generates the root <c>_Microsoft.Android.TypeMaps.dll</c> assembly that references
/// all per-assembly typemap assemblies via
/// <c>[assembly: TypeMapAssemblyTargetAttribute&lt;Java.Lang.Object&gt;("name")]</c>.
/// </summary>
/// <remarks>
/// <para>The generated assembly looks like this (pseudo-C#):</para>
/// <code>
/// // One attribute per per-assembly typemap assembly — tells the runtime where to find TypeMap entries:
/// [assembly: TypeMapAssemblyTarget&lt;Java.Lang.Object&gt;("_Mono.Android.TypeMap")]
/// [assembly: TypeMapAssemblyTarget&lt;Java.Lang.Object&gt;("_MyApp.TypeMap")]
/// </code>
/// </remarks>
sealed class RootTypeMapAssemblyGenerator
{
	const string DefaultAssemblyName = "_Microsoft.Android.TypeMaps";

	readonly Version _systemRuntimeVersion;

	/// <param name="systemRuntimeVersion">Version for System.Runtime assembly references.</param>
	public RootTypeMapAssemblyGenerator (Version systemRuntimeVersion)
	{
		_systemRuntimeVersion = systemRuntimeVersion ?? throw new ArgumentNullException (nameof (systemRuntimeVersion));
	}

	/// <summary>
	/// Generates the root typemap assembly.
	/// </summary>
	/// <param name="perAssemblyTypeMapNames">Names of per-assembly typemap assemblies to reference.</param>
	/// <param name="outputPath">Path to write the output .dll.</param>
	/// <param name="assemblyName">Optional assembly name (defaults to _Microsoft.Android.TypeMaps).</param>
	public void Generate (IReadOnlyList<string> perAssemblyTypeMapNames, string outputPath, string? assemblyName = null)
	{
		if (perAssemblyTypeMapNames is null) {
			throw new ArgumentNullException (nameof (perAssemblyTypeMapNames));
		}
		if (outputPath is null) {
			throw new ArgumentNullException (nameof (outputPath));
		}

		assemblyName ??= DefaultAssemblyName;
		var moduleName = Path.GetFileName (outputPath);

		var pe = new PEAssemblyBuilder (_systemRuntimeVersion);
		pe.EmitPreamble (assemblyName, moduleName);

		// Reference the open generic TypeMapAssemblyTargetAttribute`1 from System.Runtime.InteropServices
		var openAttrRef = pe.Metadata.AddTypeReference (pe.SystemRuntimeInteropServicesRef,
			pe.Metadata.GetOrAddString ("System.Runtime.InteropServices"),
			pe.Metadata.GetOrAddString ("TypeMapAssemblyTargetAttribute`1"));

		// Reference Java.Lang.Object from Mono.Android (the type universe)
		var javaLangObjectRef = pe.Metadata.AddTypeReference (pe.MonoAndroidRef,
			pe.Metadata.GetOrAddString ("Java.Lang"), pe.Metadata.GetOrAddString ("Object"));

		// Build TypeSpec for TypeMapAssemblyTargetAttribute<Java.Lang.Object>
		var closedAttrTypeSpec = pe.MakeGenericTypeSpec (openAttrRef, javaLangObjectRef);

		// MemberRef for .ctor(string) on the closed generic type
		var ctorRef = pe.AddMemberRef (closedAttrTypeSpec, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().String ()));

		// Add [assembly: TypeMapAssemblyTargetAttribute<Java.Lang.Object>("name")] for each per-assembly typemap
		foreach (var name in perAssemblyTypeMapNames) {
			var blobHandle = pe.BuildAttributeBlob (blob => blob.WriteSerializedString (name));
			pe.Metadata.AddCustomAttribute (EntityHandle.AssemblyDefinition, ctorRef, blobHandle);
		}

		pe.WritePE (outputPath);
	}
}
