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
		var exportMethodDispatchLocals = CreateExportMethodDispatchLocals (exportMethodDispatch, isVoid);

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

		var handle = _pe.EmitBody (uco.WrapperName,
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
			encodeSig,
			encoder => {
				EmitExportMethodDispatch (encoder, uco, callbackTypeHandle, callbackRef, jniParams, returnKind, exportMethodDispatchLocals);
				encoder.OpCode (ILOpCode.Ret);
			},
			exportMethodDispatchLocals.EncodeLocals,
			useBranches: uco.UsesExportMethodDispatch);

		AddUnmanagedCallersOnlyAttribute (handle);
		return handle;
	}

	sealed class ExportMethodDispatchLocals
	{
		public static readonly ExportMethodDispatchLocals Empty = new (new Dictionary<int, int> (), -1, null);

		public ExportMethodDispatchLocals (Dictionary<int, int> arrayParameterLocals, int returnLocalIndex, Action<BlobBuilder>? encodeLocals)
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

	static ExportMethodDispatchData GetRequiredExportMethodDispatch (UcoMethodData uco)
	{
		return uco.ExportMethodDispatch ?? throw new InvalidOperationException ($"ExportMethodDispatchEmitter only supports UCO methods with ExportMethodDispatch metadata.");
	}

	ExportMethodDispatchLocals CreateExportMethodDispatchLocals (ExportMethodDispatchData exportMethodDispatch, bool isVoid)
	{
		var localTypes = new List<TypeRefData> ();
		var arrayParameterLocals = new Dictionary<int, int> ();

		for (int i = 0; i < exportMethodDispatch.ParameterTypes.Count; i++) {
			if (!IsManagedArrayType (exportMethodDispatch.ParameterTypes [i].ManagedTypeName)) {
				continue;
			}

			arrayParameterLocals.Add (i, localTypes.Count);
			localTypes.Add (exportMethodDispatch.ParameterTypes [i]);
		}

		int returnLocalIndex = -1;
		if (arrayParameterLocals.Count > 0 && !isVoid) {
			returnLocalIndex = localTypes.Count;
			localTypes.Add (exportMethodDispatch.ReturnType);
		}

		return new ExportMethodDispatchLocals (
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

sealed class ExportMethodDispatchEmitterContext
{
	public static ExportMethodDispatchEmitterContext Create (
		PEAssemblyBuilder pe,
		TypeReferenceHandle iJavaPeerableRef,
		TypeReferenceHandle jniHandleOwnershipRef,
		TypeReferenceHandle jniEnvRef,
		TypeReferenceHandle systemTypeRef,
		MemberReferenceHandle getTypeFromHandleRef,
		MemberReferenceHandle ucoAttrCtorRef,
		BlobHandle ucoAttrBlobHandle)
	{
		var metadata = pe.Metadata;
		var iJavaObjectRef = metadata.AddTypeReference (pe.MonoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("IJavaObject"));
		var javaLangObjectRef = metadata.AddTypeReference (pe.MonoAndroidRef,
			metadata.GetOrAddString ("Java.Lang"), metadata.GetOrAddString ("Object"));
		var systemArrayRef = metadata.AddTypeReference (pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("Array"));
		var systemStreamRef = metadata.AddTypeReference (pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System.IO"), metadata.GetOrAddString ("Stream"));
		var systemXmlRef = pe.FindOrAddAssemblyRef ("System.Xml.ReaderWriter");
		var systemXmlReaderRef = metadata.AddTypeReference (systemXmlRef,
			metadata.GetOrAddString ("System.Xml"), metadata.GetOrAddString ("XmlReader"));
		var inputStreamInvokerRef = metadata.AddTypeReference (pe.MonoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("InputStreamInvoker"));
		var outputStreamInvokerRef = metadata.AddTypeReference (pe.MonoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("OutputStreamInvoker"));
		var inputStreamAdapterRef = metadata.AddTypeReference (pe.MonoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("InputStreamAdapter"));
		var outputStreamAdapterRef = metadata.AddTypeReference (pe.MonoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("OutputStreamAdapter"));
		var xmlPullParserReaderRef = metadata.AddTypeReference (pe.MonoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("XmlPullParserReader"));
		var xmlResourceParserReaderRef = metadata.AddTypeReference (pe.MonoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("XmlResourceParserReader"));
		var xmlReaderPullParserRef = metadata.AddTypeReference (pe.MonoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("XmlReaderPullParser"));
		var xmlReaderResourceParserRef = metadata.AddTypeReference (pe.MonoAndroidRef,
			metadata.GetOrAddString ("Android.Runtime"), metadata.GetOrAddString ("XmlReaderResourceParser"));

		return new ExportMethodDispatchEmitterContext {
			IJavaObjectRef = iJavaObjectRef,
			GetTypeFromHandleRef = getTypeFromHandleRef,
			JniEnvGetStringRef = pe.AddMemberRef (jniEnvRef, "GetString",
				sig => sig.MethodSignature ().Parameters (2,
					rt => rt.Type ().String (),
					p => {
						p.AddParameter ().Type ().IntPtr ();
						p.AddParameter ().Type ().Type (jniHandleOwnershipRef, true);
					})),
			JniEnvGetArrayRef = pe.AddMemberRef (jniEnvRef, "GetArray",
				sig => sig.MethodSignature ().Parameters (3,
					rt => rt.Type ().Type (systemArrayRef, false),
					p => {
						p.AddParameter ().Type ().IntPtr ();
						p.AddParameter ().Type ().Type (jniHandleOwnershipRef, true);
						p.AddParameter ().Type ().Type (systemTypeRef, false);
					})),
			JniEnvCopyArrayRef = pe.AddMemberRef (jniEnvRef, "CopyArray",
				sig => sig.MethodSignature ().Parameters (3,
					rt => rt.Void (),
					p => {
						p.AddParameter ().Type ().Type (systemArrayRef, false);
						p.AddParameter ().Type ().Type (systemTypeRef, false);
						p.AddParameter ().Type ().IntPtr ();
					})),
			JniEnvNewArrayRef = pe.AddMemberRef (jniEnvRef, "NewArray",
				sig => sig.MethodSignature ().Parameters (2,
					rt => rt.Type ().IntPtr (),
					p => {
						p.AddParameter ().Type ().Type (systemArrayRef, false);
						p.AddParameter ().Type ().Type (systemTypeRef, false);
					})),
			JniEnvNewStringRef = pe.AddMemberRef (jniEnvRef, "NewString",
				sig => sig.MethodSignature ().Parameters (1,
					rt => rt.Type ().IntPtr (),
					p => p.AddParameter ().Type ().String ())),
			JniEnvToLocalJniHandleRef = pe.AddMemberRef (jniEnvRef, "ToLocalJniHandle",
				sig => sig.MethodSignature ().Parameters (1,
					rt => rt.Type ().IntPtr (),
					p => p.AddParameter ().Type ().Type (iJavaObjectRef, false))),
			JavaLangObjectGetObjectRef = pe.AddMemberRef (javaLangObjectRef, "GetObject",
				sig => sig.MethodSignature ().Parameters (3,
					rt => rt.Type ().Type (iJavaPeerableRef, false),
					p => {
						p.AddParameter ().Type ().IntPtr ();
						p.AddParameter ().Type ().Type (jniHandleOwnershipRef, true);
						p.AddParameter ().Type ().Type (systemTypeRef, false);
					})),
			InputStreamInvokerFromJniHandleRef = pe.AddMemberRef (inputStreamInvokerRef, "FromJniHandle",
				sig => sig.MethodSignature ().Parameters (2,
					rt => rt.Type ().Type (systemStreamRef, false),
					p => {
						p.AddParameter ().Type ().IntPtr ();
						p.AddParameter ().Type ().Type (jniHandleOwnershipRef, true);
					})),
			OutputStreamInvokerFromJniHandleRef = pe.AddMemberRef (outputStreamInvokerRef, "FromJniHandle",
				sig => sig.MethodSignature ().Parameters (2,
					rt => rt.Type ().Type (systemStreamRef, false),
					p => {
						p.AddParameter ().Type ().IntPtr ();
						p.AddParameter ().Type ().Type (jniHandleOwnershipRef, true);
					})),
			InputStreamAdapterToLocalJniHandleRef = pe.AddMemberRef (inputStreamAdapterRef, "ToLocalJniHandle",
				sig => sig.MethodSignature ().Parameters (1,
					rt => rt.Type ().IntPtr (),
					p => p.AddParameter ().Type ().Type (systemStreamRef, false))),
			OutputStreamAdapterToLocalJniHandleRef = pe.AddMemberRef (outputStreamAdapterRef, "ToLocalJniHandle",
				sig => sig.MethodSignature ().Parameters (1,
					rt => rt.Type ().IntPtr (),
					p => p.AddParameter ().Type ().Type (systemStreamRef, false))),
			XmlPullParserReaderFromJniHandleRef = pe.AddMemberRef (xmlPullParserReaderRef, "FromJniHandle",
				sig => sig.MethodSignature ().Parameters (2,
					rt => rt.Type ().Type (systemXmlReaderRef, false),
					p => {
						p.AddParameter ().Type ().IntPtr ();
						p.AddParameter ().Type ().Type (jniHandleOwnershipRef, true);
					})),
			XmlResourceParserReaderFromJniHandleRef = pe.AddMemberRef (xmlResourceParserReaderRef, "FromJniHandle",
				sig => sig.MethodSignature ().Parameters (2,
					rt => rt.Type ().Type (systemXmlReaderRef, false),
					p => {
						p.AddParameter ().Type ().IntPtr ();
						p.AddParameter ().Type ().Type (jniHandleOwnershipRef, true);
					})),
			XmlReaderPullParserToLocalJniHandleRef = pe.AddMemberRef (xmlReaderPullParserRef, "ToLocalJniHandle",
				sig => sig.MethodSignature ().Parameters (1,
					rt => rt.Type ().IntPtr (),
					p => p.AddParameter ().Type ().Type (systemXmlReaderRef, false))),
			XmlReaderResourceParserToLocalJniHandleRef = pe.AddMemberRef (xmlReaderResourceParserRef, "ToLocalJniHandle",
				sig => sig.MethodSignature ().Parameters (1,
					rt => rt.Type ().IntPtr (),
					p => p.AddParameter ().Type ().Type (systemXmlReaderRef, false))),
			UcoAttrCtorRef = ucoAttrCtorRef,
			UcoAttrBlobHandle = ucoAttrBlobHandle,
		};
	}

	public required TypeReferenceHandle IJavaObjectRef { get; init; }
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
	public required MemberReferenceHandle UcoAttrCtorRef { get; init; }

	public required BlobHandle UcoAttrBlobHandle { get; init; }
}
