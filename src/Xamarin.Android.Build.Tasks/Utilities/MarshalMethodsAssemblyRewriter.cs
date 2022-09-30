using System;
using System.Collections.Generic;
using System.IO;

using Java.Interop.Tools.Cecil;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Xamarin.Android.Tasks
{
	class MarshalMethodsAssemblyRewriter
	{
		sealed class AssemblyImports
		{
			public MethodReference MonoUnhandledExceptionMethod;
			public TypeReference   SystemException;
			public MethodReference UnhandledExceptionMethod;
			public CustomAttribute UnmanagedCallersOnlyAttribute;
			public MethodReference WaitForBridgeProcessingMethod;
		}

		IDictionary<string, IList<MarshalMethodEntry>> methods;
		ICollection<AssemblyDefinition> uniqueAssemblies;
		IDictionary <string, HashSet<string>> assemblyPaths;
		TaskLoggingHelper log;

		public MarshalMethodsAssemblyRewriter (IDictionary<string, IList<MarshalMethodEntry>> methods, ICollection<AssemblyDefinition> uniqueAssemblies, IDictionary <string, HashSet<string>> assemblyPaths, TaskLoggingHelper log)
		{
			this.methods = methods ?? throw new ArgumentNullException (nameof (methods));
			this.uniqueAssemblies = uniqueAssemblies ?? throw new ArgumentNullException (nameof (uniqueAssemblies));
			this.assemblyPaths = assemblyPaths ?? throw new ArgumentNullException (nameof (assemblyPaths));
			this.log = log ?? throw new ArgumentNullException (nameof (log));
		}

		// TODO: do away with broken exception transitions, there's no point in supporting them
		public void Rewrite (DirectoryAssemblyResolver resolver, List<string> targetAssemblyPaths, bool brokenExceptionTransitions)
		{
			if (resolver == null) {
				throw new ArgumentNullException (nameof (resolver));
			}

			if (targetAssemblyPaths == null) {
				throw new ArgumentNullException (nameof (targetAssemblyPaths));
			}

			if (targetAssemblyPaths.Count == 0) {
				throw new ArgumentException ("must contain at least one target path", nameof (targetAssemblyPaths));
			}

			AssemblyDefinition? monoAndroidRuntime = resolver.Resolve ("Mono.Android.Runtime");
			if (monoAndroidRuntime == null) {
				throw new InvalidOperationException ($"Internal error: unable to load the Mono.Android.Runtime assembly");
			}

			TypeDefinition runtime = FindType (monoAndroidRuntime, "Android.Runtime.AndroidRuntimeInternal", required: true)!;
			MethodDefinition waitForBridgeProcessingMethod = FindMethod (runtime, "WaitForBridgeProcessing", required: true)!;

			TypeDefinition androidEnvironment = FindType (monoAndroidRuntime, "Android.Runtime.AndroidEnvironmentInternal", required: true)!;
			MethodDefinition unhandledExceptionMethod = FindMethod (androidEnvironment, "UnhandledException", required: true)!;

			TypeDefinition runtimeNativeMethods = FindType (monoAndroidRuntime, "Android.Runtime.RuntimeNativeMethods", required: true);
			MethodDefinition monoUnhandledExceptionMethod = FindMethod (runtimeNativeMethods, "monodroid_debugger_unhandled_exception", required: true);

			AssemblyDefinition corlib = resolver.Resolve ("System.Private.CoreLib");
			TypeDefinition systemException = FindType (corlib, "System.Exception", required: true);

			MethodDefinition unmanagedCallersOnlyAttributeCtor = GetUnmanagedCallersOnlyAttributeConstructor (resolver);
			var assemblyImports = new Dictionary<AssemblyDefinition, AssemblyImports> ();
			foreach (AssemblyDefinition asm in uniqueAssemblies) {
				var imports = new AssemblyImports {
					MonoUnhandledExceptionMethod  = asm.MainModule.ImportReference (monoUnhandledExceptionMethod),
					SystemException               = asm.MainModule.ImportReference (systemException),
					UnhandledExceptionMethod      = asm.MainModule.ImportReference (unhandledExceptionMethod),
					UnmanagedCallersOnlyAttribute = CreateImportedUnmanagedCallersOnlyAttribute (asm, unmanagedCallersOnlyAttributeCtor),
					WaitForBridgeProcessingMethod = asm.MainModule.ImportReference (waitForBridgeProcessingMethod),
				};

				assemblyImports.Add (asm, imports);
			}

			log.LogDebugMessage ("Rewriting assemblies for marshal methods support");

			var processedMethods = new Dictionary<string, MethodDefinition> (StringComparer.Ordinal);
			foreach (IList<MarshalMethodEntry> methodList in methods.Values) {
				foreach (MarshalMethodEntry method in methodList) {
					string fullNativeCallbackName = method.NativeCallback.FullName;
					if (processedMethods.TryGetValue (fullNativeCallbackName, out MethodDefinition nativeCallbackWrapper)) {
						method.NativeCallbackWrapper = nativeCallbackWrapper;
						continue;
					}

					method.NativeCallbackWrapper = GenerateWrapper (method, assemblyImports, brokenExceptionTransitions);
					if (method.Connector != null) {
						if (method.Connector.IsStatic && method.Connector.IsPrivate) {
							log.LogDebugMessage ($"Removing connector method {method.Connector.FullName}");
							method.Connector.DeclaringType?.Methods?.Remove (method.Connector);
						} else {
							log.LogWarning ($"NOT removing connector method {method.Connector.FullName} because it's either not static or not private");
						}
					}

					if (method.CallbackField != null) {
						if (method.CallbackField.IsStatic && method.CallbackField.IsPrivate) {
							log.LogDebugMessage ($"Removing callback delegate backing field {method.CallbackField.FullName}");
							method.CallbackField.DeclaringType?.Fields?.Remove (method.CallbackField);
						} else {
							log.LogWarning ($"NOT removing callback field {method.CallbackField.FullName} because it's either not static or not private");
						}
					}

					processedMethods.Add (fullNativeCallbackName, method.NativeCallback);
				}
			}

			var newAssemblyPaths = new List<string> ();
			foreach (AssemblyDefinition asm in uniqueAssemblies) {
				foreach (string path in GetAssemblyPaths (asm)) {
					var writerParams = new WriterParameters {
						WriteSymbols = (File.Exists (path + ".mdb") || File.Exists (Path.ChangeExtension (path, ".pdb"))),
					};


					string output = $"{path}.new";
					log.LogDebugMessage ($"Writing new version of assembly: {output}");

					// TODO: this should be used eventually, but it requires that all the types are reloaded from the assemblies before typemaps are generated
					// since Cecil doesn't update the MVID in the already loaded types
					//asm.MainModule.Mvid = Guid.NewGuid ();
					asm.Write (output, writerParams);
					newAssemblyPaths.Add (output);
				}
			}

			// Replace old versions of the assemblies only after we've finished rewriting without issues, otherwise leave the new
			// versions around.
			foreach (string path in newAssemblyPaths) {
				string? pdb = null;
				string? mdb = null;

				string source = Path.ChangeExtension (Path.Combine (Path.GetDirectoryName (path), Path.GetFileNameWithoutExtension (path)), ".pdb");
				if (File.Exists (source)) {
					pdb = source;
				}

				source = $"{path}.mdb";
				if (File.Exists (source)) {
					mdb = source;
				}

				foreach (string targetPath in targetAssemblyPaths) {
					string target = Path.Combine (targetPath, Path.GetFileNameWithoutExtension (path));
					CopyFile (path, target);

					if (!String.IsNullOrEmpty (pdb)) {
						target = Path.ChangeExtension (Path.Combine (targetPath, Path.GetFileNameWithoutExtension (pdb)), ".pdb");
						CopyFile (pdb, target);
					}

					if (!String.IsNullOrEmpty (mdb)) {
						target = Path.Combine (targetPath, Path.ChangeExtension (Path.GetFileName (path), ".mdb"));
						CopyFile (mdb, target);
					}
				}

				RemoveFile (path);
				RemoveFile (pdb);
				RemoveFile (mdb);
			}

			void CopyFile (string source, string target)
			{
				log.LogDebugMessage ($"Copying rewritten assembly: {source} -> {target}");
				File.Copy (source, target, true);
			}

			void RemoveFile (string? path)
			{
				if (String.IsNullOrEmpty (path) || !File.Exists (path)) {
					return;
				}

				try {
					File.Delete (path);
				} catch (Exception ex) {
					log.LogWarning ($"Unable to delete source file '{path}'");
					log.LogDebugMessage (ex.ToString ());
				}
			}
		}

		MethodDefinition GenerateWrapper (MarshalMethodEntry method, Dictionary<AssemblyDefinition, AssemblyImports> assemblyImports, bool brokenExceptionTransitions)
		{
			MethodDefinition callback = method.NativeCallback;
			AssemblyImports imports = assemblyImports [callback.Module.Assembly];
			string wrapperName = $"{callback.Name}_mm_wrapper";
			TypeReference retType = MapToBlittableTypeIfNecessary (callback.ReturnType, out bool returnTypeMapped);
			bool hasReturnValue = String.Compare ("System.Void", callback.ReturnType.FullName, StringComparison.Ordinal) != 0;
			var wrapperMethod = new MethodDefinition (wrapperName, callback.Attributes, retType);

			callback.DeclaringType.Methods.Add (wrapperMethod);
			wrapperMethod.CustomAttributes.Add (imports.UnmanagedCallersOnlyAttribute);

			MethodBody body = wrapperMethod.Body;
			body.InitLocals = true;

			VariableDefinition? retval = null;
			if (hasReturnValue) {
				retval = new VariableDefinition (retType);
				body.Variables.Add (retval);
			}

			body.Instructions.Add (Instruction.Create (OpCodes.Call, imports.WaitForBridgeProcessingMethod));
			var exceptionHandler = new ExceptionHandler (ExceptionHandlerType.Catch) {
				CatchType = imports.SystemException,
			};

			body.ExceptionHandlers.Add (exceptionHandler);

			Instruction? firstTryInstruction = null;
			Instruction? inst = null;
			uint nparam = 0;
			foreach (ParameterDefinition pdef in callback.Parameters) {
				TypeReference newType = MapToBlittableTypeIfNecessary (pdef.ParameterType, out _);
				wrapperMethod.Parameters.Add (new ParameterDefinition (pdef.Name, pdef.Attributes, newType));

				inst = GetLoadArgInstruction (nparam++, pdef);
				if (firstTryInstruction == null) {
					firstTryInstruction = inst;
				}

				body.Instructions.Add (inst);

				if (!pdef.ParameterType.IsBlittable ()) {
					GenerateNonBlittableConversion (pdef.ParameterType, newType);
				}
			}

			inst = Instruction.Create (OpCodes.Call, callback);
			if (firstTryInstruction == null) {
				firstTryInstruction = inst;
			}
			body.Instructions.Add (inst);

			exceptionHandler.TryStart = firstTryInstruction;

			if (hasReturnValue && !returnTypeMapped) {
				body.Instructions.Add (Instruction.Create (OpCodes.Stloc, retval));
			}

			Instruction ret = Instruction.Create (OpCodes.Ret);
			Instruction? retValLoadInst = null;
			Instruction leaveTarget;

			if (hasReturnValue) {
				if (returnTypeMapped) {
					GenerateRetValCast (callback.ReturnType, retType);
					body.Instructions.Add (Instruction.Create (OpCodes.Stloc, retval));
				}

				retValLoadInst = Instruction.Create (OpCodes.Ldloc, retval);
				leaveTarget = retValLoadInst;
			} else {
				leaveTarget = ret;
			}

			body.Instructions.Add (Instruction.Create (OpCodes.Leave_S, leaveTarget));

			var exceptionVar = new VariableDefinition (imports.SystemException);
			body.Variables.Add (exceptionVar);

			var catchStartInst = Instruction.Create (OpCodes.Stloc, exceptionVar);
			exceptionHandler.HandlerStart = catchStartInst;

			// TryEnd must point to the next instruction after the try block
			exceptionHandler.TryEnd = catchStartInst;

			body.Instructions.Add (catchStartInst);
			body.Instructions.Add (Instruction.Create (OpCodes.Ldarg_0));
			body.Instructions.Add (Instruction.Create (OpCodes.Ldloc, exceptionVar));

			if (brokenExceptionTransitions) {
				body.Instructions.Add (Instruction.Create (OpCodes.Call, imports.MonoUnhandledExceptionMethod));
				body.Instructions.Add (Instruction.Create (OpCodes.Throw));
			} else {
				body.Instructions.Add (Instruction.Create (OpCodes.Call, imports.UnhandledExceptionMethod));

				if (hasReturnValue) {
					AddSetDefaultValueInstructions (body, retType, retval);
				}
			}

			body.Instructions.Add (Instruction.Create (OpCodes.Leave_S, leaveTarget));

			// HandlerEnd must point to the next instruction after the catch block
			if (hasReturnValue) {
				body.Instructions.Add (retValLoadInst);
				exceptionHandler.HandlerEnd = retValLoadInst;
			} else {
				exceptionHandler.HandlerEnd = ret;
			}
			body.Instructions.Add (ret);

			return wrapperMethod;

			void GenerateNonBlittableConversion (TypeReference sourceType, TypeReference targetType)
			{
				if (IsBooleanConversion (sourceType, targetType)) {
					// We output equivalent of the `param != 0` C# code
					body.Instructions.Add (Instruction.Create (OpCodes.Ldc_I4_0));
					body.Instructions.Add (Instruction.Create (OpCodes.Cgt_Un));
					return;
				}

				ThrowUnsupportedType (sourceType);
			}

			void GenerateRetValCast (TypeReference sourceType, TypeReference targetType)
			{
				if (IsBooleanConversion (sourceType, targetType)) {
					var insLoadOne = Instruction.Create (OpCodes.Ldc_I4_1);
					var insConvert = Instruction.Create (OpCodes.Conv_U1);

					body.Instructions.Add (Instruction.Create (OpCodes.Brtrue_S, insLoadOne));
					body.Instructions.Add (Instruction.Create (OpCodes.Ldc_I4_0));
					body.Instructions.Add (Instruction.Create (OpCodes.Br_S, insConvert));
					body.Instructions.Add (insLoadOne);
					body.Instructions.Add (insConvert);
					return;
				}

				ThrowUnsupportedType (sourceType);
			}

			bool IsBooleanConversion (TypeReference sourceType, TypeReference targetType)
			{
				if (String.Compare ("System.Boolean", sourceType.FullName, StringComparison.Ordinal) == 0) {
					if (String.Compare ("System.Byte", targetType.FullName, StringComparison.Ordinal) != 0) {
						throw new InvalidOperationException ($"Unexpected conversion from '{sourceType.FullName}' to '{targetType.FullName}'");
					}

					return true;
				}

				return false;
			}

			void ThrowUnsupportedType (TypeReference type)
			{
				throw new InvalidOperationException ($"Unsupported non-blittable type '{type.FullName}'");
			}
		}

		void AddSetDefaultValueInstructions (MethodBody body, TypeReference type, VariableDefinition retval)
		{
			bool supported = false;

			switch (type.FullName) {
				case "System.Boolean":
				case "System.Byte":
				case "System.Int16":
				case "System.Int32":
				case "System.SByte":
				case "System.UInt16":
				case "System.UInt32":
					supported = true;
					body.Instructions.Add (Instruction.Create (OpCodes.Ldc_I4_0));
					break;

				case "System.Int64":
				case "System.UInt64":
					supported = true;
					body.Instructions.Add (Instruction.Create (OpCodes.Ldc_I4_0));
					body.Instructions.Add (Instruction.Create (OpCodes.Conv_I8));
					break;

				case "System.IntPtr":
				case "System.UIntPtr":
					supported = true;
					body.Instructions.Add (Instruction.Create (OpCodes.Ldc_I4_0));
					body.Instructions.Add (Instruction.Create (OpCodes.Conv_I));
					break;

				case "System.Single":
					supported = true;
					body.Instructions.Add (Instruction.Create (OpCodes.Ldc_R4, 0.0F));
					break;

				case "System.Double":
					supported = true;
					body.Instructions.Add (Instruction.Create (OpCodes.Ldc_R8, 0.0));
					break;
			}

			if (supported) {
				body.Instructions.Add (Instruction.Create (OpCodes.Stloc, retval));
				return;
			}

			throw new InvalidOperationException ($"Unsupported type: '{type.FullName}'");
		}


		Instruction GetLoadArgInstruction (uint nparam, ParameterDefinition pdef)
		{
			OpCode ldargOp;
			bool paramRef = false;

			switch (nparam++) {
				case 0:
					ldargOp = OpCodes.Ldarg_0;
					break;

				case 1:
					ldargOp = OpCodes.Ldarg_1;
					break;

				case 2:
					ldargOp = OpCodes.Ldarg_2;
					break;

				case 3:
					ldargOp = OpCodes.Ldarg_3;
					break;

				default:
					ldargOp = OpCodes.Ldarg_S;
					paramRef = true;
					break;
			}

			if (!paramRef) {
				return Instruction.Create (ldargOp);
			}

			return Instruction.Create (ldargOp, pdef);
		}

		TypeReference MapToBlittableTypeIfNecessary (TypeReference type, out bool typeMapped)
		{
			if (type.IsBlittable () || String.Compare ("System.Void", type.FullName, StringComparison.Ordinal) == 0) {
				typeMapped = false;
				return type;
			}

			if (String.Compare ("System.Boolean", type.FullName, StringComparison.Ordinal) == 0) {
				// Maps to Java JNI's jboolean which is an unsigned 8-bit type
				typeMapped = true;
				return ReturnValid (typeof(byte));
			}

			throw new NotSupportedException ($"Cannot map unsupported blittable type '{type.FullName}'");

			TypeReference ReturnValid (Type typeToLookUp)
			{
				TypeReference? mappedType = type.Module.Assembly.MainModule.ImportReference (typeToLookUp);
				if (mappedType == null) {
					throw new InvalidOperationException ($"Unable to obtain reference to type '{typeToLookUp.FullName}'");
				}

				return mappedType;
			}
		}

		ICollection<string> GetAssemblyPaths (AssemblyDefinition asm)
		{
			if (!assemblyPaths.TryGetValue (asm.Name.Name, out HashSet<string> paths)) {
				throw new InvalidOperationException ($"Unable to determine file path for assembly '{asm.Name.Name}'");
			}

			return paths;
		}

		MethodDefinition GetUnmanagedCallersOnlyAttributeConstructor (DirectoryAssemblyResolver resolver)
		{
			AssemblyDefinition asm = resolver.Resolve ("System.Runtime.InteropServices");
			TypeDefinition unmanagedCallersOnlyAttribute = null;
			foreach (ModuleDefinition md in asm.Modules) {
				foreach (ExportedType et in md.ExportedTypes) {
					if (!et.IsForwarder) {
						continue;
					}

					if (String.Compare ("System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute", et.FullName, StringComparison.Ordinal) != 0) {
						continue;
					}

					unmanagedCallersOnlyAttribute = et.Resolve ();
					break;
				}
			}

			if (unmanagedCallersOnlyAttribute == null) {
				throw new InvalidOperationException ("Unable to find the System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute type");
			}

			foreach (MethodDefinition md in unmanagedCallersOnlyAttribute.Methods) {
				if (!md.IsConstructor) {
					continue;
				}

				return md;
			}

			throw new InvalidOperationException ("Unable to find the System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute type constructor");
		}

		CustomAttribute CreateImportedUnmanagedCallersOnlyAttribute (AssemblyDefinition targetAssembly, MethodDefinition unmanagedCallersOnlyAtributeCtor)
		{
			return new CustomAttribute (targetAssembly.MainModule.ImportReference (unmanagedCallersOnlyAtributeCtor));
		}

		MethodDefinition? FindMethod (TypeDefinition type, string methodName, bool required)
		{
			log.LogDebugMessage ($"Looking for method '{methodName}' in type {type}");
			foreach (MethodDefinition method in type.Methods) {
				log.LogDebugMessage ($"  method: {method.Name}");
				if (String.Compare (methodName, method.Name, StringComparison.Ordinal) == 0) {
					log.LogDebugMessage ("    match!");
					return method;
				}
			}

			if (required) {
				throw new InvalidOperationException ($"Internal error: required method '{methodName}' in type {type} not found");
			}

			return null;
		}

		TypeDefinition? FindType (AssemblyDefinition asm, string typeName, bool required)
		{
			log.LogDebugMessage ($"Looking for type '{typeName}' in assembly '{asm}'");
			foreach (TypeDefinition t in asm.MainModule.Types) {
				log.LogDebugMessage ($"   checking {t.FullName}");
				if (String.Compare (typeName, t.FullName, StringComparison.Ordinal) == 0) {
					log.LogDebugMessage ($"    match!");
					return t;
				}
			}

			if (required) {
				throw new InvalidOperationException ($"Internal error: required type '{typeName}' in assembly {asm} not found");
			}

			return null;
		}
	}
}
