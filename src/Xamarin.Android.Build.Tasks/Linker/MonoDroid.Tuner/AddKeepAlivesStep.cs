using System;
using System.Collections.Generic;
using System.Linq;

using Mono.Cecil;

using Java.Interop.Tools.Cecil;

using Mono.Linker;
using Mono.Linker.Steps;

using Mono.Cecil.Cil;

namespace MonoDroid.Tuner
{
	public class AddKeepAlivesStep : BaseStep
	{
		readonly TypeDefinitionCache cache;

		public AddKeepAlivesStep (TypeDefinitionCache cache)
		{
			this.cache = cache;
		}

		protected override void ProcessAssembly (AssemblyDefinition assembly)
		{
			if (AddKeepAlives (assembly)) {
				AssemblyAction action = Annotations.HasAction (assembly) ? Annotations.GetAction (assembly) : AssemblyAction.Skip;
				if (action == AssemblyAction.Skip || action == AssemblyAction.Copy || action == AssemblyAction.Delete)
					Annotations.SetAction (assembly, AssemblyAction.Save);
			}
		}

		internal bool AddKeepAlives (AssemblyDefinition assembly)
		{
			if (assembly.MainModule.HasTypeReference ("Java.Lang.Object"))
				return false;
			bool changed = false;
			List<TypeDefinition> types = assembly.MainModule.Types.ToList ();
			foreach (TypeDefinition type in assembly.MainModule.Types)
				AddNestedTypes (types, type);
			foreach (TypeDefinition type in types)
				if (MightNeedFix (type))
					changed |= AddKeepAlives (type);
			return changed;
		}

		// Adapted from `MarkJavaObjects`
		static void AddNestedTypes (List<TypeDefinition> types, TypeDefinition type)
		{
			if (!type.HasNestedTypes)
				return;

			foreach (var t in type.NestedTypes) {
				types.Add (t);
				AddNestedTypes (types, t);
			}
		}

		bool MightNeedFix (TypeDefinition type)
		{
			return !type.IsAbstract && type.IsSubclassOf ("Java.Lang.Object", cache);
		}

		bool AddKeepAlives (TypeDefinition type)
		{
			bool changed = false;
			foreach (MethodDefinition method in type.Methods) {
				if (!method.CustomAttributes.Any (a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute"))
					continue;
				ILProcessor processor = method.Body.GetILProcessor ();
				ModuleDefinition module = method.DeclaringType.Module;
				MethodDefinition methodKeepAlive = Context.GetMethod ("mscorlib", "System.GC", "KeepAlive", new string [] { "System.Object" });
				Instruction end = method.Body.Instructions.Last ();
				if (end.Previous.OpCode == OpCodes.Endfinally)
					end = end.Previous;
				for (int i = 0; i < method.Parameters.Count; i++) {
					if (method.Parameters [i].ParameterType.IsValueType)
						continue;
					changed = true;
					processor.InsertBefore (end, GetLoadArgumentInstruction (method.IsStatic ? i : i + 1, method.Parameters [i]));
					processor.InsertBefore (end, Instruction.Create (OpCodes.Call, module.ImportReference (methodKeepAlive)));
				}
			}
			return changed;
		}

		// Adapted from src/Mono.Android.Export/Mono.CodeGeneration/CodeArgumentReference.cs
		static Instruction GetLoadArgumentInstruction (int argNum, ParameterDefinition parameter)
		{
			switch (argNum) {
				case 0: return Instruction.Create (OpCodes.Ldarg_0);
				case 1: return Instruction.Create (OpCodes.Ldarg_1);
				case 2: return Instruction.Create (OpCodes.Ldarg_2);
				case 3: return Instruction.Create (OpCodes.Ldarg_3);
				default: return Instruction.Create (OpCodes.Ldarg, parameter);
			}
		}
	}
}
