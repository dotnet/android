#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using Xamarin.Android.Tasks;

namespace MonoDroid.Tuner;

/// <summary>
/// Inlines resource designer constants and removes designer classes.
/// The core logic is adapted from the original ILLink RemoveResourceDesignerStep / LinkDesignerBase.
/// </summary>
class RemoveResourceDesignerStep : IAssemblyModifierPipelineStep
{
	readonly IList<AssemblyDefinition> allAssemblies;
	readonly Action<string> logMessage;
	readonly Regex opCodeRegex = new Regex (@"([\w]+): ([\w]+) ([\w.]+) ([\w:./]+)");

	TypeDefinition mainDesigner = null;
	AssemblyDefinition mainAssembly = null;
	CustomAttribute mainDesignerAttribute;
	Dictionary<string, int> designerConstants;
	bool designerLoaded;

	public RemoveResourceDesignerStep (IList<AssemblyDefinition> allAssemblies, Action<string> logMessage)
	{
		this.allAssemblies = allAssemblies;
		this.logMessage = logMessage;
	}

	public void ProcessAssembly (AssemblyDefinition assembly, StepContext context)
	{
		LoadDesigner ();
		context.IsAssemblyModified |= ProcessAssemblyDesigner (assembly);
	}

	void LoadDesigner ()
	{
		if (designerLoaded)
			return;
		designerLoaded = true;

		foreach (var asm in allAssemblies) {
			if (FindResourceDesigner (asm, mainApplication: true, designer: out mainDesigner, designerAttribute: out mainDesignerAttribute)) {
				mainAssembly = asm;
				break;
			}
		}
		if (mainDesigner == null) {
			logMessage ("  Main Designer not found.");
			return;
		}
		logMessage ($"  Main Designer found {mainDesigner.FullName}.");
		designerConstants = BuildResourceDesignerFieldLookup (mainDesigner);
	}

	bool ProcessAssemblyDesigner (AssemblyDefinition assembly)
	{
		if (mainDesigner == null)
			return false;
		if (MonoAndroidHelper.IsFrameworkAssembly (assembly))
			return false;

		logMessage ($"  Fixing up {assembly.Name.Name}");
		TypeDefinition localDesigner = null;
		CustomAttribute designerAttribute;
		if (assembly != mainAssembly) {
			logMessage ($"   {assembly.Name.Name} is not the main assembly. ");
			if (!FindResourceDesigner (assembly, mainApplication: false, designer: out localDesigner, designerAttribute: out designerAttribute)) {
				logMessage ($"   {assembly.Name.Name} does not have a designer file.");
				return false;
			}
		} else {
			logMessage ($"   {assembly.Name.Name} is the main assembly. ");
			localDesigner = mainDesigner;
			designerAttribute = mainDesignerAttribute;
		}

		logMessage ($"   {assembly.Name.Name} has designer {localDesigner.FullName}.");

		FixupAssemblyTypes (assembly, localDesigner);

		ClearDesignerClass (localDesigner);
		if (designerAttribute != null) {
			assembly.CustomAttributes.Remove (designerAttribute);
		}
		return true;
	}

	// ---- Methods below are from LinkDesignerBase, adapted for non-ILLink use ----

	bool FindResourceDesigner (AssemblyDefinition assembly, bool mainApplication, out TypeDefinition designer, out CustomAttribute designerAttribute)
	{
		string designerFullName = null;
		designer = null;
		designerAttribute = null;
		foreach (CustomAttribute attribute in assembly.CustomAttributes)
		{
			if (attribute.AttributeType.FullName == "Android.Runtime.ResourceDesignerAttribute")
			{
				designerAttribute = attribute;
				if (attribute.HasProperties)
				{
					foreach (var p in attribute.Properties)
					{
						if (p.Name == "IsApplication" && (bool)p.Argument.Value == (mainApplication ? mainApplication : (bool)p.Argument.Value))
						{
							designerFullName = attribute.ConstructorArguments[0].Value.ToString ();
							break;
						}
					}
				}
				break;

			}
		}

		if (string.IsNullOrEmpty(designerFullName)) {
			logMessage ($"Inspecting member references for assembly: {assembly.FullName};");
			var memberRefs = assembly.MainModule.GetMemberReferences ();
			foreach (var memberRef in memberRefs) {
				string declaringType = memberRef.DeclaringType?.ToString () ?? string.Empty;
				if (!declaringType.Contains (".Resource/")) {
					continue;
				}
				if (declaringType.Contains ("_Microsoft.Android.Resource.Designer")) {
					continue;
				}
				var resolved = false;
				try {
					var def = memberRef.Resolve ();
					if (resolved = def != null) {
						logMessage ($"Resolved member `{memberRef?.Name}`");
					}
				} catch (Exception ex) {
					logMessage ($"Exception resolving member `{memberRef?.Name}`: {ex}");
					resolved = false;
				}
				if (!resolved) {
					logMessage ($"Adding _Linker.Generated.Resource to {assembly.Name.Name}. Could not resolve {memberRef?.Name} : {declaringType}");
					designer = new TypeDefinition ("_Linker.Generated", "Resource", TypeAttributes.Public | TypeAttributes.AnsiClass);
					designer.BaseType = new TypeDefinition ("System", "Object", TypeAttributes.Public | TypeAttributes.AnsiClass);
					return true;
				}
			}
		}

		if (string.IsNullOrEmpty(designerFullName))
			return false;

		foreach (ModuleDefinition module in assembly.Modules)
		{
			foreach (TypeDefinition type in module.Types)
			{
				if (type.FullName == designerFullName)
				{
					designer = type;
					return true;
				}
			}
		}
		return false;
	}

	void FixBody (MethodBody body, TypeDefinition designer)
	{
		Dictionary<Instruction, int> instructions = new Dictionary<Instruction, int>();
		var processor = body.GetILProcessor ();
		string designerFullName = $"{designer.FullName}/";
		bool isDesignerMethod = designerFullName.Contains (body.Method.DeclaringType.FullName);
		string declaringTypeName = body.Method.DeclaringType.Name;
		foreach (var i in body.Instructions)
		{
			string line = i.ToString ();
			if ((line.Contains (designerFullName) || (isDesignerMethod && i.OpCode == OpCodes.Stsfld)) && !instructions.ContainsKey (i))
			{
				var match = opCodeRegex.Match (line);
				if (match.Success && match.Groups.Count == 5) {
					string key = match.Groups[4].Value.Replace (designerFullName, string.Empty);
					if (isDesignerMethod) {
						key = declaringTypeName +"::" + key;
					}
					if (designerConstants.ContainsKey (key) && !instructions.ContainsKey (i))
						instructions.Add(i, designerConstants [key]);
				}
			}
		}
		if (instructions.Count > 0)
			logMessage ($"    Fixing up {body.Method.FullName}");
		foreach (var i in instructions)
		{
			var newCode = Extensions.CreateLoadArraySizeOrOffsetInstruction (i.Value);
			logMessage ($"      Replacing {i.Key}");
			logMessage ($"      With {newCode}");
			processor.Replace(i.Key, newCode);
		}
	}

	Dictionary<string, int> BuildResourceDesignerFieldLookup (TypeDefinition type)
	{
		var output = new Dictionary<string, int> ();
		foreach (TypeDefinition definition in type.NestedTypes)
		{
			foreach (FieldDefinition field in definition.Fields)
			{
				string key = $"{definition.Name}::{field.Name}";
				if (!output.ContainsKey (key))
					output.Add(key, int.Parse (field.Constant?.ToString () ?? "0", CultureInfo.InvariantCulture));
			}
		}
		return output;
	}

	void ClearDesignerClass (TypeDefinition designer, bool completely = false)
	{
		logMessage ($"    TryRemoving {designer.FullName}");
		// for each of the nested types clear all but the
		// int[] fields.
		if (!completely) {
			for (int i = designer.NestedTypes.Count -1; i >= 0; i--) {
				var nestedType = designer.NestedTypes [i];
				RemoveFieldsFromType (nestedType, designer.Module);
				if (nestedType.Fields.Count == 0) {
					// no fields we do not need this class at all.
					designer.NestedTypes.RemoveAt (i);
				}
			}
			RemoveUpdateIdValues (designer);
		} else {
			designer.NestedTypes.Clear ();
		}
		designer.Fields.Clear ();
		designer.Properties.Clear ();
		designer.CustomAttributes.Clear ();
		designer.Interfaces.Clear ();
		designer.Events.Clear ();
	}

	void FixType (TypeDefinition type, TypeDefinition localDesigner)
	{
		foreach (MethodDefinition method in type.Methods)
		{
			if (!method.HasBody)
				continue;
			FixBody (method.Body, localDesigner);
		}
		foreach (PropertyDefinition property in type.Properties)
		{
			if (property.GetMethod != null && property.GetMethod.HasBody)
			{
				FixBody (property.GetMethod.Body, localDesigner);
			}
			if (property.SetMethod != null && property.SetMethod.HasBody)
			{
				FixBody (property.SetMethod.Body, localDesigner);
			}
		}
		foreach (TypeDefinition nestedType in type.NestedTypes)
		{
			FixType (nestedType, localDesigner);
		}
	}

	void FixupAssemblyTypes (AssemblyDefinition assembly, TypeDefinition designer)
	{
		foreach (ModuleDefinition module in assembly.Modules)
		{
			foreach (TypeDefinition type in module.Types)
			{
				if (type.FullName == designer.FullName)
					continue;
				FixType (type, designer);
			}
		}
	}

	void RemoveFieldsFromType (TypeDefinition type, ModuleDefinition module)
	{
		for (int i = type.Fields.Count - 1; i >= 0; i--) {
			var field = type.Fields [i];
			if (field.FieldType.IsArray) {
				continue;
			}
			logMessage ($"Removing {type.Name}::{field.Name}");
			type.Fields.RemoveAt (i);
		}
	}

	void RemoveUpdateIdValues (TypeDefinition type)
	{
		foreach (var method in type.Methods) {
			if (method.Name.Contains ("UpdateIdValues")) {
				FixUpdateIdValuesBody (method);
			} else {
				FixBody (method.Body, type);
			}
		}

		foreach (var nestedType in type.NestedTypes) {
			RemoveUpdateIdValues (nestedType);
		}
	}

	void FixUpdateIdValuesBody (MethodDefinition method)
	{
		List<Instruction> finalInstructions = new List<Instruction> ();
		Collection<Instruction> instructions = method.Body.Instructions;
		for (int i = 0; i < method.Body.Instructions.Count-1; i++) {
			Instruction instruction = instructions[i];
			string line = instruction.ToString ();
			bool found = line.Contains ("Int32[]") || instruction.OpCode == OpCodes.Ret;
			if (!found) {
				method.Body.Instructions.Remove (instruction);
				i--;
			}
		}
	}
}
