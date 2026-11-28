using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

enum CallbackBoundaryKind
{
	Unavailable,
	Unrecognized,
	Raw,
	ProtectedHelper,
	ProtectedInline,
	UnmanagedCallersOnly,
}

readonly record struct CallbackBoundary (CallbackBoundaryKind Kind, string Reason)
{
	public bool IsProtected => Kind is CallbackBoundaryKind.ProtectedHelper or CallbackBoundaryKind.ProtectedInline;
	public static CallbackBoundary Unavailable => new (CallbackBoundaryKind.Unavailable, "No implementation callback body is available.");
}

// Recognize supported generator shapes, not arbitrary IL that might implement an exception boundary.
sealed class CallbackBoundaryClassifier
{
	sealed record Api (string Assembly, string Type, string Name, MethodSignature<string> Signature,
		MethodSignature<string> DefinitionSignature, bool Trusted);
	sealed record Instruction (int Offset, int End, string Op, object? Operand, Api? Api, FlowControl FlowControl);

	static readonly Dictionary<ushort, OpCode> Opcodes = typeof (OpCodes).GetFields (BindingFlags.Public | BindingFlags.Static)
		.Where (f => f.FieldType == typeof (OpCode))
		.Select (f => f.GetValue (null) is OpCode op ? op : throw new InvalidOperationException ("Invalid IL opcode."))
		.ToDictionary (o => unchecked ((ushort) o.Value));

	readonly MetadataReader reader;
	readonly PEReader pe;
	readonly bool isReferenceAssembly;
	readonly BoundarySignatureProvider signatures;
	readonly Dictionary<MethodDefinitionHandle, CallbackBoundary> cache = new ();

	public CallbackBoundaryClassifier (PEReader pe, bool isReferenceAssembly)
	{
		this.pe = pe;
		reader = pe.GetMetadataReader ();
		this.isReferenceAssembly = isReferenceAssembly;
		signatures = new BoundarySignatureProvider (reader);
	}

	public CallbackBoundary Classify (MethodDefinitionHandle handle)
	{
		if (cache.TryGetValue (handle, out var result))
			return result;
		result = ClassifyCore (handle);
		cache.Add (handle, result);
		return result;
	}

	public bool HasFrameworkAttribute (MethodDefinition method, string type)
	{
		return method.GetCustomAttributes ().Any (h => {
			var api = ReadApi (reader.GetCustomAttribute (h).Constructor);
			return api.Type == type && api.Trusted &&
				api.Assembly is "System.Runtime" or "System.Private.CoreLib" or "System.Diagnostics.Debug" or "System.Runtime.InteropServices" or "mscorlib";
		});
	}

	CallbackBoundary ClassifyCore (MethodDefinitionHandle handle)
	{
		var method = reader.GetMethodDefinition (handle);
		if (HasFrameworkAttribute (method, "System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute"))
			return new (CallbackBoundaryKind.UnmanagedCallersOnly, "Native-only callback; register its address directly.");
		if (isReferenceAssembly || method.RelativeVirtualAddress == 0)
			return CallbackBoundary.Unavailable;
		var body = pe.GetMethodBody (method.RelativeVirtualAddress);
		var instructions = Decode (body.GetILBytes () ?? throw new BadImageFormatException ("Missing callback IL."));
		var code = instructions.Where (i => i.Op != "nop").ToArray ();
		var signature = method.DecodeSignature (signatures, []);
		if (IsHelperThunk (code, body, signature))
			return new (CallbackBoundaryKind.ProtectedHelper, "Exact trusted marshaling-helper thunk.");
		if (IsInlineBoundary (code, instructions, body, signature.ReturnType) &&
		    HasFrameworkAttribute (method, "System.Diagnostics.DebuggerDisableUserUnhandledExceptionsAttribute"))
			return new (CallbackBoundaryKind.ProtectedInline, "Complete guarded inline JNI transition with debugger-aware exception policy.");
		if (body.ExceptionRegions.Length == 0 && !code.Any (i => IsHelper (i.Api) || IsBegin (i.Api) || IsEnd (i.Api) || IsNotify (i.Api)))
			return new (CallbackBoundaryKind.Raw, "No recognized JNI exception boundary.");
		return new (CallbackBoundaryKind.Unrecognized, "Callback does not match a complete supported debugger-aware boundary.");
	}

	static bool IsHelperThunk (Instruction [] code, MethodBodyBlock body, MethodSignature<string> signature)
	{
		int count = signature.ParameterTypes.Length;
		if (body.ExceptionRegions.Length != 0 || code.Length != count + 3 ||
		    !code.Take (count).Select (ArgumentIndex).SequenceEqual (Enumerable.Range (0, count)) ||
		    code [count].Op != "ldftn" || code [count].Api is not { Signature.Header.IsInstance: false } target ||
		    code [count + 1].Op != "call" || !IsHelper (code [count + 1].Api) ||
		    code [count + 1].Api is not Api helper || code [^1].Op != "ret")
			return false;
		var helperSignature = helper.Signature;
		return helperSignature.ParameterTypes.Length == count + 1 &&
			helperSignature.ParameterTypes.Take (count).SequenceEqual (signature.ParameterTypes) &&
			helperSignature.ReturnType == signature.ReturnType &&
			helperSignature.ParameterTypes [^1] == BoundarySignatureProvider.FunctionPointer (target.Signature);
	}

	static bool IsHelper (Api? api)
	{
		if (api is not { Trusted: true, Signature.Header.IsInstance: false } ||
		    api.Signature.Header.CallingConvention != SignatureCallingConvention.Default ||
		    api.Signature.ParameterTypes.Length < 3 ||
		    api.Signature.ParameterTypes [0] != "System.IntPtr" || api.Signature.ParameterTypes [1] != "System.IntPtr")
			return false;
		var definition = api.DefinitionSignature;
		int count = definition.ParameterTypes.Length - 3;
		var pointerParameters = new List<string> ();
		string pointerReturn;
		if (api.Assembly == "Java.Interop" && api.Type == "Java.Interop.JniMarshal") {
			bool action = api.Name == "SafeInvokeAction";
			if ((!action && api.Name != "SafeInvokeFunc") || count > 16 ||
			    definition.GenericParameterCount != count + (action ? 0 : 1))
				return false;
			pointerParameters.AddRange (["System.IntPtr", "System.IntPtr"]);
			for (int i = 0; i < count; i++) {
				string parameter = $"!!{i}";
				if (definition.ParameterTypes [i + 2] != parameter)
					return false;
				pointerParameters.Add (parameter);
			}
			pointerReturn = action ? "System.Void" : $"!!{count}";
			return definition.ReturnType == pointerReturn &&
				definition.ParameterTypes [^1] == $"fnptr:0:0:({string.Join (",", pointerParameters)})->{pointerReturn}";
		}
		if (api.Assembly != "Mono.Android" || api.Type != "Java.Interop.JniMarshalTyped" || !api.Name.StartsWith ("Invoke_", StringComparison.Ordinal))
			return false;
		var shape = api.Name.AsSpan (7);
		if (!(shape.Length == api.Signature.ParameterTypes.Length - 2 &&
			shape.Length <= 9 && shape [^1] is 'X' or 'S' or 'O' && shape [..^1].IndexOfAnyExcept ('O', 'S') < 0 &&
			(shape.Length <= 4 || shape [..^1].IndexOf ('O') < 0)) ||
		    definition.GenericParameterCount != count + 1 + (shape [^1] == 'S' ? 1 : 0))
			return false;
		pointerParameters.Add ("!!0");
		for (int i = 0; i < count; i++) {
			string parameter = $"!!{i + 1}";
			if (definition.ParameterTypes [i + 2] != (shape [i] == 'O' ? "System.IntPtr" : parameter))
				return false;
			pointerParameters.Add (parameter);
		}
		pointerReturn = shape [^1] switch {
			'X' => "System.Void", 'S' => $"!!{count + 1}", _ => "Android.Runtime.IJavaObject",
		};
		return definition.ReturnType == (shape [^1] == 'O' ? "System.IntPtr" : pointerReturn) &&
			definition.ParameterTypes [^1] == $"fnptr:0:0:({string.Join (",", pointerParameters)})->{pointerReturn}";
	}

	static bool IsBegin (Api? api) => Matches (api, "Java.Interop.JniEnvironment", "BeginMarshalMethod", false, "System.Boolean",
		["System.IntPtr", "Java.Interop.JniTransition&", "Java.Interop.JniRuntime&"]);
	static bool IsEnd (Api? api) => Matches (api, "Java.Interop.JniEnvironment", "EndMarshalMethod", false, "System.Void", ["Java.Interop.JniTransition&"]);
	static bool IsNotify (Api? api) => Matches (api, "Java.Interop.JniRuntime", "OnUserUnhandledException", true, "System.Void",
		["Java.Interop.JniTransition&", "System.Exception"]);
	static bool Matches (Api? api, string type, string name, bool instance, string returnType, string [] parameters)
		=> api is { Trusted: true, Assembly: "Java.Interop" } && api.Type == type && api.Name == name &&
			api.Signature.GenericParameterCount == 0 && api.Signature.Header.CallingConvention == SignatureCallingConvention.Default &&
			api.Signature.Header.IsInstance == instance && api.Signature.ReturnType == returnType &&
			api.Signature.ParameterTypes.SequenceEqual (parameters);

	bool IsInlineBoundary (Instruction [] code, Instruction [] instructions, MethodBodyBlock body, string returnType)
	{
		var begin = code.Where (i => i.Op == "call" && IsBegin (i.Api)).ToArray ();
		var notify = code.Where (i => i.Op == "callvirt" && IsNotify (i.Api)).ToArray ();
		var end = code.Where (i => i.Op == "call" && IsEnd (i.Api)).ToArray ();
		if (begin.Length != 1 || notify.Length != 1 || end.Length != 1 || code.Length < 6)
			return false;
		var catches = body.ExceptionRegions.Where (r => r.Kind == ExceptionRegionKind.Catch &&
			signatures.TypeName (r.CatchType) == "System.Exception" && signatures.IsTrustedType (r.CatchType) && InHandler (notify [0], r)).ToArray ();
		var finalizers = body.ExceptionRegions.Where (r => r.Kind == ExceptionRegionKind.Finally && InHandler (end [0], r)).ToArray ();
		if (catches.Length != 1 || finalizers.Length != 1)
			return false;
		var catcher = catches [0];
		var finalizer = finalizers [0];
		int transition = LocalIndex (code [1], "ldloca");
		int runtime = LocalIndex (code [2], "ldloca");
		var locals = body.LocalSignature.IsNil ? [] : reader.GetStandaloneSignature (body.LocalSignature).DecodeLocalSignature (signatures, []);
		if (code [3] != begin [0] || ArgumentIndex (code [0]) != 0 || transition < 0 || runtime < 0 || transition == runtime ||
		    transition >= locals.Length || runtime >= locals.Length ||
		    locals [transition] != "Java.Interop.JniTransition" || locals [runtime] != "Java.Interop.JniRuntime" ||
		    code [4].Op is not ("brtrue" or "brtrue.s") || code [4].Operand is not int target ||
		    target > catcher.TryOffset || !instructions.Any (i => i.Offset == target) ||
		    instructions.Any (i => i.Offset >= target && i.Offset < catcher.TryOffset && i.Op != "nop") ||
		    finalizer.TryOffset != catcher.TryOffset ||
		    catcher.TryOffset + catcher.TryLength != catcher.HandlerOffset ||
		    catcher.HandlerOffset + catcher.HandlerLength != finalizer.TryOffset + finalizer.TryLength ||
		    finalizer.TryOffset + finalizer.TryLength != finalizer.HandlerOffset)
			return false;
		if (!DefaultValue (code.Skip (5).TakeWhile (i => i.Offset < catcher.TryOffset).ToArray (), returnType, locals, "ret"))
			return false;
		if (body.ExceptionRegions.Any (r => !r.Equals (catcher) && !r.Equals (finalizer) &&
		    !(r.TryOffset >= catcher.TryOffset && r.HandlerOffset + r.HandlerLength <= catcher.TryOffset + catcher.TryLength)))
			return false;
		var handler = code.Where (i => InHandler (i, catcher)).ToArray ();
		var cleanup = code.Where (i => InHandler (i, finalizer)).ToArray ();
		var epilogue = code.Where (i => i.Offset >= finalizer.HandlerOffset + finalizer.HandlerLength).ToArray ();
		int exception = handler.Length == 0 ? -1 : LocalIndex (handler [0], "stloc");
		if (handler.Length < 6 || exception < 0 || exception >= locals.Length || locals [exception] != "System.Exception" ||
		    LocalIndex (handler [1], "ldloc") != runtime || LocalIndex (handler [2], "ldloca") != transition ||
		    LocalIndex (handler [3], "ldloc") != exception || handler [4] != notify [0] ||
		    handler [^1].Op is not ("leave" or "leave.s") || handler [^1].Operand is not int leaveTarget ||
		    leaveTarget != finalizer.HandlerOffset + finalizer.HandlerLength ||
		    cleanup.Length != 3 || LocalIndex (cleanup [0], "ldloca") != transition || cleanup [1] != end [0] || cleanup [2].Op != "endfinally")
			return false;
		if (returnType == "System.Void") {
			if (handler.Length != 6 || epilogue.Length != 1 || epilogue [0].Op != "ret")
				return false;
		} else {
			int resultLocal = LocalIndex (handler [^2], "stloc");
			if (resultLocal < 0 || resultLocal == transition || resultLocal == runtime || resultLocal == exception ||
			    resultLocal >= locals.Length || locals [resultLocal] != returnType ||
			    epilogue.Length != 2 || LocalIndex (epilogue [0], "ldloc") != resultLocal || epilogue [1].Op != "ret" ||
			    !DefaultValue (handler [5..^2], returnType, locals, null))
				return false;
		}
		// A callback must not mutate, publish, or expose its boundary-owned locals in the user body.
		return code.Where (i => InTry (i, catcher)).All (i =>
			LocalIndex (i, "ldloc") != transition && LocalIndex (i, "stloc") != transition && LocalIndex (i, "ldloca") != transition &&
			LocalIndex (i, "ldloc") != runtime && LocalIndex (i, "stloc") != runtime && LocalIndex (i, "ldloca") != runtime &&
			i.Op is not ("ret" or "jmp" or "tail.") &&
			(i.FlowControl is not (FlowControl.Branch or FlowControl.Cond_Branch) ||
			 (i.Operand is int branch && SafeTarget (i, branch)) ||
			 (i.Operand is int [] targets && targets.All (destination => SafeTarget (i, destination)))));

		bool SafeTarget (Instruction instruction, int branch)
			=> instructions.Any (i => i.Offset == branch) &&
				(branch >= catcher.TryOffset && branch < catcher.TryOffset + catcher.TryLength ||
				 instruction.Op is "leave" or "leave.s" && branch == finalizer.HandlerOffset + finalizer.HandlerLength);
	}

	static bool InTry (Instruction i, ExceptionRegion r) => i.Offset >= r.TryOffset && i.End <= r.TryOffset + r.TryLength;
	static bool InHandler (Instruction i, ExceptionRegion r) => i.Offset >= r.HandlerOffset && i.End <= r.HandlerOffset + r.HandlerLength;

	static bool DefaultValue (Instruction [] code, string type, ImmutableArray<string> locals, string? terminal)
	{
		if (terminal is not null) {
			if (code.Length == 0 || code [^1].Op != terminal)
				return false;
			code = code [..^1];
		}
		if (type == "System.Void")
			return code.Length == 0;
		if (code.Length == 1) {
			if (type is "System.Single" or "System.Double")
				return code [0].Op is "ldc.r4" or "ldc.r8" && Convert.ToDouble (code [0].Operand, CultureInfo.InvariantCulture) == 0;
			return code [0].Op == "ldc.i4.0" && type is "System.Boolean" or "System.SByte" or "System.Byte" or "System.Char" or
				"System.Int16" or "System.UInt16" or "System.Int32" or "System.UInt32";
		}
		if (code.Length == 2 && code [0].Op == "ldc.i4.0")
			return (type is "System.IntPtr" or "System.UIntPtr" && code [1].Op is "conv.i" or "conv.u") ||
				(type is "System.Int64" or "System.UInt64" && code [1].Op is "conv.i8" or "conv.u8");
		if (code.Length == 3 && code [1].Op == "initobj" && code [1].Operand is string initialized && initialized == type) {
			int local = LocalIndex (code [0], "ldloca");
			return local >= 0 && local < locals.Length && locals [local] == type && LocalIndex (code [2], "ldloc") == local;
		}
		return false;
	}

	static int ArgumentIndex (Instruction i) => i.Op switch {
		"ldarg.0" => 0, "ldarg.1" => 1, "ldarg.2" => 2, "ldarg.3" => 3,
		"ldarg" or "ldarg.s" => i.Operand is int n ? n : -1, _ => -1,
	};
	static int LocalIndex (Instruction i, string prefix)
	{
		if (i.Op == prefix || i.Op == prefix + ".s")
			return i.Operand is int n ? n : -1;
		return i.Op.StartsWith (prefix + ".", StringComparison.Ordinal) && int.TryParse (i.Op.AsSpan (prefix.Length + 1), out int index) ? index : -1;
	}

	Instruction [] Decode (byte [] bytes)
	{
		var result = new List<Instruction> ();
		int position = 0;
		int Int32 () { int value = BitConverter.ToInt32 (bytes, position); position += 4; return value; }
		while (position < bytes.Length) {
			int start = position;
			ushort key = bytes [position++];
			if (key == 0xfe)
				key = (ushort) (0xfe00 | bytes [position++]);
			if (!Opcodes.TryGetValue (key, out var opcode))
				throw new BadImageFormatException ($"Unknown callback IL opcode {key:x4}.");
			object? operand = null;
			Api? api = null;
			switch (opcode.OperandType) {
			case OperandType.InlineNone: break;
			case OperandType.ShortInlineVar: operand = (int) bytes [position++]; break;
			case OperandType.InlineVar: operand = (int) BitConverter.ToUInt16 (bytes, position); position += 2; break;
			case OperandType.ShortInlineI: operand = (int) (sbyte) bytes [position++]; break;
			case OperandType.InlineI: operand = Int32 (); break;
			case OperandType.InlineI8: operand = BitConverter.ToInt64 (bytes, position); position += 8; break;
			case OperandType.ShortInlineR: operand = BitConverter.ToSingle (bytes, position); position += 4; break;
			case OperandType.InlineR: operand = BitConverter.ToDouble (bytes, position); position += 8; break;
			case OperandType.ShortInlineBrTarget:
				int delta = (sbyte) bytes [position++];
				operand = position + delta;
				break;
			case OperandType.InlineBrTarget:
				int offset = Int32 ();
				operand = position + offset;
				break;
			case OperandType.InlineSwitch:
				int count = Int32 ();
				int switchEnd = checked (position + count * 4);
				var targets = new int [count];
				for (int j = 0; j < count; j++)
					targets [j] = switchEnd + Int32 ();
				operand = targets;
				break;
			case OperandType.InlineMethod: api = ReadApi (MetadataTokens.EntityHandle (Int32 ())); break;
			case OperandType.InlineType: operand = signatures.TypeName (MetadataTokens.EntityHandle (Int32 ())); break;
			default: operand = Int32 (); break;
			}
			result.Add (new Instruction (start, position, opcode.Name ?? throw new BadImageFormatException ("Unnamed IL opcode."), operand, api, opcode.FlowControl));
		}
		return result.ToArray ();
	}

	Api ReadApi (EntityHandle handle, ImmutableArray<string> arguments = default)
	{
		if (handle.Kind == HandleKind.MethodSpecification) {
			var spec = reader.GetMethodSpecification ((MethodSpecificationHandle) handle);
			return ReadApi (spec.Method, spec.DecodeSignature (signatures, []));
		}
		EntityHandle owner;
		string name;
		MethodSignature<string> signature;
		MethodSignature<string> definitionSignature;
		if (handle.Kind == HandleKind.MethodDefinition) {
			var method = reader.GetMethodDefinition ((MethodDefinitionHandle) handle);
			owner = method.GetDeclaringType ();
			name = reader.GetString (method.Name);
			signature = method.DecodeSignature (signatures, arguments);
			definitionSignature = method.DecodeSignature (signatures, []);
		} else if (handle.Kind == HandleKind.MemberReference) {
			var member = reader.GetMemberReference ((MemberReferenceHandle) handle);
			owner = member.Parent;
			name = reader.GetString (member.Name);
			signature = member.DecodeMethodSignature (signatures, arguments);
			definitionSignature = member.DecodeMethodSignature (signatures, []);
		} else {
			throw new BadImageFormatException ($"Unexpected callback method handle {handle.Kind}.");
		}
		return new Api (signatures.TypeAssembly (owner), signatures.TypeName (owner), name, signature, definitionSignature, signatures.IsTrustedType (owner));
	}

	sealed class BoundarySignatureProvider (MetadataReader reader) : ISignatureTypeProvider<string, ImmutableArray<string>>
	{
		public string TypeName (EntityHandle handle)
		{
			string name = RawTypeName (handle);
			bool boundaryType = name is "Java.Interop.JniTransition" or "Java.Interop.JniRuntime";
			bool javaObject = name == "Android.Runtime.IJavaObject";
			if ((boundaryType || javaObject || name == "System.Exception") &&
			    (!IsTrustedType (handle) || boundaryType && TypeAssembly (handle) != "Java.Interop" ||
			     javaObject && TypeAssembly (handle) != "Mono.Android" ||
			     name == "System.Exception" && TypeAssembly (handle) is not ("System.Runtime" or "System.Private.CoreLib" or "mscorlib")))
				return $"[{TypeAssembly (handle)}:untrusted]{name}";
			return name;
		}
		string RawTypeName (EntityHandle handle) => handle.Kind switch {
			HandleKind.TypeDefinition => MetadataTypeNameResolver.GetTypeFromDefinition (reader, (TypeDefinitionHandle) handle, 0),
			HandleKind.TypeReference => MetadataTypeNameResolver.GetTypeFromReference (reader, (TypeReferenceHandle) handle, 0),
			HandleKind.TypeSpecification => reader.GetTypeSpecification ((TypeSpecificationHandle) handle).DecodeSignature (this, []),
			_ => throw new BadImageFormatException ($"Unexpected type handle {handle.Kind}."),
		};
		EntityHandle AssemblyScope (EntityHandle handle)
		{
			while (handle.Kind == HandleKind.TypeReference)
				handle = reader.GetTypeReference ((TypeReferenceHandle) handle).ResolutionScope;
			return handle;
		}
		public string TypeAssembly (EntityHandle handle)
		{
			var scope = AssemblyScope (handle);
			return scope.Kind == HandleKind.AssemblyReference
				? reader.GetString (reader.GetAssemblyReference ((AssemblyReferenceHandle) scope).Name)
				: reader.GetString (reader.GetAssemblyDefinition ().Name);
		}
		public bool IsTrustedType (EntityHandle handle)
		{
			var scope = AssemblyScope (handle);
			var name = TypeAssembly (handle);
			string token;
			if (scope.Kind == HandleKind.AssemblyReference) {
				var assembly = reader.GetAssemblyReference ((AssemblyReferenceHandle) scope);
				if (!assembly.Culture.IsNil && reader.GetString (assembly.Culture).Length != 0)
					return false;
				var key = reader.GetBlobBytes (assembly.PublicKeyOrToken);
				token = (assembly.Flags & AssemblyFlags.PublicKey) == 0 ? Convert.ToHexString (key) : KeyToken (key);
			} else if (scope.Kind == HandleKind.TypeDefinition) {
				token = KeyToken (reader.GetBlobBytes (reader.GetAssemblyDefinition ().PublicKey));
			} else {
				return false;
			}
			return name switch {
				"Java.Interop" or "Mono.Android" => token == "84E04FF9CFB79065",
				"System.Runtime" or "System.Runtime.InteropServices" or "System.Diagnostics.Debug" => token == "B03F5F7F11D50A3A",
				"System.Private.CoreLib" => token == "7CEC85D7BEA7798E",
				"mscorlib" => token == "B77A5C561934E089",
				_ => false,
			};
		}
		static string KeyToken (byte [] key)
		{
			if (key.Length == 0)
				return "";
			var name = new AssemblyName ();
			name.SetPublicKey (key);
			return Convert.ToHexString (name.GetPublicKeyToken () ?? throw new BadImageFormatException ("Missing assembly public key token."));
		}
		public static string FunctionPointer (MethodSignature<string> signature)
			=> $"fnptr:{signature.Header.RawValue}:{signature.GenericParameterCount}:({string.Join (",", signature.ParameterTypes)})->{signature.ReturnType}";
		public string GetFunctionPointerType (MethodSignature<string> signature) => FunctionPointer (signature);
		public string GetPrimitiveType (PrimitiveTypeCode code) => SignatureTypeProvider.Instance.GetPrimitiveType (code);
		public string GetTypeFromDefinition (MetadataReader r, TypeDefinitionHandle h, byte kind) => TypeName (h);
		public string GetTypeFromReference (MetadataReader r, TypeReferenceHandle h, byte kind) => TypeName (h);
		public string GetTypeFromSpecification (MetadataReader r, ImmutableArray<string> context, TypeSpecificationHandle h, byte kind)
			=> r.GetTypeSpecification (h).DecodeSignature (this, context);
		public string GetGenericMethodParameter (ImmutableArray<string> context, int index) => !context.IsDefault && index < context.Length ? context [index] : $"!!{index}";
		public string GetGenericTypeParameter (ImmutableArray<string> context, int index) => $"!{index}";
		public string GetGenericInstantiation (string type, ImmutableArray<string> args) => $"{type}<{string.Join (",", args)}>";
		public string GetArrayType (string type, ArrayShape shape) => $"{type}[{new string (',', shape.Rank - 1)}]";
		public string GetSZArrayType (string type) => type + "[]";
		public string GetByReferenceType (string type) => type + "&";
		public string GetPointerType (string type) => type + "*";
		public string GetPinnedType (string type) => type;
		public string GetModifiedType (string modifier, string type, bool required) => $"{type} mod{(required ? "req" : "opt")}({modifier})";
	}
}
