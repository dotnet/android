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

		MethodReference consoleWriteLine;

		public TypeMover (AssemblyDefinition source, AssemblyDefinition destination, Dictionary<string, System.Reflection.Emit.TypeBuilder> types, DirectoryAssemblyResolver resolver)
		{
			Source = source;
			Destination = destination;
			Types = types;

			if (App.Debug) {
				consoleWriteLine = GetSingleParameterMethod (resolver, Destination.MainModule, "mscorlib", "System.Console", "WriteLine", "System.String");
				if (consoleWriteLine == null) {
					App.Warning ("Unable to find System.Console::WriteLine method. Disabling debug injection");
					App.Debug = false;
				}
			}
		}

		public void Move ()
		{
			foreach (var type in Types.Values)
				Move (type);

			var newName = $"{Path.Combine (Path.GetDirectoryName (Destination.MainModule.FileName), Path.GetFileNameWithoutExtension (Destination.MainModule.FileName))}-new{Path.GetExtension (Destination.MainModule.FileName)}";
			Destination.Write (newName, new WriterParameters () { WriteSymbols = true });

			if (App.Verbose)
				App.ColorWriteLine ($"Wrote {newName} assembly", ConsoleColor.Cyan);
		}

		static readonly string nestedName = "__<$>_jni_marshal_methods";

		Dictionary<string, MethodReference> newHelperMethods;

		bool TypeIsEmptyOrHasOnlyDefaultConstructor (TypeDefinition type)
		{
			return !type.HasMethods || (type.Methods.Count == 1 && type.Methods [0].IsConstructor);
		}

		void Move (Type type)
		{
			var typeSrc = Source.MainModule.GetType (type.GetCecilName ());
			var typeDst = Destination.MainModule.GetType (type.GetCecilName ());
			var jniType = new TypeDefinition ("", nestedName, TypeAttributes.NestedPrivate | TypeAttributes.Sealed);

			if (TypeIsEmptyOrHasOnlyDefaultConstructor (typeSrc))
				return;

			if (App.Verbose) {
				Console.Write ($"Moving type ");
				App.ColorWrite ($"{typeSrc.FullName},{typeSrc.Module.FileName}", ConsoleColor.Yellow);
				Console.Write (" to ");
				App.ColorWriteLine ($"{Destination.MainModule.FileName}", ConsoleColor.Yellow);
			}

			jniType.BaseType = GetUpdatedType (typeSrc.BaseType, Destination.MainModule);
			typeDst.NestedTypes.Add (jniType);


			newHelperMethods = new Dictionary<string, MethodReference> ();

			foreach (var m in typeSrc.Methods) {
				if (m.Name == "__RegisterNativeMembers")
					continue;
				var newMethod = Duplicate (m, Destination.MainModule, typeDst);
				AddMethod (jniType, newMethod);

				newHelperMethods [newMethod.Name] = newMethod;
			}

			foreach (var m in typeSrc.Methods) {
				if (m.Name != "__RegisterNativeMembers")
					continue;

				AddMethod (jniType, Duplicate (m, Destination.MainModule, typeDst));
			}
		}

		void AddMethod (TypeDefinition type, MethodDefinition method)
		{
			type.Methods.Add (method);

			if (App.Verbose) {
				Console.Write ("Moved method ");
				App.ColorWriteLine ($"{method}", ConsoleColor.Green);
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

		Instruction GetUpdatedInstruction (Instruction il, ModuleDefinition module)
		{
			if (il.Operand == null)
				return Instruction.Create (il.OpCode);

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

		MethodReference GetActionConstructor (TypeReference type, ModuleDefinition module)
		{
			var td = Resolve (type);
			if (!td.HasMethods)
				return null;

			foreach (var m in td.Methods) {
				if (m.IsConstructor && m.HasParameters && m.Parameters.Count == 2 && m.Parameters [0].ParameterType.FullName == "System.Object" && m.Parameters [1].ParameterType.FullName == "System.IntPtr") {
					var mr = GetUpdatedMethod (m, module);
					if (type is GenericInstanceType)
						mr.DeclaringType = type;
					return mr;
				}
			}
			return null;
		}

		bool AnalyzeAndImprove (Mono.Collections.Generic.Collection<Instruction> instructions, Mono.Collections.Generic.Collection<Instruction> newInstructions, int idx, string typeName, out int skipCount, ModuleDefinition module)
		{
			var idxStart = idx;
			var il = instructions [idx++];

			skipCount = 0;

			if (il.OpCode == OpCodes.Ldstr && il.Operand is string opStr && opStr != null && opStr == typeName) {
				il = instructions [idx++];
				if (il.OpCode != OpCodes.Call || !(il.Operand is MethodReference opMR) || opMR.Name != "GetType" || opMR.DeclaringType.FullName != "System.Type")
					return false;

				il = instructions [idx++];
				if (il.OpCode != OpCodes.Stloc_0)
					return false;

				skipCount = 2;

				if (App.Debug) {
					newInstructions.Add (Instruction.Create (OpCodes.Ldstr, $"Registering JNI marshal methods in {opStr}"));
					newInstructions.Add (Instruction.Create (OpCodes.Call, consoleWriteLine));
				}

				return true;
			}

			if (il.OpCode == OpCodes.Dup && instructions.Count > idxStart + 10) {
				il = instructions [idx++];
				if (!il.OpCode.ToString ().StartsWith ("ldc.i4.", StringComparison.InvariantCulture))
					return false;

				il = instructions [idx++];
				if (il.OpCode != OpCodes.Ldstr)
					return false;

				il = instructions [idx++];
				if (il.OpCode != OpCodes.Ldstr)
					return false;

				il = instructions [idx++];
				if (il.OpCode != OpCodes.Ldtoken || !(il.Operand is TypeReference))
					return false;

				var delegateType = GetUpdatedType (il.Operand as TypeReference, module);
				var constructor = GetActionConstructor (delegateType, module);
				if (constructor == null)
					return false;

				il = instructions [idx++];
				if (il.OpCode != OpCodes.Call || !(il.Operand is MethodReference opMR2) || opMR2.Name != "GetTypeFromHandle")
					return false;

				il = instructions [idx++];
				if (il.OpCode != OpCodes.Ldloc_0)
					return false;

				il = instructions [idx++];
				if (il.OpCode != OpCodes.Ldstr)
					return false;

				var methodName = il.Operand as string;
				if (string.IsNullOrEmpty (methodName))
					return false;

				il = instructions [idx++];
				if (il.OpCode != OpCodes.Call || !(il.Operand is MethodReference opMR3) || opMR3.Name != "CreateDelegate")
					return false;

				il = instructions [idx++];
				if (il.OpCode != OpCodes.Newobj)
					return false;

				il = instructions [idx++];
				if (il.OpCode != OpCodes.Stelem_Any)
					return false;

				idx = idxStart;
				for (int i = 0; i < 4; i++)
					newInstructions.Add (GetUpdatedInstruction (instructions [idx++], module));

				newInstructions.Add (Instruction.Create (OpCodes.Ldnull));
				newInstructions.Add (Instruction.Create (OpCodes.Ldftn, newHelperMethods?[methodName]));
				newInstructions.Add (Instruction.Create (OpCodes.Newobj, constructor));

				idx += 5;
				for (int i = 0; i < 2; i++)
					newInstructions.Add (GetUpdatedInstruction (instructions [idx++], module));

				skipCount = 10;
				return true;
			}

			return false;
		}

		MethodDefinition Duplicate (MethodDefinition src, ModuleDefinition module, TypeDefinition type)
		{
			var md = new MethodDefinition (src.Name, src.Attributes, GetUpdatedType (src.ReturnType, module));

			if (src.HasCustomAttributes)
				foreach (var ca in src.CustomAttributes)
					md.CustomAttributes.Add (new CustomAttribute (GetUpdatedMethod (ca.Constructor, module), ca.GetBlob ()));

			foreach (var p in src.Parameters)
				md.Parameters.Add (new ParameterDefinition (p.Name, p.Attributes, GetUpdatedType (p.ParameterType, module)));

			md.Body.InitLocals = src.Body.InitLocals;

			var instructionMap = new Dictionary<Instruction, Instruction> ();
			var instructions = src.Body.Instructions;
			var newInstructions = md.Body.Instructions;
			var count = instructions.Count;
			var typeName = type.FullName.Replace ('/', '+');
			var isRegisterMethod = src.Name == "__RegisterNativeMembers";
			int skipCount;
			int improvements = 0;

			if (src.Body.HasVariables)
				foreach (var v in src.Body.Variables)
					if (!isRegisterMethod || v.VariableType.FullName != "System.Type")
						md.Body.Variables.Add (new VariableDefinition (GetUpdatedType (v.VariableType, module)));

			for (int i = 0; i < count; i++) {
				var il = instructions [i];

				if (isRegisterMethod && AnalyzeAndImprove (instructions, newInstructions, i, typeName, out skipCount, module)) {
					i += skipCount;
					improvements++;
				} else {
					Instruction newInstruction = GetUpdatedInstruction (il, module);
					newInstructions.Add (newInstruction);
					instructionMap [il] = newInstruction;
				}
			}

			if (isRegisterMethod && improvements < 2)
				App.Warning ($"Method {md} was not improved. There should have been at least 2 improvements in this registration method.");

			if (src.Body.HasExceptionHandlers)
				foreach (var eh in src.Body.ExceptionHandlers)
					md.Body.ExceptionHandlers.Add (GetUpdatedExceptionHandler (eh, instructionMap, module));

			md.Body.MaxStackSize = src.Body.MaxStackSize;

			return md;
		}

		MethodReference GetSingleParameterMethod (DirectoryAssemblyResolver resolver, ModuleDefinition module, string assemblyName, string typeName, string methodName, string parameterTypeName)
		{
			var assembly = resolver.Resolve (assemblyName);
			if (assembly == null)
				return null;

			var typeTD = assembly.MainModule.GetType (typeName);
			if (typeTD == null)
				return null;

			foreach (var md in typeTD.Methods)
				if (md.Name == methodName && md.HasParameters && md.Parameters.Count == 1 && md.Parameters [0].ParameterType.FullName == parameterTypeName)
					return GetUpdatedMethod (md, module);

			return null;
		}
	}
}
