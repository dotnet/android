using System;
using System.Collections.Generic;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;

using Java.Interop.Tools.Cecil;

using Mono.Linker;
using Mono.Linker.Steps;

using Mono.Tuner;
#if ILLINK
using Microsoft.Android.Sdk.ILLink;
#endif  // ILLINK

namespace MonoDroid.Tuner
{
	public class FixLegacyResourceDesignerStep : LinkDesignerBase
	{
		internal const string DesignerAssemblyName = "_Microsoft.Android.Resource.Designer";
		internal const string DesignerAssemblyNamespace = "_Microsoft.Android.Resource.Designer";

		bool designerLoaded = false;
		AssemblyDefinition designerAssembly = null;
		TypeDefinition designerType = null;
		Dictionary<string, MethodDefinition> lookup;

		protected override void EndProcess ()
		{
			if (designerAssembly != null) {
				LogMessage ($"  Setting Action on {designerAssembly.Name} to Link.");
				Annotations.SetAction (designerAssembly, AssemblyAction.Link);
			}
		}

		protected override void LoadDesigner ()
		{
			if (designerLoaded)
				return;
			try {
				var designerNameAssembly = AssemblyNameReference.Parse ($"{DesignerAssemblyName}, Version=1.0.0.0");
				try {
					designerAssembly = Resolve (designerNameAssembly);
					LogMessage ($"   Loaded {designerNameAssembly}");
				} catch (Mono.Cecil.AssemblyResolutionException) {
					LogMessage ($"   Could not resolve assembly {DesignerAssemblyName}.");
				} catch (System.IO.FileNotFoundException) {
					LogMessage ($"   Assembly {DesignerAssemblyName} did not exist.");
				}
				if (designerAssembly == null) {
					return;
				}
				designerType = designerAssembly.MainModule.GetTypes ().FirstOrDefault (x => x.FullName == $"{DesignerAssemblyNamespace}.Resource");
				if (designerType == null) {
					LogMessage ($"   Did not find {DesignerAssemblyNamespace}.Resource type. It was probably linked out.");
					return;
				}
				lookup = BuildResourceDesignerPropertyLookup (designerType);
			} finally {
				designerLoaded = true;
			}
		}

		internal override bool ProcessAssemblyDesigner (AssemblyDefinition assembly)
		{
			if (!FindResourceDesigner (assembly, mainApplication: false, out TypeDefinition designer, out CustomAttribute designerAttribute)) {
				LogMessage ($"   {assembly.Name.Name} has no designer. ");
				return false;
			}

			LogMessage ($"   {assembly.Name.Name} has a designer. ");
			LogMessage ($"   BaseType: {designer.BaseType.FullName}. ");
			if (designer.BaseType.FullName == $"{DesignerAssemblyNamespace}.Resource") {
				LogMessage ($"   {assembly.Name.Name} has already been processed. ");
				return false;
			}

			// This is expected for the first call, in <LinkAssembliesNoShrink/>
			if (!designerLoaded)
				LoadDesigner ();

			if (designerAssembly == null || designerType == null) {
				LogMessage ($"   Not using {DesignerAssemblyName}");
				return false;
			}

			LogMessage ($"    Adding reference {designerAssembly.Name.Name}.");
			assembly.MainModule.AssemblyReferences.Add (designerAssembly.Name);
			var importedDesignerType = assembly.MainModule.ImportReference (designerType.Resolve ());

			LogMessage ($"    FixupAssemblyTypes {assembly.Name.Name}.");
			// now replace all ldsfld with a call to the property get_ method.
			FixupAssemblyTypes (assembly, designer);

			LogMessage ($"    ClearDesignerClass {assembly.Name.Name}.");
			// then clean out the designer.
			ClearDesignerClass (designer, completely: true);
			designer.BaseType = importedDesignerType;
			return true;
		}

		Dictionary<string, MethodDefinition> BuildResourceDesignerPropertyLookup (TypeDefinition type)
		{
			LogMessage ($"     Building Designer Lookups for {type.FullName}");
			var output = new Dictionary<string, MethodDefinition> (StringComparer.Ordinal);
			foreach (TypeDefinition definition in type.NestedTypes)
			{
				foreach (PropertyDefinition property in definition.Properties)
				{
					string key = $"{definition.Name}::{property.Name}";
					if (output.ContainsKey (key)) {
						LogMessage ($"          Found duplicate {key}");
					} else {
						output.Add (key, property.GetMethod);
					}
				}
			}
			return output;
		}

		protected override void FixBody (MethodBody body, TypeDefinition designer)
		{
			// replace
			// IL_0068: ldsfld int32 Xamarin.Forms.Platform.Android.Resource/Layout::Toolbar
			// with
			// call int32 Xamarin.Forms.Platform.Android.Resource/Layout::get_Toolbar()
			string designerFullName = $"{designer.FullName}/";
			var processor = body.GetILProcessor ();
			Dictionary<Instruction, Instruction> instructions = new Dictionary<Instruction, Instruction>();
			foreach (var i in body.Instructions)
			{
				if (i.OpCode != OpCodes.Ldsfld)
					continue;
				string line = i.ToString ();
				int idx = line.IndexOf (designerFullName, StringComparison.Ordinal);
				if (idx >= 0) {
					string key = line.Substring (idx + designerFullName.Length);
					LogMessage ($"Looking for {key}.");
					if (lookup.TryGetValue (key, out MethodDefinition method)) {
						var importedMethod = designer.Module.ImportReference (method);
						var newIn = Instruction.Create (OpCodes.Call, importedMethod);
						instructions.Add (i, newIn);
					} else {
						LogMessage ($"DEBUG! Failed to find {key}!");
						throw new InvalidOperationException ($"Failed to find AndroidResource for {key}!");
					}
				}
			}
			if (instructions.Count > 0)
				LogMessage ($"    Fixing up {body.Method.FullName}");
			foreach (var i in instructions)
			{
				LogMessage ($"      Replacing {i.Key}");
				LogMessage ($"      With {i.Value}");
				processor.Replace(i.Key, i.Value);
			}
		}
	}
}
