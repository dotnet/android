using System;
using System.Linq;
using Java.Interop.Tools.Cecil;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xamarin.Android.Tasks;

namespace MonoDroid.Tuner
{
	static class AddKeepAlivesHelper
	{
		internal static bool AddKeepAlives (AssemblyDefinition assembly, IMetadataResolver resolver, Func<AssemblyDefinition?> getCorlibAssembly, Action<string> logMessage)
		{
			if (!assembly.MainModule.HasTypeReference ("Java.Lang.Object"))
				return false;

			// Anything that was built against .NET for Android will have
			// keep-alives already compiled in.
			if (MonoAndroidHelper.IsDotNetAndroidAssembly (assembly))
				return false;

			MethodDefinition? methodKeepAlive = null;
			bool changed = false;
			foreach (TypeDefinition type in assembly.MainModule.Types)
				changed |= ProcessType (type, resolver, ref methodKeepAlive, getCorlibAssembly, logMessage);

			return changed;
		}

		static bool ProcessType (TypeDefinition type, IMetadataResolver resolver, ref MethodDefinition? methodKeepAlive, Func<AssemblyDefinition?> getCorlibAssembly, Action<string> logMessage)
		{
			bool changed = false;
			if (MightNeedFix (type, resolver))
				changed |= AddKeepAlives (type, ref methodKeepAlive, getCorlibAssembly, logMessage);

			if (type.HasNestedTypes) {
				foreach (var t in type.NestedTypes) {
					changed |= ProcessType (t, resolver, ref methodKeepAlive, getCorlibAssembly, logMessage);
				}
			}

			return changed;
		}

		static bool MightNeedFix (TypeDefinition type, IMetadataResolver resolver)
		{
			return !type.IsAbstract && type.IsSubclassOf ("Java.Lang.Object", resolver);
		}

		static bool AddKeepAlives (TypeDefinition type, ref MethodDefinition? methodKeepAlive, Func<AssemblyDefinition?> getCorlibAssembly, Action<string> logMessage)
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
						methodKeepAlive = GetKeepAliveMethod (getCorlibAssembly, logMessage);

					if (methodKeepAlive == null) {
						logMessage ("Unable to add KeepAlive call, did not find System.GC.KeepAlive method.");
						break;
					}

					processor.InsertBefore (end, GetLoadArgumentInstruction (method.IsStatic ? i : i + 1, method.Parameters [i]));
					processor.InsertBefore (end, Instruction.Create (OpCodes.Call, module.ImportReference (methodKeepAlive)));
					changed = true;
				}
			}
			return changed;
		}

		static MethodDefinition? GetKeepAliveMethod (Func<AssemblyDefinition?> getCorlibAssembly, Action<string> logMessage)
		{
			var corlibAssembly = getCorlibAssembly ();
			if (corlibAssembly == null)
				return null;

			var gcType = Extensions.GetType (corlibAssembly, "System.GC");
			if (gcType == null)
				return null;

			return Extensions.GetMethod (gcType, "KeepAlive", new string [] { "System.Object" });
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
