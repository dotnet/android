using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using System;
using System.Linq;
using Xamarin.Android.Tasks;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using System.Text.RegularExpressions;

namespace MonoDroid.Tuner
{
	public class RemoveResourceDesignerStep : BaseStep
	{
		TypeDefinition mainDesigner = null;
		AssemblyDefinition mainAssembly = null;
		CustomAttribute mainDesignerAttribute;
		Dictionary<string, int> designerConstants;
		Regex opCodeRegex = new Regex (@"([\w]+): ([\w]+) ([\w.]+) ([\w:./]+)");

		protected override void Process ()
		{
			// resolve the MainAssembly Resource designer TypeDefinition
			AndroidLinkConfiguration config = AndroidLinkConfiguration.GetInstance (Context);
			if (config == null)
				return;
			foreach(var asm in config.Assemblies) {
				if (FindResourceDesigner (asm, mainApplication: true, designer: out mainDesigner, designerAttribute: out mainDesignerAttribute)) {
					mainAssembly = asm;
				 	break;
				}
			}
			if (mainDesigner == null) {
				Context.LogMessage ($"  Main Designer not found.");
				return;
			}
			Context.LogMessage ($"  Main Designer found {mainDesigner.FullName}.");
			designerConstants = BuildResourceDesignerFieldLookup (mainDesigner);
		}

		protected override void EndProcess ()
		{
			if (mainDesigner != null) {
				Context.LogMessage ($"  Setting Action on {mainAssembly.Name} to Save.");
				Annotations.SetAction (mainAssembly, AssemblyAction.Save);
			}
		}

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

		Dictionary<string, int> BuildResourceDesignerFieldLookup (TypeDefinition type)
		{
			var output = new Dictionary<string, int> ();
			foreach (TypeDefinition definition in type.NestedTypes)
			{
				foreach (FieldDefinition field in definition.Fields)
				{
					string key = $"{definition.Name}::{field.Name}";
					if (!output.ContainsKey (key))
						output.Add(key, int.Parse (field.Constant?.ToString () ?? "0"));
				}
			}
			return output;
		}

		void ClearDesignerClass (TypeDefinition designer)
		{
			Context.LogMessage ($"    TryRemoving {designer.FullName}");
			designer.NestedTypes.Clear ();
			designer.Methods.Clear ();
			designer.Fields.Clear ();
			designer.Properties.Clear ();
			designer.CustomAttributes.Clear ();
			designer.Interfaces.Clear ();
			designer.Events.Clear ();
		}

		void FixBody (MethodBody body, TypeDefinition localDesigner)
		{
			Dictionary<Instruction, int> instructions = new Dictionary<Instruction, int>();
			var processor = body.GetILProcessor ();
			string designerFullName = $"{localDesigner.FullName}/";
			foreach (var i in body.Instructions)
			{
				string line = i.ToString ();
				if (line.Contains (designerFullName) && !instructions.ContainsKey (i))
				{
					var match = opCodeRegex.Match (line);
					if (match.Success && match.Groups.Count == 5) {
						string key = match.Groups[4].Value.Replace (designerFullName, string.Empty);
						if (designerConstants.ContainsKey (key) && !instructions.ContainsKey (i))
							instructions.Add(i, designerConstants [key]);
					}
				}
			}
			if (instructions.Count > 0)
				Context.LogMessage ($"    Fixing up {body.Method.FullName}");
			foreach (var i in instructions)
			{
				var newCode = Extensions.CreateLoadArraySizeOrOffsetInstruction (i.Value);
				Context.LogMessage ($"      Replacing {i.Key}");
				Context.LogMessage ($"      With {newCode}");
				processor.Replace(i.Key, newCode);
			}
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

		protected override void ProcessAssembly (AssemblyDefinition assembly)
		{
			if (mainDesigner == null)
				return;
			var fileName = assembly.Name.Name + ".dll";
			if (MonoAndroidHelper.IsFrameworkAssembly (fileName))
				return;

			Context.LogMessage ($"  Fixing up {assembly.Name.Name}");
			TypeDefinition localDesigner = null;
			CustomAttribute designerAttribute;
			if (assembly != mainAssembly) {
				Context.LogMessage ($"   {assembly.Name.Name} is not the main assembly. ");
				if (!FindResourceDesigner (assembly, mainApplication: false, designer: out localDesigner, designerAttribute: out designerAttribute)) {
					Context.LogMessage ($"   {assembly.Name.Name} does not have a designer file.");
					return;
				}
			} else {
				Context.LogMessage ($"   {assembly.Name.Name} is the main assembly. ");
				localDesigner = mainDesigner;
				designerAttribute = mainDesignerAttribute;
			}

			Context.LogMessage ($"   {assembly.Name.Name} has designer {localDesigner.FullName}.");

			foreach (var mod in assembly.Modules) {
				foreach (var type in mod.Types) {
					if (type == localDesigner)
						continue;
					FixType (type, localDesigner);
				}
			}
			ClearDesignerClass (localDesigner);
			if (designerAttribute != null) {
				assembly.CustomAttributes.Remove (designerAttribute);
			}
		}
	}
}
