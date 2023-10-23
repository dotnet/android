using System;

using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;

#if ILLINK
using Microsoft.Android.Sdk.ILLink;
#endif

namespace MonoDroid.Tuner;

public class AddRidMetadataAttributeStep : BaseStep
{
	protected override void ProcessAssembly (AssemblyDefinition assembly)
	{
		if (!Annotations.HasAction (assembly)) {
				return;
		}

		var action = Annotations.GetAction (assembly);
		if (action == AssemblyAction.Skip || action == AssemblyAction.Delete) {
			return;
		}

		string? rid = null;
#if ILLINK
		if (!Context.TryGetCustomData ("XARuntimeIdentifier", out rid)) {
			throw new InvalidOperationException ("Missing XARuntimeIdentifier custom data");
		}
#endif
		if (String.IsNullOrEmpty (rid)) {
			throw new InvalidOperationException ("RID must have a non-empty value");
		}

		AssemblyDefinition corlib = GetCorlib ();
		MethodDefinition assemblyMetadataAttributeCtor = FindAssemblyMetadataAttributeCtor (corlib);
		TypeDefinition systemString = GetSystemString (corlib);

		var attr = new CustomAttribute (assembly.MainModule.ImportReference (assemblyMetadataAttributeCtor));
		attr.ConstructorArguments.Add (new CustomAttributeArgument (systemString, "XamarinAndroidAbi")); // key

		// TODO: figure out how to get the RID...
		attr.ConstructorArguments.Add (new CustomAttributeArgument (systemString, rid)); // value

		assembly.CustomAttributes.Add (attr);

		if (action == AssemblyAction.Copy) {
			Annotations.SetAction (assembly, AssemblyAction.Save);
		}
	}

	TypeDefinition GetSystemString (AssemblyDefinition asm) => FindType (asm, "System.String", required: true);

	AssemblyDefinition GetCorlib ()
	{
		const string ImportAssembly = "System.Private.CoreLib";
		AssemblyDefinition? asm = Context.Resolve (AssemblyNameReference.Parse (ImportAssembly));
		if (asm == null) {
			throw new InvalidOperationException ($"Unable to import assembly '{ImportAssembly}'");
		}

		return asm;
	}

	MethodDefinition FindAssemblyMetadataAttributeCtor (AssemblyDefinition asm)
	{
		const string AttributeType = "System.Reflection.AssemblyMetadataAttribute";

		TypeDefinition assemblyMetadataAttribute = FindType (asm, AttributeType, required: true);
		foreach (MethodDefinition md in assemblyMetadataAttribute!.Methods) {
			if (!md.IsConstructor) {
				continue;
			}

			return md;
		}

		throw new InvalidOperationException ($"Unable to find the {AttributeType} type constructor");
	}

	TypeDefinition? FindType (AssemblyDefinition asm, string typeName, bool required)
	{
		foreach (ModuleDefinition md in asm.Modules) {
			foreach (TypeDefinition et in md.Types) {
				if (String.Compare (typeName, et.FullName, StringComparison.Ordinal) != 0) {
					continue;
				}

				return et;
			}
		}

		if (required) {
			throw new InvalidOperationException ($"Internal error: required type '{typeName}' in assembly {asm} not found");
		}

		return null;
	}
}
