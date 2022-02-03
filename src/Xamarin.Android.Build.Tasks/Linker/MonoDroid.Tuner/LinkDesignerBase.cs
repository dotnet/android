using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using System;
using System.Linq;
using Xamarin.Android.Tasks;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using System.Text.RegularExpressions;
#if ILLINK
using Microsoft.Android.Sdk.ILLink;
#endif


namespace MonoDroid.Tuner  {
	public abstract class LinkDesignerBase : BaseStep {
		public virtual void LogMessage (string message)
		{
			Context.LogMessage (message);
		}

		public virtual AssemblyDefinition Resolve (AssemblyNameReference name)
		{
			return Context.Resolve (name);
		}

		protected bool FindResourceDesigner (AssemblyDefinition assembly, bool mainApplication, out TypeDefinition designer, out CustomAttribute designerAttribute)
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

		protected void ClearDesignerClass (TypeDefinition designer)
		{
			LogMessage ($"    TryRemoving {designer.FullName}");
			designer.NestedTypes.Clear ();
			designer.Methods.Clear ();
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
						output.Add(key, int.Parse (field.Constant?.ToString () ?? "0"));
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
			LoadDesigner ();

			var action = Annotations.HasAction (assembly) ? Annotations.GetAction (assembly) : AssemblyAction.Skip;
			if (action == AssemblyAction.Delete)
				return;

			if (ProcessAssemblyDesigner (assembly)) {
				if (action == AssemblyAction.Skip || action == AssemblyAction.Copy)
					Annotations.SetAction (assembly, AssemblyAction.Save);
			}
		}

		internal abstract bool ProcessAssemblyDesigner (AssemblyDefinition assemblyDefinition);
		protected abstract void LoadDesigner ();
		protected abstract void FixBody (MethodBody body, TypeDefinition designer);
	}
}
