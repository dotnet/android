using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

using Java.Interop.Tools.Cecil;

using Mono.Linker;
using Mono.Linker.Steps;
using Mono.Tuner;

namespace MonoDroid.Tuner
{
	class MonoDroidMarkStep : MarkStep
	{
		const string ICustomMarshalerName = "System.Runtime.InteropServices.ICustomMarshaler";
		HashSet<TypeDefinition> marshalTypes = new HashSet<TypeDefinition> ();

		public override void Process (LinkContext context)
		{
			marshalTypes.Clear ();
			base.Process (context);

			if (PreserveJniMarshalMethods () && UpdateMarshalTypes ())
				base.Process (context);
		}

		bool UpdateMarshalTypes ()
		{
			MethodDefinition registerMethod;
			HashSet<string> markedMethods = new HashSet<string> ();
			var updated = false;

			foreach (var type in marshalTypes) {
				registerMethod = null;
				markedMethods.Clear ();

				foreach (var method in type.Methods) {
					if (method.Name == "__RegisterNativeMembers") {
						registerMethod = method;

						continue;
					}

					if (_context.Annotations.IsMarked (method))
						markedMethods.Add (method.Name);
				}

				if (registerMethod == null || markedMethods.Count <= 0)
					continue;

				updated |= UpdateMarshalRegisterMethod (registerMethod, markedMethods);
			}

			return updated;
		}

		static bool IsLdcI4 (Instruction instruction, out int intValue)
		{
			intValue = 0;

			if (instruction.OpCode == OpCodes.Ldc_I4_0)
				intValue = 0;
			else if (instruction.OpCode == OpCodes.Ldc_I4_1)
				intValue = 1;
			else if (instruction.OpCode == OpCodes.Ldc_I4_2)
				intValue = 2;
			else if (instruction.OpCode == OpCodes.Ldc_I4_3)
				intValue = 3;
			else if (instruction.OpCode == OpCodes.Ldc_I4_4)
				intValue = 4;
			else if (instruction.OpCode == OpCodes.Ldc_I4_5)
				intValue = 5;
			else if (instruction.OpCode == OpCodes.Ldc_I4_6)
				intValue = 6;
			else if (instruction.OpCode == OpCodes.Ldc_I4_7)
				intValue = 7;
			else if (instruction.OpCode == OpCodes.Ldc_I4_8)
				intValue = 8;
			else if (instruction.OpCode == OpCodes.Ldc_I4)
				intValue = (int) instruction.Operand;
			else if (instruction.OpCode == OpCodes.Ldc_I4_S)
				intValue = (sbyte) instruction.Operand;
			else
				return false;

			return true;
		}

		static Instruction CreateLoadArraySizeOrOffsetInstruction (int intValue)
		{
			if (intValue < 0)
				throw new ArgumentException ($"{nameof (intValue)} cannot be negative");

			if (intValue < 9) {
				switch (intValue) {
				case 0:
					return Instruction.Create (OpCodes.Ldc_I4_0);
				case 1:
					return Instruction.Create (OpCodes.Ldc_I4_1);
				case 2:
					return Instruction.Create (OpCodes.Ldc_I4_2);
				case 3:
					return Instruction.Create (OpCodes.Ldc_I4_3);
				case 4:
					return Instruction.Create (OpCodes.Ldc_I4_4);
				case 5:
					return Instruction.Create (OpCodes.Ldc_I4_5);
				case 6:
					return Instruction.Create (OpCodes.Ldc_I4_6);
				case 7:
					return Instruction.Create (OpCodes.Ldc_I4_7);
				case 8:
					return Instruction.Create (OpCodes.Ldc_I4_8);
				}
			}

			if (intValue < 128)
				return Instruction.Create (OpCodes.Ldc_I4_S, (sbyte)intValue);

			return Instruction.Create (OpCodes.Ldc_I4, intValue);
		}

		bool UpdateMarshalRegisterMethod (MethodDefinition method, HashSet<string> markedMethods)
		{
			var instructions = method.Body.Instructions;
			var arraySizeUpdated = false;
			var idx = 0;
			var arrayOffset = 0;

			while (idx < instructions.Count) {
				if (!arraySizeUpdated && idx + 1 < instructions.Count) {
					int length;
					if (IsLdcI4 (instructions [idx++], out length) && instructions [idx].OpCode == OpCodes.Newarr) {
						instructions [idx - 1] = CreateLoadArraySizeOrOffsetInstruction (markedMethods.Count);
						idx++;
						arraySizeUpdated = true;
						continue;
					}
				} else if (idx + 9 < instructions.Count) {
					var chunkStart = idx;
					if (instructions [idx++].OpCode != OpCodes.Dup)
						continue;

					int offset;
					var offsetIdx = idx;
					if (!IsLdcI4 (instructions [idx++], out offset))
						continue;

					if (instructions [idx++].OpCode != OpCodes.Ldstr)
						continue;

					if (instructions [idx++].OpCode != OpCodes.Ldstr)
						continue;

					if (instructions [idx++].OpCode != OpCodes.Ldnull)
						continue;

					if (instructions [idx++].OpCode != OpCodes.Ldftn)
						continue;

					if (!(instructions [idx - 1].Operand is MethodReference mr))
						continue;

					if (instructions [idx++].OpCode != OpCodes.Newobj)
						continue;

					if (instructions [idx++].OpCode != OpCodes.Newobj)
						continue;

					var chunkEnd = idx;
					if (instructions [idx++].OpCode != OpCodes.Stelem_Any)
						continue;

					if (markedMethods.Contains (mr.Name)) {
						instructions [offsetIdx] = CreateLoadArraySizeOrOffsetInstruction (arrayOffset++);
						continue;
					}

					for (int i = 0; i <= chunkEnd - chunkStart; i++)
						instructions.RemoveAt (chunkStart);

					idx = chunkStart;
				} else
					break;
			}

			if (!arraySizeUpdated || arrayOffset != markedMethods.Count) {
				_context.LogMessage ($"Unable to update {method} size updated {arraySizeUpdated} counts {arrayOffset} {markedMethods.Count}");
				return false;
			}

			MarkMethod (method);

			return true;
		}

		bool PreserveJniMarshalMethods ()
		{
			if (_context is AndroidLinkContext ac)
				return ac.PreserveJniMarshalMethods;

			return false;
		}

		// If this is one of our infrastructure methods that has [Register], like:
		// [Register ("hasWindowFocus", "()Z", "GetHasWindowFocusHandler")],
		// we need to preserve the "GetHasWindowFocusHandler" method as well.
		protected override void DoAdditionalMethodProcessing (MethodDefinition method)
		{
			string member, nativeMethod, signature;

			if (!method.TryGetRegisterMember (out member, out nativeMethod, out signature))
				return;

			MethodDefinition marshalMethod;
			if (PreserveJniMarshalMethods () && method.TryGetMarshalMethod (nativeMethod, signature, out marshalMethod)) {
				MarkMethod (marshalMethod);
				marshalTypes.Add (marshalMethod.DeclaringType);
			}

			PreserveRegisteredMethod (method.DeclaringType, member);
		}

		protected override void DoAdditionalTypeProcessing (TypeDefinition type)
		{
			// If we are preserving a Mono.Android interface,
			// preserve all members on the interface.
			if (!type.IsInterface)
				return;

			// Mono.Android interfaces will always inherit IJavaObject
			if (!type.ImplementsIJavaObject ())
				return;

			foreach (MethodReference method in type.Methods)
				MarkMethod (method);
		}
		
		private void PreserveRegisteredMethod (TypeDefinition type, string member)
		{
			var type_ptr = type;
			var pos = member.IndexOf (':');

			if (pos > 0) {
				var type_name = member.Substring (pos + 1);
				member = member.Substring (0, pos);
				type_ptr = type_ptr.Module.Types.FirstOrDefault (t => t.FullName == type_name);
			}

			if (type_ptr == null)
				return;

			while (MarkNamedMethod (type, member) == 0 && type.BaseType != null)
				type = type.BaseType.Resolve ();
		}

		protected override TypeDefinition MarkType (TypeReference reference)
		{
			TypeDefinition type = base.MarkType (reference);
			if (type == null)
				return null;

			switch (type.Module.Assembly.Name.Name) {
			case "mscorlib":
				ProcessCorlib (type);
				break;
			case "System.Core":
				ProcessSystemCore (type);
				break;
			case "System.Data":
				ProcessSystemData (type);
				break;
			case "System":
				ProcessSystem (type);
				break;
			}

			if (type.HasMethods && type.HasInterfaces && type.Implements (ICustomMarshalerName)) {
				foreach (MethodDefinition method in type.Methods) {
					if (method.Name == "GetInstance" && method.IsStatic && method.HasParameters && method.Parameters.Count == 1 && method.ReturnType.FullName == ICustomMarshalerName && method.Parameters.First ().ParameterType.FullName == "System.String") {
						MarkMethod (method);
						break;
					}
				}
			}

			return type;
		}

		bool DebugBuild {
			get { return _context.LinkSymbols; }
		}

		void ProcessCorlib (TypeDefinition type)
		{
			switch (type.Namespace) {
			case "System.Runtime.CompilerServices":
				switch (type.Name) {
				case "AsyncTaskMethodBuilder":
					if (DebugBuild) {
						MarkNamedMethod (type, "SetNotificationForWaitCompletion");
						MarkNamedMethod (type, "get_ObjectIdForDebugger");
					}
					break;
				case "AsyncTaskMethodBuilder`1":
					if (DebugBuild) {
						MarkNamedMethod (type, "SetNotificationForWaitCompletion");
						MarkNamedMethod (type, "get_ObjectIdForDebugger");
					}
					break;
				}
				break;
			case "System.Threading.Tasks":
				switch (type.Name) {
				case "Task":
					if (DebugBuild)
						MarkNamedMethod (type, "NotifyDebuggerOfWaitCompletion");
					break;
				}
				break;
			}
		}

		void ProcessSystemCore (TypeDefinition type)
		{
			switch (type.Namespace) {
			case "System.Linq.Expressions":
				switch (type.Name) {
				case "LambdaExpression":
					var expr_t = type.Module.GetType ("System.Linq.Expressions.Expression`1");
					if (expr_t != null)
						MarkNamedMethod (expr_t, "Create");
					break;
				}
				break;
			case "System.Linq.Expressions.Compiler":
				switch (type.Name) {
				case "LambdaCompiler":
					MarkNamedMethod (type.Module.GetType ("System.Runtime.CompilerServices.RuntimeOps"), "Quote");
					break;
				}
				break;
			}
		}

		protected AssemblyDefinition GetAssembly (string assemblyName)
		{
			AssemblyDefinition ad;
			_context.TryGetLinkedAssembly (assemblyName, out ad);
			return ad;
		}

		protected TypeDefinition GetType (string assemblyName, string typeName)
		{
			AssemblyDefinition ad = GetAssembly (assemblyName);
			return ad == null ? null : GetType (ad, typeName);
		}

		protected TypeDefinition GetType (AssemblyDefinition assembly, string typeName)
		{
			return assembly.MainModule.GetType (typeName);
		}

		void ProcessSystemData (TypeDefinition type)
		{
			switch (type.Namespace) {
			case "System.Data.SqlTypes":
				switch (type.Name) {
				case "SqlXml":
					// TODO: Needed only if CreateSqlReaderDelegate is used
					TypeDefinition xml_reader = GetType ("System.Xml", "System.Xml.XmlReader");
					MarkNamedMethod (xml_reader, "CreateSqlReader");
					break;
				}
				break;
			}
		}

		void ProcessSystem (TypeDefinition type)
		{
			switch (type.Namespace) {
			case "System.Diagnostics":
				switch (type.Name) {
				// see mono/metadata/process.c
				case "FileVersionInfo":
				case "ProcessModule":
					// fields are initialized by the runtime, if the type is here then all (instance) fields must be present
					MarkFields (type, false);
					break;
				}
				break;
			case "System.Net.Sockets":
				switch (type.Name) {
				case "IPAddress":
					// mono/metadata/socket-io.c directly access 'm_Address' and 'm_Numbers'
					MarkFields (type, false);
					break;
				case "IPv6MulticastOption":
					// mono/metadata/socket-io.c directly access 'group' and 'ifIndex' private instance fields
					MarkFields (type, false);
					break;
				case "LingerOption":
					// mono/metadata/socket-io.c directly access 'enabled' and 'seconds' private instance fields
					MarkFields (type, false);
					break;
				case "MulticastOption":
					// mono/metadata/socket-io.c directly access 'group' and 'local' private instance fields
					MarkFields (type, false);
					break;
				case "Socket":
					// mono/metadata/socket-io.c directly access 'ipv4Supported', 'ipv6Supported' (static) and 'socket' (instance)
					MarkFields (type, true);
					break;
				case "SocketAddress":
					// mono/metadata/socket-io.c directly access 'data'
					MarkFields (type, false);
					break;
				}
				break;
			case "":
				if (!type.IsNested)
					break;

				switch (type.Name) {
				case "SocketAsyncResult":
					// mono/metadata/socket-io.h defines this structure (MonoSocketAsyncResult) for the runtime usage
					MarkFields (type, false);
					break;
				case "ProcessAsyncReader":
					// mono/metadata/socket-io.h defines this structure (MonoSocketAsyncResult) for the runtime usage
					MarkFields (type, false);
					break;
				}
				break;
			}
		}
	}
}
