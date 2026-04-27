using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

sealed class ExportMethodDispatchEmitter
{
	readonly PEAssemblyBuilder _pe;
	readonly ExportMethodDispatchEmitterContext _context;

	public ExportMethodDispatchEmitter (PEAssemblyBuilder pe, ExportMethodDispatchEmitterContext context)
	{
		_pe = pe ?? throw new ArgumentNullException (nameof (pe));
		_context = context ?? throw new ArgumentNullException (nameof (context));
	}

	public MethodDefinitionHandle EmitUcoMethod (UcoMethodData uco)
	{
		var exportMethodDispatch = GetRequiredExportMethodDispatch (uco);
		var jniParams = JniSignatureHelper.ParseParameterTypes (uco.JniSignature);
		var returnKind = JniSignatureHelper.ParseReturnType (uco.JniSignature);
		int paramCount = 2 + jniParams.Count;
		bool isVoid = returnKind == JniParamKind.Void;
		var exportMethodDispatchLocals = CreateExportMethodDispatchLocals (exportMethodDispatch, isVoid, returnKind);

		// UCO wrapper signature: uses JNI ABI types (byte for boolean)
		Action<BlobEncoder> encodeSig = sig => sig.MethodSignature ().Parameters (paramCount,
			rt => { if (isVoid) rt.Void (); else JniSignatureHelper.EncodeClrType (rt.Type (), returnKind); },
			p => {
				p.AddParameter ().Type ().IntPtr ();
				p.AddParameter ().Type ().IntPtr ();
				for (int j = 0; j < jniParams.Count; j++) {
					JniSignatureHelper.EncodeClrType (p.AddParameter ().Type (), jniParams [j]);
				}
			});

		// Callback member reference: uses MCW n_* types (sbyte for boolean)
		Action<BlobEncoder> encodeCallbackSig = sig => sig.MethodSignature ().Parameters (paramCount,
			rt => { if (isVoid) rt.Void (); else JniSignatureHelper.EncodeClrTypeForCallback (rt.Type (), returnKind); },
			p => {
				p.AddParameter ().Type ().IntPtr ();
				p.AddParameter ().Type ().IntPtr ();
				for (int j = 0; j < jniParams.Count; j++) {
					JniSignatureHelper.EncodeClrTypeForCallback (p.AddParameter ().Type (), jniParams [j]);
				}
			});

		var callbackTypeHandle = _pe.ResolveTypeRef (uco.CallbackType);
		var callbackRef = AddExportMethodDispatchRef (uco, callbackTypeHandle);

		// Wrap the dispatch in the standard BeginMarshalMethod/try/catch/finally pattern so
		// managed exceptions thrown from the [Export] body are routed through
		// JniRuntime.OnUserUnhandledException — matching the legacy LLVM-IR contract
		// (Mono.Android.Export/CallbackCode.cs) and the trimmable UCO ctor wrapper.
		var handle = _pe.EmitBody (uco.WrapperName,
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			encodeSig,
			(encoder, cfb) => {
				EmitWrappedExportMethodDispatch (encoder, cfb, uco, callbackTypeHandle, callbackRef,
					jniParams, returnKind, exportMethodDispatchLocals);
			},
			exportMethodDispatchLocals.EncodeLocals);

		AddUnmanagedCallersOnlyAttribute (handle);
		return handle;
	}

	void EmitWrappedExportMethodDispatch (InstructionEncoder encoder, ControlFlowBuilder cfb,
		UcoMethodData uco, EntityHandle callbackTypeHandle, MemberReferenceHandle callbackRef,
		List<JniParamKind> jniParams, JniParamKind returnKind, ExportMethodDispatchLocals locals)
	{
		bool isVoid = returnKind == JniParamKind.Void;
		var tryStart = encoder.DefineLabel ();
		var catchStart = encoder.DefineLabel ();
		var finallyStart = encoder.DefineLabel ();
		var afterAll = encoder.DefineLabel ();
		var endCatch = encoder.DefineLabel ();

		// Preamble: if (!BeginMarshalMethod(jnienv, out envp, out runtime)) goto afterAll;
		// On the false path, the ABI return local is zero-initialized (InitLocals=true) so
		// it returns the appropriate default (0 / IntPtr.Zero) for the JNI return kind.
		encoder.LoadArgument (0);
		encoder.LoadLocalAddress (0);
		encoder.LoadLocalAddress (1);
		encoder.Call (_context.BeginMarshalMethodRef);
		encoder.Branch (ILOpCode.Brfalse, afterAll);

		// TRY: dispatch + (if non-void) store ABI return value to the survival local.
		encoder.MarkLabel (tryStart);
		EmitExportMethodDispatch (encoder, uco, callbackTypeHandle, callbackRef, jniParams, returnKind, locals);
		if (!isVoid) {
			encoder.StoreLocal (locals.AbiReturnLocalIndex);
		}
		encoder.Branch (ILOpCode.Leave, afterAll);

		// CATCH (System.Exception e): runtime?.OnUserUnhandledException(ref envp, e);
		encoder.MarkLabel (catchStart);
		encoder.StoreLocal (2);
		encoder.LoadLocal (1);
		encoder.Branch (ILOpCode.Brfalse, endCatch);
		encoder.LoadLocal (1);
		encoder.LoadLocalAddress (0);
		encoder.LoadLocal (2);
		encoder.OpCode (ILOpCode.Callvirt);
		encoder.Token (_context.OnUserUnhandledExceptionRef);
		encoder.MarkLabel (endCatch);
		encoder.Branch (ILOpCode.Leave, afterAll);

		// FINALLY: EndMarshalMethod(ref envp);
		encoder.MarkLabel (finallyStart);
		encoder.LoadLocalAddress (0);
		encoder.Call (_context.EndMarshalMethodRef);
		encoder.OpCode (ILOpCode.Endfinally);

		// AFTER: load ABI return (if non-void) and return.
		encoder.MarkLabel (afterAll);
		if (!isVoid) {
			encoder.LoadLocal (locals.AbiReturnLocalIndex);
		}
		encoder.OpCode (ILOpCode.Ret);

		cfb.AddCatchRegion (tryStart, catchStart, catchStart, finallyStart, _context.ExceptionRef);
		cfb.AddFinallyRegion (tryStart, finallyStart, finallyStart, afterAll);
	}

	sealed class ExportMethodDispatchLocals
	{
		public ExportMethodDispatchLocals (Dictionary<int, int> arrayParameterLocals, int returnLocalIndex, int abiReturnLocalIndex, Action<BlobBuilder> encodeLocals)
		{
			ArrayParameterLocals = arrayParameterLocals;
			ReturnLocalIndex = returnLocalIndex;
			AbiReturnLocalIndex = abiReturnLocalIndex;
			EncodeLocals = encodeLocals;
		}

		public Dictionary<int, int> ArrayParameterLocals { get; }

		/// <summary>Local that holds the managed return value across array copy-backs (-1 if not needed).</summary>
		public int ReturnLocalIndex { get; }

		/// <summary>Local that holds the JNI ABI return value across try/finally so it survives 'leave' (-1 if void).</summary>
		public int AbiReturnLocalIndex { get; }

		public Action<BlobBuilder> EncodeLocals { get; }

		public bool HasArrayParameters => ArrayParameterLocals.Count > 0;
	}

	static ExportMethodDispatchData GetRequiredExportMethodDispatch (UcoMethodData uco)
	{
		return uco.ExportMethodDispatch ?? throw new InvalidOperationException ($"ExportMethodDispatchEmitter only supports UCO methods with ExportMethodDispatch metadata.");
	}

	ExportMethodDispatchLocals CreateExportMethodDispatchLocals (ExportMethodDispatchData exportMethodDispatch, bool isVoid, JniParamKind returnKind)
	{
		// Local layout (fixed prefix shared with the UCO ctor wrapper):
		//   0 = JniTransition envp           (valuetype)
		//   1 = JniRuntime?  runtime         (class)
		//   2 = Exception    e               (class)
		// Then:
		//   3..N = managed array-param copy-back locals (one per array parameter)
		//   (next) = managed return temp     — only when there are array params and return is non-void
		//   (next) = ABI     return temp     — only when return is non-void; survives try/finally → afterAll
		var arrayParameterLocals = new Dictionary<int, int> ();
		var arrayLocalTypes = new List<TypeRefData> ();
		int nextLocalIndex = 3;

		for (int i = 0; i < exportMethodDispatch.ParameterTypes.Count; i++) {
			if (!IsManagedArrayType (exportMethodDispatch.ParameterTypes [i].ManagedTypeName)) {
				continue;
			}

			arrayParameterLocals.Add (i, nextLocalIndex++);
			arrayLocalTypes.Add (exportMethodDispatch.ParameterTypes [i]);
		}

		int returnLocalIndex = -1;
		TypeRefData? managedReturnType = null;
		if (arrayParameterLocals.Count > 0 && !isVoid) {
			returnLocalIndex = nextLocalIndex++;
			managedReturnType = exportMethodDispatch.ReturnType;
		}

		int abiReturnLocalIndex = -1;
		if (!isVoid) {
			abiReturnLocalIndex = nextLocalIndex++;
		}

		return new ExportMethodDispatchLocals (
			arrayParameterLocals,
			returnLocalIndex,
			abiReturnLocalIndex,
			blob => EncodeAllLocals (blob, arrayLocalTypes, managedReturnType, isVoid, returnKind));
	}

	void EncodeAllLocals (BlobBuilder blob, IReadOnlyList<TypeRefData> arrayLocalTypes,
		TypeRefData? managedReturnType, bool isVoid, JniParamKind returnKind)
	{
		int total = 3 + arrayLocalTypes.Count + (managedReturnType is not null ? 1 : 0) + (isVoid ? 0 : 1);

		blob.WriteByte (0x07); // LOCAL_SIG
		blob.WriteCompressedInteger (total);

		// 0: JniTransition (valuetype)
		blob.WriteByte (0x11);
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_context.JniTransitionRef));
		// 1: JniRuntime (class)
		blob.WriteByte (0x12);
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_context.JniRuntimeRef));
		// 2: Exception (class)
		blob.WriteByte (0x12);
		blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_context.ExceptionRef));

		// 3..N: managed array-parameter copy-back locals
		foreach (var localType in arrayLocalTypes) {
			EncodeManagedType (new SignatureTypeEncoder (blob), localType);
		}

		// Managed return temp (managed type — same encoding as method parameters)
		if (managedReturnType is not null) {
			EncodeManagedType (new SignatureTypeEncoder (blob), managedReturnType);
		}

		// ABI return temp (JNI ABI type — byte for boolean, IntPtr for object handles, etc.)
		if (!isVoid) {
			JniSignatureHelper.EncodeClrType (new SignatureTypeEncoder (blob), returnKind);
		}
	}

	static bool IsManagedArrayType (string managedTypeName)
		=> managedTypeName.EndsWith ("[]", StringComparison.Ordinal);

	MemberReferenceHandle AddExportMethodDispatchRef (UcoMethodData uco, EntityHandle callbackTypeHandle)
	{
		var exportMethodDispatch = GetRequiredExportMethodDispatch (uco);

		return _pe.AddMemberRef (callbackTypeHandle, exportMethodDispatch.ManagedMethodName,
			sig => sig.MethodSignature (isInstanceMethod: !exportMethodDispatch.IsStatic).Parameters (exportMethodDispatch.ParameterTypes.Count,
				rt => {
					if (exportMethodDispatch.ReturnType.ManagedTypeName == "System.Void") {
						rt.Void ();
					} else {
						EncodeManagedType (rt.Type (), exportMethodDispatch.ReturnType);
					}
				},
				p => {
					for (int i = 0; i < exportMethodDispatch.ParameterTypes.Count; i++) {
						EncodeManagedType (p.AddParameter ().Type (), exportMethodDispatch.ParameterTypes [i]);
					}
				}));
	}

	void EmitExportMethodDispatch (InstructionEncoder encoder, UcoMethodData uco, EntityHandle callbackTypeHandle,
		MemberReferenceHandle callbackRef, List<JniParamKind> jniParams, JniParamKind returnKind,
		ExportMethodDispatchLocals exportMethodDispatchLocals)
	{
		var exportMethodDispatch = GetRequiredExportMethodDispatch (uco);

		if (!exportMethodDispatch.IsStatic) {
			encoder.LoadArgument (1);
			encoder.LoadConstantI4 (0);
			EmitManagedTypeToken (encoder, callbackTypeHandle);
			encoder.Call (_context.JavaLangObjectGetObjectRef);
			encoder.OpCode (ILOpCode.Castclass);
			encoder.Token (callbackTypeHandle);
		}

		for (int i = 0; i < exportMethodDispatch.ParameterTypes.Count; i++) {
			LoadManagedArgument (encoder,
				exportMethodDispatch.ParameterTypes [i],
				GetExportMethodDispatchParameterKind (exportMethodDispatch, i),
				jniParams [i],
				2 + i);

			if (exportMethodDispatchLocals.ArrayParameterLocals.TryGetValue (i, out var localIndex)) {
				encoder.StoreLocal (localIndex);
				encoder.LoadLocal (localIndex);
			}
		}

		if (exportMethodDispatch.IsStatic) {
			encoder.Call (callbackRef);
		} else {
			encoder.OpCode (ILOpCode.Callvirt);
			encoder.Token (callbackRef);
		}

		EmitManagedArrayCopyBacks (encoder, exportMethodDispatch, returnKind, exportMethodDispatchLocals);
		ConvertManagedReturnValue (encoder, exportMethodDispatch.ReturnType, exportMethodDispatch.ReturnKind, returnKind);
	}

	static ExportParameterKindInfo GetExportMethodDispatchParameterKind (ExportMethodDispatchData exportMethodDispatch, int index)
		=> index < exportMethodDispatch.ParameterKinds.Count ? exportMethodDispatch.ParameterKinds [index] : ExportParameterKindInfo.Unspecified;

	void EmitManagedArrayCopyBacks (InstructionEncoder encoder, ExportMethodDispatchData exportMethodDispatch, JniParamKind returnKind, ExportMethodDispatchLocals exportMethodDispatchLocals)
	{
		if (!exportMethodDispatchLocals.HasArrayParameters) {
			return;
		}

		if (returnKind != JniParamKind.Void) {
			encoder.StoreLocal (exportMethodDispatchLocals.ReturnLocalIndex);
		}

		foreach (var kvp in exportMethodDispatchLocals.ArrayParameterLocals) {
			var skipCopy = encoder.DefineLabel ();
			encoder.LoadLocal (kvp.Value);
			encoder.Branch (ILOpCode.Brfalse_s, skipCopy);
			encoder.LoadLocal (kvp.Value);
			EmitManagedArrayElementTypeToken (encoder, exportMethodDispatch.ParameterTypes [kvp.Key]);
			encoder.LoadArgument (2 + kvp.Key);
			encoder.Call (_context.JniEnvCopyArrayRef);
			encoder.MarkLabel (skipCopy);
		}

		if (returnKind != JniParamKind.Void) {
			encoder.LoadLocal (exportMethodDispatchLocals.ReturnLocalIndex);
		}
	}

	/// <summary>
	/// Emits IL that loads JNI argument <paramref name="argumentIndex"/> onto the
	/// stack and converts it to the managed type expected by the user-visible
	/// method or constructor parameter. Handles primitives (with <c>byte → bool</c>
	/// conversion for <c>System.Boolean</c>), strings, arrays, <c>[Export]</c>
	/// parameter kinds (streams / XML parsers), and object peers via
	/// <c>Java.Lang.Object.GetObject (IntPtr, JniHandleOwnership, Type)</c>.
	/// </summary>
	internal void LoadManagedArgument (InstructionEncoder encoder, TypeRefData managedType, ExportParameterKindInfo exportKind, JniParamKind jniKind, int argumentIndex)
	{
		string managedTypeName = managedType.ManagedTypeName;

		ThrowIfUnsupportedManagedType (managedTypeName);

		if (TryEmitExportParameterArgument (encoder, exportKind, argumentIndex)) {
			return;
		}

		if (TryEmitPrimitiveManagedArgument (encoder, managedTypeName, argumentIndex)) {
			return;
		}

		if (jniKind != JniParamKind.Object) {
			encoder.LoadArgument (argumentIndex);
			return;
		}

		if (IsManagedArrayType (managedTypeName)) {
			encoder.LoadArgument (argumentIndex);
			encoder.LoadConstantI4 (0);
			EmitManagedArrayElementTypeToken (encoder, managedType);
			encoder.Call (_context.JniEnvGetArrayRef);
			encoder.OpCode (ILOpCode.Castclass);
			encoder.Token (ResolveManagedTypeHandle (managedType));
			return;
		}

		EmitManagedObjectArgument (encoder, managedType, argumentIndex);
	}

	void ConvertManagedReturnValue (InstructionEncoder encoder, TypeRefData managedReturnType, ExportParameterKindInfo exportKind, JniParamKind returnKind)
	{
		string managedReturnTypeName = managedReturnType.ManagedTypeName;

		if (returnKind == JniParamKind.Void) {
			return;
		}

		if (returnKind != JniParamKind.Object) {
			if (managedReturnTypeName == "System.Boolean") {
				encoder.OpCode (ILOpCode.Conv_u1);
			}
			return;
		}

		if (managedReturnTypeName == "System.String") {
			encoder.Call (_context.JniEnvNewStringRef);
			return;
		}

		if (managedReturnTypeName == "System.Void") {
			return;
		}

		if (IsManagedArrayType (managedReturnTypeName)) {
			EmitManagedArrayReturn (encoder, managedReturnType);
			return;
		}

		if (TryEmitExportParameterReturn (encoder, exportKind)) {
			return;
		}

		encoder.OpCode (ILOpCode.Castclass);
		encoder.Token (_context.IJavaObjectRef);
		encoder.Call (_context.JniEnvToLocalJniHandleRef);
	}

	void ThrowIfUnsupportedManagedType (string managedTypeName)
	{
		if (managedTypeName.EndsWith ("&", StringComparison.Ordinal) || managedTypeName.EndsWith ("*", StringComparison.Ordinal)) {
			throw new NotSupportedException ($"[Export] methods with by-ref or pointer signature types are not supported: '{managedTypeName}'.");
		}

		if (managedTypeName.IndexOf ('<') >= 0) {
			throw new NotSupportedException ($"[Export] methods with generic signature types are not supported: '{managedTypeName}'.");
		}
	}

	bool TryEmitExportParameterArgument (InstructionEncoder encoder, ExportParameterKindInfo exportKind, int argumentIndex)
	{
		switch (exportKind) {
			case ExportParameterKindInfo.InputStream:
				encoder.LoadArgument (argumentIndex);
				encoder.LoadConstantI4 (0);
				encoder.Call (_context.InputStreamInvokerFromJniHandleRef);
				return true;
			case ExportParameterKindInfo.OutputStream:
				encoder.LoadArgument (argumentIndex);
				encoder.LoadConstantI4 (0);
				encoder.Call (_context.OutputStreamInvokerFromJniHandleRef);
				return true;
			case ExportParameterKindInfo.XmlPullParser:
				encoder.LoadArgument (argumentIndex);
				encoder.LoadConstantI4 (0);
				encoder.Call (_context.XmlPullParserReaderFromJniHandleRef);
				return true;
			case ExportParameterKindInfo.XmlResourceParser:
				encoder.LoadArgument (argumentIndex);
				encoder.LoadConstantI4 (0);
				encoder.Call (_context.XmlResourceParserReaderFromJniHandleRef);
				return true;
			default:
				return false;
		}
	}

	bool TryEmitPrimitiveManagedArgument (InstructionEncoder encoder, string managedTypeName, int argumentIndex)
	{
		switch (managedTypeName) {
			case "System.Boolean":
				encoder.LoadArgument (argumentIndex);
				encoder.LoadConstantI4 (0);
				encoder.OpCode (ILOpCode.Cgt_un);
				return true;
			case "System.Byte":
			case "System.SByte":
			case "System.Char":
			case "System.Int16":
			case "System.UInt16":
			case "System.Int32":
			case "System.UInt32":
			case "System.Int64":
			case "System.UInt64":
			case "System.Single":
			case "System.Double":
			case "System.IntPtr":
				encoder.LoadArgument (argumentIndex);
				return true;
			case "System.String":
				encoder.LoadArgument (argumentIndex);
				encoder.LoadConstantI4 (0);
				encoder.Call (_context.JniEnvGetStringRef);
				return true;
			default:
				return false;
		}
	}

	void EmitManagedObjectArgument (InstructionEncoder encoder, TypeRefData managedType, int argumentIndex)
	{
		encoder.LoadArgument (argumentIndex);
		encoder.LoadConstantI4 (0);
		if (managedType.ManagedTypeName == "System.Object") {
			encoder.OpCode (ILOpCode.Ldnull);
		} else {
			EmitManagedTypeToken (encoder, ResolveManagedTypeHandle (managedType));
		}
		encoder.Call (_context.JavaLangObjectGetObjectRef);

		if (managedType.ManagedTypeName != "System.Object") {
			var managedTypeHandle = ResolveManagedTypeHandle (managedType);
			encoder.OpCode (ILOpCode.Castclass);
			encoder.Token (managedTypeHandle);
		}
	}

	void EmitManagedArrayReturn (InstructionEncoder encoder, TypeRefData managedReturnType)
	{
		var nonNullArray = encoder.DefineLabel ();
		var done = encoder.DefineLabel ();

		encoder.OpCode (ILOpCode.Dup);
		encoder.Branch (ILOpCode.Brtrue_s, nonNullArray);
		encoder.OpCode (ILOpCode.Pop);
		encoder.LoadConstantI4 (0);
		encoder.Branch (ILOpCode.Br_s, done);
		encoder.MarkLabel (nonNullArray);
		EmitManagedArrayElementTypeToken (encoder, managedReturnType);
		encoder.Call (_context.JniEnvNewArrayRef);
		encoder.MarkLabel (done);
	}

	bool TryEmitExportParameterReturn (InstructionEncoder encoder, ExportParameterKindInfo exportKind)
	{
		switch (exportKind) {
			case ExportParameterKindInfo.InputStream:
				encoder.Call (_context.InputStreamAdapterToLocalJniHandleRef);
				return true;
			case ExportParameterKindInfo.OutputStream:
				encoder.Call (_context.OutputStreamAdapterToLocalJniHandleRef);
				return true;
			case ExportParameterKindInfo.XmlPullParser:
				encoder.Call (_context.XmlReaderPullParserToLocalJniHandleRef);
				return true;
			case ExportParameterKindInfo.XmlResourceParser:
				encoder.Call (_context.XmlReaderResourceParserToLocalJniHandleRef);
				return true;
			default:
				return false;
		}
	}

	void EmitManagedTypeToken (InstructionEncoder encoder, EntityHandle typeHandle)
	{
		encoder.OpCode (ILOpCode.Ldtoken);
		encoder.Token (typeHandle);
		encoder.Call (_context.GetTypeFromHandleRef);
	}

	void EmitManagedArrayElementTypeToken (InstructionEncoder encoder, TypeRefData arrayType)
	{
		var elementType = arrayType with {
			ManagedTypeName = arrayType.ManagedTypeName.Substring (0, arrayType.ManagedTypeName.Length - 2),
		};
		EmitManagedTypeToken (encoder, ResolveManagedTypeHandle (elementType));
	}

	EntityHandle ResolveManagedTypeHandle (TypeRefData managedType)
	{
		if (IsManagedArrayType (managedType.ManagedTypeName)) {
			var blob = new BlobBuilder ();
			EncodeManagedType (new SignatureTypeEncoder (blob), managedType);
			return _pe.Metadata.AddTypeSpecification (_pe.Metadata.GetOrAddBlob (blob));
		}

		return _pe.ResolveTypeRef (managedType);
	}

	void EncodeManagedType (SignatureTypeEncoder encoder, TypeRefData managedType)
	{
		string managedTypeName = managedType.ManagedTypeName;

		ThrowIfUnsupportedManagedType (managedTypeName);
		if (managedTypeName.EndsWith ("[]", StringComparison.Ordinal)) {
			EncodeManagedType (encoder.SZArray (), managedType with {
				ManagedTypeName = managedTypeName.Substring (0, managedTypeName.Length - 2),
			});
			return;
		}

		switch (managedTypeName) {
			case "System.Boolean": encoder.Boolean (); return;
			case "System.Byte": encoder.Byte (); return;
			case "System.SByte": encoder.SByte (); return;
			case "System.Char": encoder.Char (); return;
			case "System.Int16": encoder.Int16 (); return;
			case "System.UInt16": encoder.UInt16 (); return;
			case "System.Int32": encoder.Int32 (); return;
			case "System.UInt32": encoder.UInt32 (); return;
			case "System.Int64": encoder.Int64 (); return;
			case "System.UInt64": encoder.UInt64 (); return;
			case "System.Single": encoder.Single (); return;
			case "System.Double": encoder.Double (); return;
			case "System.String": encoder.String (); return;
			case "System.Object": encoder.Object (); return;
			case "System.IntPtr": encoder.IntPtr (); return;
		}

		var typeHandle = ResolveManagedTypeHandle (managedType);
		encoder.Type (typeHandle, isValueType: false);
	}

	void AddUnmanagedCallersOnlyAttribute (MethodDefinitionHandle handle)
	{
		_pe.Metadata.AddCustomAttribute (handle, _context.UcoAttrCtorRef, _context.UcoAttrBlobHandle);
	}

}

