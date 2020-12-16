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
			if (assembly.Name.Name != "Mono.Android" && !assembly.MainModule.HasTypeReference ("Java.Lang.Object"))
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

		MethodDefinition methodKeepAlive = null;

		bool AddKeepAlives (TypeDefinition type)
		{
			bool changed = false;
			foreach (MethodDefinition method in type.Methods) {
				if (!method.CustomAttributes.Any (a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute"))
					continue;

				if (method.Parameters.Count == 0)
					continue;

				var processor = method.Body.GetILProcessor ();
				var module = method.DeclaringType.Module;
				var instructions = method.Body.Instructions;
				var end = instructions.Last ();
				if (end.Previous.OpCode == OpCodes.Endfinally)
					end = end.Previous;

				var found = false;
				for (int off = Math.Max (0, instructions.Count - 6); off < instructions.Count; off++) {
					var current = instructions [off];
					if (current.OpCode == OpCodes.Call && current.Operand.ToString ().Contains ("System.GC::KeepAlive")) {
						found = true;
						break;
					}
				}

				if (found)
					continue;

				for (int i = 0; i < method.Parameters.Count; i++) {
					if (method.Parameters [i].ParameterType.IsValueType || method.Parameters [i].ParameterType.FullName == "System.String")
						continue;

					changed = true;

					if (methodKeepAlive == null)
						methodKeepAlive = Context.GetMethod ("mscorlib", "System.GC", "KeepAlive", new string [] { "System.Object" });

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
