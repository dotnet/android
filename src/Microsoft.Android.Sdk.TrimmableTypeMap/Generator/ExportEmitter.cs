using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

sealed class ExportEmitter
{
	readonly PEAssemblyBuilder _pe;
	readonly ExportEmitterContext _context;

	public ExportEmitter (PEAssemblyBuilder pe, ExportEmitterContext context)
	{
		_pe = pe ?? throw new ArgumentNullException (nameof (pe));
		_context = context ?? throw new ArgumentNullException (nameof (context));
	}

	public MethodDefinitionHandle EmitUcoMethod (UcoMethodData uco)
	{
		var jniParams = JniSignatureHelper.ParseParameterTypes (uco.JniSignature);
		var returnKind = JniSignatureHelper.ParseReturnType (uco.JniSignature);
		int paramCount = 2 + jniParams.Count;
		bool isVoid = returnKind == JniParamKind.Void;
		var dispatchLocals = uco.UseDirectManagedDispatch
			? CreateDirectDispatchLocals (uco, isVoid)
			: DirectDispatchLocals.Empty;

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
		var callbackRef = uco.UseDirectManagedDispatch
			? AddDirectManagedDispatchRef (uco, callbackTypeHandle)
			: _pe.AddMemberRef (callbackTypeHandle, uco.CallbackMethodName, encodeCallbackSig);

		var handle = _pe.EmitBody (uco.WrapperName,
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			encodeSig,
			encoder => {
				if (!uco.UseDirectManagedDispatch) {
					for (int p = 0; p < paramCount; p++) {
						encoder.LoadArgument (p);
					}

					encoder.Call (callbackRef);
				} else {
					EmitDirectManagedDispatch (encoder, uco, callbackTypeHandle, callbackRef, jniParams, returnKind, dispatchLocals);
				}
				encoder.OpCode (ILOpCode.Ret);
			},
			dispatchLocals.EncodeLocals,
			useBranches: uco.UseDirectManagedDispatch);

		AddUnmanagedCallersOnlyAttribute (handle);
		return handle;
	}

	public MethodDefinitionHandle EmitUcoConstructor (UcoConstructorData uco)
	{
		var userTypeRef = _pe.ResolveTypeRef (uco.TargetType);

		// UCO constructor wrappers must match the JNI native method signature exactly.
		var jniParams = JniSignatureHelper.ParseParameterTypes (uco.JniSignature);
		int paramCount = 2 + jniParams.Count;

		var handle = _pe.EmitBody (uco.WrapperName,
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			sig => sig.MethodSignature ().Parameters (paramCount,
				rt => rt.Void (),
				p => {
					p.AddParameter ().Type ().IntPtr ();
					p.AddParameter ().Type ().IntPtr ();
					for (int j = 0; j < jniParams.Count; j++) {
						JniSignatureHelper.EncodeClrType (p.AddParameter ().Type (), jniParams [j]);
					}
				}),
			encoder => {
				encoder.LoadArgument (1);
				encoder.OpCode (ILOpCode.Ldtoken);
				encoder.Token (userTypeRef);
				encoder.Call (_context.GetTypeFromHandleRef);
				encoder.Call (_context.ActivateInstanceRef);
				encoder.OpCode (ILOpCode.Ret);
			});

		AddUnmanagedCallersOnlyAttribute (handle);
		return handle;
	}

	public void EmitRegisterNatives (List<NativeRegistrationData> registrations, Dictionary<string, MethodDefinitionHandle> wrapperHandles)
	{
		var validRegs = new List<(NativeRegistrationData Reg, MethodDefinitionHandle Wrapper)> (registrations.Count);
		foreach (var reg in registrations) {
			if (wrapperHandles.TryGetValue (reg.WrapperMethodName, out var wrapperHandle)) {
				validRegs.Add ((reg, wrapperHandle));
			}
		}

		if (validRegs.Count == 0) {
			_pe.EmitBody ("RegisterNatives",
				MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig |
				MethodAttributes.NewSlot | MethodAttributes.Final,
				sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
					rt => rt.Void (),
					p => p.AddParameter ().Type ().Type (_context.JniTypeRef, false)),
				encoder => encoder.OpCode (ILOpCode.Ret));
			return;
		}

		var nameFields = new FieldDefinitionHandle [validRegs.Count];
		var sigFields = new FieldDefinitionHandle [validRegs.Count];
		for (int i = 0; i < validRegs.Count; i++) {
			nameFields [i] = _pe.GetOrAddUtf8Field (validRegs [i].Reg.JniMethodName);
			sigFields [i] = _pe.GetOrAddUtf8Field (validRegs [i].Reg.JniSignature);
		}

		int methodCount = validRegs.Count;

		_pe.EmitBody ("RegisterNatives",
			MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig |
			MethodAttributes.NewSlot | MethodAttributes.Final,
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().Type (_context.JniTypeRef, false)),
			encoder => {
				encoder.LoadConstantI4 (methodCount);
				encoder.OpCode (ILOpCode.Sizeof);
				encoder.Token (_context.JniNativeMethodRef);
				encoder.OpCode (ILOpCode.Mul);
				encoder.OpCode (ILOpCode.Localloc);
				encoder.StoreLocal (0);

				for (int i = 0; i < methodCount; i++) {
					encoder.LoadLocal (0);
					if (i > 0) {
						encoder.LoadConstantI4 (i);
						encoder.OpCode (ILOpCode.Sizeof);
						encoder.Token (_context.JniNativeMethodRef);
						encoder.OpCode (ILOpCode.Mul);
						encoder.OpCode (ILOpCode.Add);
					}

					encoder.OpCode (ILOpCode.Ldsflda);
					encoder.Token (nameFields [i]);

					encoder.OpCode (ILOpCode.Ldsflda);
					encoder.Token (sigFields [i]);

					encoder.OpCode (ILOpCode.Ldftn);
					encoder.Token (validRegs [i].Wrapper);

					encoder.OpCode (ILOpCode.Newobj);
					encoder.Token (_context.JniNativeMethodCtorRef);
					encoder.OpCode (ILOpCode.Stobj);
					encoder.Token (_context.JniNativeMethodRef);
				}

				encoder.LoadArgument (1);
				encoder.OpCode (ILOpCode.Callvirt);
				encoder.Token (_context.JniTypePeerReferenceRef);
				encoder.StoreLocal (1);

				encoder.LoadLocalAddress (2);
				encoder.LoadLocal (0);
				encoder.LoadConstantI4 (methodCount);
				encoder.Call (_context.ReadOnlySpanOfJniNativeMethodCtorRef);

				encoder.LoadLocal (1);
				encoder.LoadLocal (2);
				encoder.Call (_context.JniEnvTypesRegisterNativesRef);
				encoder.OpCode (ILOpCode.Ret);
			},
			encodeLocals: localSig => {
				localSig.WriteByte (0x07);
				localSig.WriteCompressedInteger (3);
				localSig.WriteByte (0x18);
				localSig.WriteByte (0x11);
				localSig.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (_context.JniObjectReferenceRef));
				EncodeGenericValueTypeInst (localSig, _context.ReadOnlySpanOpenRef, _context.JniNativeMethodRef);
			});
	}

	sealed class DirectDispatchLocals
	{
		public static readonly DirectDispatchLocals Empty = new (new Dictionary<int, int> (), -1, null);

		public DirectDispatchLocals (Dictionary<int, int> arrayParameterLocals, int returnLocalIndex, Action<BlobBuilder>? encodeLocals)
		{
			ArrayParameterLocals = arrayParameterLocals;
			ReturnLocalIndex = returnLocalIndex;
			EncodeLocals = encodeLocals;
		}

		public Dictionary<int, int> ArrayParameterLocals { get; }
		public int ReturnLocalIndex { get; }
		public Action<BlobBuilder>? EncodeLocals { get; }

		public bool HasArrayParameters => ArrayParameterLocals.Count > 0;
	}

	DirectDispatchLocals CreateDirectDispatchLocals (UcoMethodData uco, bool isVoid)
	{
		var localTypes = new List<TypeRefData> ();
		var arrayParameterLocals = new Dictionary<int, int> ();

		for (int i = 0; i < uco.ManagedParameterTypeNames.Count; i++) {
			if (!IsManagedArrayType (uco.ManagedParameterTypeNames [i])) {
				continue;
			}

			arrayParameterLocals.Add (i, localTypes.Count);
			localTypes.Add (GetManagedParameterType (uco, i));
		}

		int returnLocalIndex = -1;
		if (arrayParameterLocals.Count > 0 && !isVoid) {
			returnLocalIndex = localTypes.Count;
			localTypes.Add (GetManagedReturnType (uco));
		}

		return new DirectDispatchLocals (
			arrayParameterLocals,
			returnLocalIndex,
			localTypes.Count > 0 ? blob => EncodeManagedLocals (blob, localTypes) : null);
	}

	void EncodeManagedLocals (BlobBuilder blob, IReadOnlyList<TypeRefData> localTypes)
	{
		blob.WriteByte (0x07);
		blob.WriteCompressedInteger (localTypes.Count);
		foreach (var localType in localTypes) {
			EncodeManagedType (new SignatureTypeEncoder (blob), localType);
		}
	}

	static bool IsManagedArrayType (string managedTypeName)
		=> managedTypeName.EndsWith ("[]", StringComparison.Ordinal);

	static TypeRefData GetManagedParameterType (UcoMethodData uco, int index)
	{
		if (index < uco.ManagedParameterTypes.Count) {
			return uco.ManagedParameterTypes [index];
		}

		return new TypeRefData {
			ManagedTypeName = uco.ManagedParameterTypeNames [index],
			AssemblyName = uco.CallbackType.AssemblyName,
		};
	}

	static TypeRefData GetManagedReturnType (UcoMethodData uco)
	{
		if (uco.ManagedReturnType.ManagedTypeName.Length > 0) {
			return uco.ManagedReturnType;
		}

		return new TypeRefData {
			ManagedTypeName = uco.ManagedReturnTypeName,
			AssemblyName = uco.CallbackType.AssemblyName,
		};
	}

	MemberReferenceHandle AddDirectManagedDispatchRef (UcoMethodData uco, EntityHandle callbackTypeHandle)
	{
		return _pe.AddMemberRef (callbackTypeHandle, uco.ManagedMethodName,
			sig => sig.MethodSignature (isInstanceMethod: !uco.IsStatic).Parameters (uco.ManagedParameterTypeNames.Count,
				rt => {
					if (uco.ManagedReturnTypeName == "System.Void") {
						rt.Void ();
					} else {
						EncodeManagedType (rt.Type (), GetManagedReturnType (uco));
					}
				},
				p => {
					for (int i = 0; i < uco.ManagedParameterTypeNames.Count; i++) {
						EncodeManagedType (p.AddParameter ().Type (), GetManagedParameterType (uco, i));
					}
				}));
	}

	void EmitDirectManagedDispatch (InstructionEncoder encoder, UcoMethodData uco, EntityHandle callbackTypeHandle,
		MemberReferenceHandle callbackRef, List<JniParamKind> jniParams, JniParamKind returnKind,
		DirectDispatchLocals dispatchLocals)
	{
		if (!uco.IsStatic) {
			encoder.LoadArgument (1);
			encoder.LoadConstantI4 (0);
			EmitManagedTypeToken (encoder, callbackTypeHandle);
			encoder.Call (_context.JavaLangObjectGetObjectRef);
			encoder.OpCode (ILOpCode.Castclass);
			encoder.Token (callbackTypeHandle);
		}

		for (int i = 0; i < uco.ManagedParameterTypeNames.Count; i++) {
			LoadManagedArgument (encoder,
				GetManagedParameterType (uco, i),
				GetManagedParameterExportKind (uco, i),
				jniParams [i],
				2 + i);

			if (dispatchLocals.ArrayParameterLocals.TryGetValue (i, out var localIndex)) {
				encoder.StoreLocal (localIndex);
				encoder.LoadLocal (localIndex);
			}
		}

		if (uco.IsStatic) {
			encoder.Call (callbackRef);
		} else {
			encoder.OpCode (ILOpCode.Callvirt);
			encoder.Token (callbackRef);
		}

		EmitManagedArrayCopyBacks (encoder, uco, returnKind, dispatchLocals);
		ConvertManagedReturnValue (encoder, GetManagedReturnType (uco), uco.ManagedReturnExportKind, returnKind);
	}

	static ExportParameterKindInfo GetManagedParameterExportKind (UcoMethodData uco, int index)
		=> index < uco.ManagedParameterExportKinds.Count ? uco.ManagedParameterExportKinds [index] : ExportParameterKindInfo.Unspecified;

	void EmitManagedArrayCopyBacks (InstructionEncoder encoder, UcoMethodData uco, JniParamKind returnKind, DirectDispatchLocals dispatchLocals)
	{
		if (!dispatchLocals.HasArrayParameters) {
			return;
		}

		if (returnKind != JniParamKind.Void) {
			encoder.StoreLocal (dispatchLocals.ReturnLocalIndex);
		}

		foreach (var kvp in dispatchLocals.ArrayParameterLocals) {
			var skipCopy = encoder.DefineLabel ();
			encoder.LoadLocal (kvp.Value);
			encoder.Branch (ILOpCode.Brfalse_s, skipCopy);
			encoder.LoadLocal (kvp.Value);
			EmitManagedArrayElementTypeToken (encoder, GetManagedParameterType (uco, kvp.Key));
			encoder.LoadArgument (2 + kvp.Key);
			encoder.Call (_context.JniEnvCopyArrayRef);
			encoder.MarkLabel (skipCopy);
		}

		if (returnKind != JniParamKind.Void) {
			encoder.LoadLocal (dispatchLocals.ReturnLocalIndex);
		}
	}

	void LoadManagedArgument (InstructionEncoder encoder, TypeRefData managedType, ExportParameterKindInfo exportKind, JniParamKind jniKind, int argumentIndex)
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
		encoder.LoadArgument (argumentIndex);
		encoder.LoadConstantI4 (0);

		switch (exportKind) {
			case ExportParameterKindInfo.InputStream:
				encoder.Call (_context.InputStreamInvokerFromJniHandleRef);
				return true;
			case ExportParameterKindInfo.OutputStream:
				encoder.Call (_context.OutputStreamInvokerFromJniHandleRef);
				return true;
			case ExportParameterKindInfo.XmlPullParser:
				encoder.Call (_context.XmlPullParserReaderFromJniHandleRef);
				return true;
			case ExportParameterKindInfo.XmlResourceParser:
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

	static void EncodeGenericValueTypeInst (BlobBuilder builder, EntityHandle openType, EntityHandle valueTypeArg)
	{
		builder.WriteByte (0x15);
		builder.WriteByte (0x11);
		builder.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (openType));
		builder.WriteCompressedInteger (1);
		builder.WriteByte (0x11);
		builder.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (valueTypeArg));
	}
}

sealed class ExportEmitterContext
{
	public required TypeReferenceHandle JniObjectReferenceRef { get; init; }
	public required TypeReferenceHandle IJavaObjectRef { get; init; }
	public required TypeReferenceHandle JniTypeRef { get; init; }
	public required TypeReferenceHandle JniNativeMethodRef { get; init; }
	public required TypeReferenceHandle ReadOnlySpanOpenRef { get; init; }

	public required MemberReferenceHandle GetTypeFromHandleRef { get; init; }
	public required MemberReferenceHandle JniEnvGetStringRef { get; init; }
	public required MemberReferenceHandle JniEnvGetArrayRef { get; init; }
	public required MemberReferenceHandle JniEnvCopyArrayRef { get; init; }
	public required MemberReferenceHandle JniEnvNewArrayRef { get; init; }
	public required MemberReferenceHandle JniEnvNewStringRef { get; init; }
	public required MemberReferenceHandle JniEnvToLocalJniHandleRef { get; init; }
	public required MemberReferenceHandle JavaLangObjectGetObjectRef { get; init; }
	public required MemberReferenceHandle InputStreamInvokerFromJniHandleRef { get; init; }
	public required MemberReferenceHandle OutputStreamInvokerFromJniHandleRef { get; init; }
	public required MemberReferenceHandle InputStreamAdapterToLocalJniHandleRef { get; init; }
	public required MemberReferenceHandle OutputStreamAdapterToLocalJniHandleRef { get; init; }
	public required MemberReferenceHandle XmlPullParserReaderFromJniHandleRef { get; init; }
	public required MemberReferenceHandle XmlResourceParserReaderFromJniHandleRef { get; init; }
	public required MemberReferenceHandle XmlReaderPullParserToLocalJniHandleRef { get; init; }
	public required MemberReferenceHandle XmlReaderResourceParserToLocalJniHandleRef { get; init; }
	public required MemberReferenceHandle ActivateInstanceRef { get; init; }
	public required MemberReferenceHandle JniNativeMethodCtorRef { get; init; }
	public required MemberReferenceHandle JniTypePeerReferenceRef { get; init; }
	public required MemberReferenceHandle JniEnvTypesRegisterNativesRef { get; init; }
	public required MemberReferenceHandle ReadOnlySpanOfJniNativeMethodCtorRef { get; init; }
	public required MemberReferenceHandle UcoAttrCtorRef { get; init; }

	public required BlobHandle UcoAttrBlobHandle { get; init; }
}
