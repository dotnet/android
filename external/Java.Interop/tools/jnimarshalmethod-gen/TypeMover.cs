using System;
using System.IO;
using System.Collections.Generic;

using Mono.Cecil;
using Mono.Cecil.Cil;

using Java.Interop.Tools.Cecil;

namespace Xamarin.Android.Tools.JniMarshalMethodGenerator
{
	public class TypeMover
	{
		AssemblyDefinition Source { get; }
		AssemblyDefinition Destination { get; }
		Dictionary<string, System.Reflection.Emit.TypeBuilder> Types { get; }

		public TypeMover (AssemblyDefinition source, AssemblyDefinition destination, Dictionary<string, System.Reflection.Emit.TypeBuilder> types)
		{
			Source = source;
			Destination = destination;
			Types = types;
		}

		public void Move ()
		{
			foreach (var type in Types.Values)
				Move (type);

			var newName = $"{Path.Combine (Path.GetDirectoryName (Destination.MainModule.FileName), Path.GetFileNameWithoutExtension (Destination.MainModule.FileName))}-new{Path.GetExtension (Destination.MainModule.FileName)}";
			Destination.Write (newName, new WriterParameters () { WriteSymbols = true });

			App.ColorWriteLine ($"Wrote {newName} assembly", ConsoleColor.Cyan);
		}

		static readonly string nestedName = "__<$>_jni_marshal_methods";

		void Move (Type type)
		{
			var typeSrc = Source.MainModule.GetType (type.GetCecilName ());
			var typeDst = Destination.MainModule.GetType (type.GetCecilName ());
			var jniType = new TypeDefinition ("", nestedName, TypeAttributes.NestedPrivate | TypeAttributes.Sealed);

			if (App.Verbose) {
				Console.Write ($"Moving type ");
				App.ColorWrite ($"{typeSrc.FullName},{typeSrc.Module.FileName}", ConsoleColor.Yellow);
				Console.Write (" to ");
				App.ColorWriteLine ($"{Destination.MainModule.FileName}", ConsoleColor.Yellow);
			}

			jniType.BaseType = GetUpdatedType (typeSrc.BaseType, Destination.MainModule);
			typeDst.NestedTypes.Add (jniType);

			foreach (var m in typeSrc.Methods) {
				if (App.Verbose) {
					Console.Write ("Moving method ");
					App.ColorWriteLine ($"{m}", ConsoleColor.Green);
				}
				jniType.Methods.Add (Duplicate (m, Destination.MainModule, typeDst));
			}
		}

		static Dictionary<TypeReference, TypeReference> typeMap = new Dictionary<TypeReference, TypeReference> ();
		static Dictionary<TypeReference, TypeDefinition> resolvedTypeMap = new Dictionary<TypeReference, TypeDefinition> ();

		static TypeDefinition Resolve (TypeReference type)
		{
			if (resolvedTypeMap.ContainsKey (type))
				return resolvedTypeMap [type];

			var resolved = type.Resolve ();

			resolvedTypeMap [type] = resolved;

			return resolved;
		}

		static TypeReference GetUpdatedType (TypeReference type, ModuleDefinition module)
		{
			if (typeMap.ContainsKey (type))
				return typeMap [type];

			if (type is GenericInstanceType giType)
				return GetUpdatedGenericType (giType, module);

			var td = Resolve (type);

			var tr = td.Module.FileName != module.FileName
				? module.ImportReference (type)
				: module.GetType (td.FullName);

			if (type.IsArray)
				tr = new ArrayType (tr);

			typeMap [type] = tr;

			return tr;
		}

		static TypeReference GetUpdatedGenericType (GenericInstanceType type, ModuleDefinition module)
		{
			if (typeMap.ContainsKey (type))
				return typeMap [type];

			var td = Resolve (type);
			var newType = new GenericInstanceType (GetUpdatedType (td, module));

			if (type.HasGenericArguments)
				foreach (var ga in type.GenericArguments)
					newType.GenericArguments.Add (GetUpdatedType (ga, module));

			if (type.HasGenericParameters)
				foreach (var gp in type.GenericParameters)
					newType.GenericParameters.Add (gp);

			var tr = td.Module.FileName != module.FileName
				? module.ImportReference (newType)
				: module.GetType (newType.FullName);

			if (type.IsArray)
				tr = new ArrayType (tr);

			typeMap [type] = tr;

			return tr;
		}


		static Dictionary<MethodReference, MethodReference> methodMap = new Dictionary<MethodReference, MethodReference> ();
		static Dictionary<MethodReference, MethodDefinition> resolvedMethodMap = new Dictionary<MethodReference, MethodDefinition> ();

		static MethodDefinition ResolveMethod (MethodReference method)
		{
			if (resolvedMethodMap.ContainsKey (method))
				return resolvedMethodMap [method];

			var resolved = method.Resolve ();

			resolvedMethodMap [method] = resolved;

			return resolved;
		}

		static MethodReference GetUpdatedMethod (MethodReference method, ModuleDefinition module)
		{
			if (methodMap.ContainsKey (method))
				return methodMap [method];

			MethodDefinition md = ResolveMethod (method.IsGenericInstance ? (method as GenericInstanceMethod).ElementMethod : method);
			MethodReference mr;

			if (method.IsGenericInstance) {
				var newGenericMethod = new GenericInstanceMethod (md);

				var genericInstance = method as GenericInstanceMethod;
				if (genericInstance.HasGenericArguments)
					foreach (var ga in genericInstance.GenericArguments)
						newGenericMethod.GenericArguments.Add (GetUpdatedType (ga, module));

				mr = module.ImportReference (newGenericMethod);
			} else
				mr = module.ImportReference (md.Module != null && md.Module.FileName == module.FileName ? md : method);

			foreach (var p in mr.Parameters)
				p.ParameterType = GetUpdatedType (p.ParameterType, module);

			methodMap [method] = mr;

			return mr;
		}

		static FieldReference GetUpdatedField (FieldReference fr, ModuleDefinition module)
		{
			FieldReference newField = new FieldReference (fr.Name, GetUpdatedType (fr.FieldType, module));
			newField.DeclaringType = GetUpdatedType (fr.DeclaringType, module);

			return newField;
		}

		static Instruction GetUpdatedInstruction (Instruction il, TypeDefinition type, MethodDefinition method, ModuleDefinition module)
		{
			if (il.Operand == null)
				return Instruction.Create (il.OpCode);

			var typeName = type.FullName.Replace ('/', '+');
			if (method.Name == "__RegisterNativeMembers" && il.OpCode == OpCodes.Ldstr && il.Operand is string opStr && opStr != null && opStr == typeName)
				il.Operand = $"{typeName}+{nestedName}";

			if (il.Operand is MethodReference mr)
				return Instruction.Create (il.OpCode, GetUpdatedMethod (mr, module));

			if (il.Operand is GenericInstanceType giType)
				return Instruction.Create (il.OpCode, GetUpdatedGenericType (giType, module));

			if (il.Operand is TypeReference tr)
				return Instruction.Create (il.OpCode, GetUpdatedType (tr, module));

			if (il.Operand is FieldReference fr)
				return Instruction.Create (il.OpCode, GetUpdatedField (fr, module));

			return il;
		}

		static ExceptionHandler GetUpdatedExceptionHandler (ExceptionHandler eh, Dictionary<Instruction, Instruction> instructionMap, ModuleDefinition module)
		{
			var handler = new ExceptionHandler (eh.HandlerType);

			if (handler.CatchType != null)
				handler.CatchType = GetUpdatedType (eh.CatchType, module);

			if (eh.TryStart != null)
				handler.TryStart = instructionMap [eh.TryStart];

			if (eh.TryEnd != null)
				handler.TryEnd = instructionMap [eh.TryEnd];

			if (eh.FilterStart != null)
				handler.FilterStart = instructionMap [eh.FilterStart];

			if (eh.HandlerStart != null)
				handler.HandlerStart = instructionMap [eh.HandlerStart];

			if (eh.HandlerEnd != null)
				handler.HandlerEnd = instructionMap [eh.HandlerEnd];

			return handler;
		}

		static MethodDefinition Duplicate (MethodDefinition src, ModuleDefinition module, TypeDefinition type)
		{
			var md = new MethodDefinition (src.Name, src.Attributes, GetUpdatedType (src.ReturnType, module));

			if (src.HasCustomAttributes)
				foreach (var ca in src.CustomAttributes)
					md.CustomAttributes.Add (new CustomAttribute (GetUpdatedMethod (ca.Constructor, module), ca.GetBlob ()));

			foreach (var p in src.Parameters)
				md.Parameters.Add (new ParameterDefinition (p.Name, p.Attributes, GetUpdatedType (p.ParameterType, module)));

			md.Body.InitLocals = src.Body.InitLocals;

			if (src.Body.HasVariables)
				foreach (var v in src.Body.Variables)
					md.Body.Variables.Add (new VariableDefinition (GetUpdatedType (v.VariableType, module)));

			var instructionMap = new Dictionary<Instruction, Instruction> ();
			var instructions = src.Body.Instructions;
			var newInstructions = md.Body.Instructions;
			var count = instructions.Count;
			for (int i = 0; i < count; i++) {
				var il = instructions [i];
				Instruction newInstruction = GetUpdatedInstruction (il, type, src, module);
				newInstructions.Add (newInstruction);
				instructionMap [il] = newInstruction;
			}

			if (src.Body.HasExceptionHandlers)
				foreach (var eh in src.Body.ExceptionHandlers)
					md.Body.ExceptionHandlers.Add (GetUpdatedExceptionHandler (eh, instructionMap, module));

			md.Body.MaxStackSize = src.Body.MaxStackSize;

			return md;
		}
	}
}
