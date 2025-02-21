#nullable enable
using System;
using System.Linq;
using Java.Interop.Tools.Cecil;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Linker;
using Mono.Linker.Steps;
using Xamarin.Android.Tasks;

namespace MonoDroid.Tuner
{
	public class AddKeepAlivesStep : BaseStep
	{

		protected override void ProcessAssembly (AssemblyDefinition assembly)
		{
			var action = Annotations.HasAction (assembly) ? Annotations.GetAction (assembly) : AssemblyAction.Skip;
			if (action == AssemblyAction.Delete)
				return;

			if (AddKeepAlives (assembly)) {
				if (action == AssemblyAction.Skip || action == AssemblyAction.Copy)
					Annotations.SetAction (assembly, AssemblyAction.Save);
			}
		}

		internal bool AddKeepAlives (AssemblyDefinition assembly)
		{
			if (!assembly.MainModule.HasTypeReference ("Java.Lang.Object"))
				return false;

			// Anything that was built against .NET for Android will have
			// keep-alives already compiled in.
			if (MonoAndroidHelper.IsDotNetAndroidAssembly (assembly))
				return false;

			bool changed = false;
			foreach (TypeDefinition type in assembly.MainModule.Types)
				changed |= ProcessType (type);

			return changed;
		}

		bool ProcessType (TypeDefinition type)
		{
  			bool changed = false;
			if (MightNeedFix (type))
				changed |= AddKeepAlives (type);

			if (type.HasNestedTypes) {
				foreach (var t in type.NestedTypes) {
					changed |= ProcessType (t);
				}
			}

			return changed;
		}

		bool MightNeedFix (TypeDefinition type)
		{
			return !type.IsAbstract && type.IsSubclassOf ("Java.Lang.Object", Context);
		}

		MethodDefinition? methodKeepAlive = null;

		bool AddKeepAlives (TypeDefinition type)
		{
			bool changed = false;
			foreach (MethodDefinition method in type.Methods) {
				if (method.Parameters.Count == 0)
					continue;

				if (!method.CustomAttributes.Any (a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute"))
					continue;

				var instructions = method.Body.Instructions;

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

				var processor = method.Body.GetILProcessor ();
				var module = method.DeclaringType.Module;
				var end = instructions.Last ();
				if (end.Previous.OpCode == OpCodes.Endfinally)
					end = end.Previous;

				for (int i = 0; i < method.Parameters.Count; i++) {
					if (method.Parameters [i].ParameterType.IsValueType || method.Parameters [i].ParameterType.FullName == "System.String")
						continue;

					if (methodKeepAlive == null)
						methodKeepAlive = GetKeepAliveMethod ();

					if (methodKeepAlive == null) {
						LogMessage ("Unable to add KeepAlive call, did not find System.GC.KeepAlive method.");
						break;
					}

					processor.InsertBefore (end, GetLoadArgumentInstruction (method.IsStatic ? i : i + 1, method.Parameters [i]));
					processor.InsertBefore (end, Instruction.Create (OpCodes.Call, module.ImportReference (methodKeepAlive)));
					changed = true;
				}
			}
			return changed;
		}

		protected virtual AssemblyDefinition GetCorlibAssembly ()
		{
			return Context.GetAssembly ("System.Private.CoreLib");
		}

		MethodDefinition? GetKeepAliveMethod ()
		{
			var corlibAssembly = GetCorlibAssembly ();
			if (corlibAssembly == null)
				return null;

			var gcType = Extensions.GetType (corlibAssembly, "System.GC");
			if (gcType == null)
				return null;

			return Extensions.GetMethod (gcType, "KeepAlive", new string [] { "System.Object" });
		}

#if ILLINK
		public virtual void LogMessage (string message)
		{
			Context.LogMessage (message);
		}
#endif // ILLINK

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
