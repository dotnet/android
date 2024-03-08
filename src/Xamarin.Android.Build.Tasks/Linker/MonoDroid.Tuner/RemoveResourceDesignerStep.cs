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

namespace MonoDroid.Tuner
{
	public class RemoveResourceDesignerStep : LinkDesignerBase
	{
		TypeDefinition mainDesigner = null;
		AssemblyDefinition mainAssembly = null;
		CustomAttribute mainDesignerAttribute;
		Dictionary<string, int> designerConstants;
		Regex opCodeRegex = new Regex (@"([\w]+): ([\w]+) ([\w.]+) ([\w:./]+)");

#if !ILLINK
		public RemoveResourceDesignerStep (IMetadataResolver cache) : base (cache) { }
#endif

		protected override void LoadDesigner ()
		{
			if (mainAssembly != null)
				return;
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
				LogMessage ($"  Main Designer not found.");
				return;
			}
			LogMessage ($"  Main Designer found {mainDesigner.FullName}.");
			designerConstants = BuildResourceDesignerFieldLookup (mainDesigner);
		}

		protected override void EndProcess ()
		{
			if (mainDesigner != null) {
				LogMessage ($"  Setting Action on {mainAssembly.Name} to Save.");
				Annotations.SetAction (mainAssembly, AssemblyAction.Save);
			}
		}

		protected override void FixBody (MethodBody body, TypeDefinition designer)
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
				LogMessage ($"    Fixing up {body.Method.FullName}");
			foreach (var i in instructions)
			{
				var newCode = Extensions.CreateLoadArraySizeOrOffsetInstruction (i.Value);
				LogMessage ($"      Replacing {i.Key}");
				LogMessage ($"      With {newCode}");
				processor.Replace(i.Key, newCode);
			}
		}

		internal override bool ProcessAssemblyDesigner (AssemblyDefinition assembly)
		{
			if (mainDesigner == null)
				return false;
			var fileName = assembly.Name.Name + ".dll";
			if (MonoAndroidHelper.IsFrameworkAssembly (fileName))
				return false;

			LogMessage ($"  Fixing up {assembly.Name.Name}");
			TypeDefinition localDesigner = null;
			CustomAttribute designerAttribute;
			if (assembly != mainAssembly) {
				LogMessage ($"   {assembly.Name.Name} is not the main assembly. ");
				if (!FindResourceDesigner (assembly, mainApplication: false, designer: out localDesigner, designerAttribute: out designerAttribute)) {
					Context.LogMessage ($"   {assembly.Name.Name} does not have a designer file.");
					return false;
				}
			} else {
				LogMessage ($"   {assembly.Name.Name} is the main assembly. ");
				localDesigner = mainDesigner;
				designerAttribute = mainDesignerAttribute;
			}

			LogMessage ($"   {assembly.Name.Name} has designer {localDesigner.FullName}.");

			FixupAssemblyTypes (assembly, localDesigner);

			ClearDesignerClass (localDesigner);
			if (designerAttribute != null) {
				assembly.CustomAttributes.Remove (designerAttribute);
			}
			return true;
		}
	}
}
