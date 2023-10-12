using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using System;
using System.Linq;
using Xamarin.Android.Tasks;
using System.Collections.Generic;
using System.Globalization;
using Mono.Cecil.Cil;
using System.Text.RegularExpressions;
using Mono.Collections.Generic;
#if ILLINK
using Microsoft.Android.Sdk.ILLink;
#endif


namespace MonoDroid.Tuner  {
	public abstract class LinkDesignerBase : BaseStep {
		HashSet<AssemblyDefinition> allAssemblies = new ();
		HashSet<AssemblyDefinition> processedAssemblies = new ();

		string[] knownDesigners = new string [] {
			"Microsoft.Maui",
			"Microsoft.Maui.Core",
			"Microsoft.Maui.Controls",
		};

		public virtual void LogMessage (string message)
		{
			Context.LogMessage (message);
		}

		public virtual void LogError (int code, string error)
		{
#if ILLINK
			Context.LogMessage (MessageContainer.CreateCustomErrorMessage (error, code, origin: new MessageOrigin ()));
#else   // !ILLINK
			Console.Error.WriteLine ($"error XA{code}: {error}");
#endif  // !ILLINK
		}

		public virtual AssemblyDefinition Resolve (AssemblyNameReference name)
		{
			return Context.Resolve (name);
		}

		internal bool FindResourceDesigner (AssemblyDefinition assembly, bool mainApplication, out TypeDefinition designer, out CustomAttribute designerAttribute)
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
				// Check for known designers which have been removed.
				LogMessage ($"    c");
				foreach (var d in knownDesigners) {
					LogMessage ($"    Checking {d} against {assembly.Name.Name}");
					if (d.Contains (assembly.Name.Name)) {
						designer = new TypeDefinition (d, "Resource", TypeAttributes.Public | TypeAttributes.AnsiClass);
						designer.BaseType = new TypeDefinition ("System", "Object", TypeAttributes.Public | TypeAttributes.AnsiClass);
						return true;
					}
				}
				return false;
			}

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

		protected void ClearDesignerClass (TypeDefinition designer, bool completely = false)
		{
			LogMessage ($"    TryRemoving {designer.FullName}");
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
		protected Dictionary<string, int> BuildResourceDesignerFieldLookup (TypeDefinition type)
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

		protected void FixType (TypeDefinition type, TypeDefinition localDesigner)
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

		protected void RemoveFieldsFromType (TypeDefinition type, ModuleDefinition module)
		{
			for (int i = type.Fields.Count - 1; i >= 0; i--) {
				var field = type.Fields [i];
				if (field.FieldType.IsArray) {
					continue;
				}
				LogMessage ($"Removing {type.Name}::{field.Name}");
				type.Fields.RemoveAt (i);
			}
		}

		protected void RemoveUpdateIdValues (TypeDefinition type)
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

		protected void FixUpdateIdValuesBody (MethodDefinition method)
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

		protected void FixupAssemblyTypes (AssemblyDefinition assembly, TypeDefinition designer)
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

		protected override void ProcessAssembly (AssemblyDefinition assembly)
		{
			allAssemblies.Add (assembly);
			LoadDesigner ();

			var action = Annotations.HasAction (assembly) ? Annotations.GetAction (assembly) : AssemblyAction.Skip;
			if (action == AssemblyAction.Delete)
				return;

			if (ProcessAssemblyDesigner (assembly)) {
				if (action == AssemblyAction.Skip || action == AssemblyAction.Copy) {
					Annotations.SetAction (assembly, AssemblyAction.Save);
					processedAssemblies.Add (assembly);
				}
			}
		}

		internal void ProcessNonDesignerAssemblies ()
		{
			
		}

		protected override void EndProcess ()
		{
			// This is a "second pass" to fix assemblies with references to assemblies with a designer
			if (processedAssemblies.Count > 0) {
				foreach (var assembly in allAssemblies) {
					if (processedAssemblies.Contains (assembly))
						continue;

					var action = Annotations.HasAction (assembly) ? Annotations.GetAction (assembly) : AssemblyAction.Skip;
					if (action == AssemblyAction.Delete)
						continue;

					foreach (var processedAssembly in processedAssemblies) {
						if (ProcessAssemblyDesignerSecondPass (assembly, processedAssembly) &&
								(action == AssemblyAction.Skip || action == AssemblyAction.Copy)) {
							Annotations.SetAction (assembly, AssemblyAction.Save);
						}
					}
				}
			}
		}

		public bool ProcessAssemblyDesignerSecondPass (AssemblyDefinition assembly, AssemblyDefinition processedAssembly)
		{
			if (assembly.MainModule.AssemblyReferences.Any (r => r.FullName == processedAssembly.Name.FullName)) {
				LogMessage ($"   {assembly.Name.Name} has an assembly reference to {processedAssembly.Name}");
				if (FindResourceDesigner (processedAssembly, mainApplication: false, out TypeDefinition designer, out _) &&
						ProcessAssemblyDesigner (assembly, designer)) {
					return true;
				} else {
					LogMessage ($"   {processedAssembly.Name} did not have a designer");
				}
			}
			return false;
		}

		internal abstract bool ProcessAssemblyDesigner (AssemblyDefinition assemblyDefinition, TypeDefinition designer = null);
		protected abstract void LoadDesigner ();
		protected abstract void FixBody (MethodBody body, TypeDefinition designer);
	}
}
