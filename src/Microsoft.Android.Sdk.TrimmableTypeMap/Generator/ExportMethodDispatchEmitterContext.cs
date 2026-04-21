using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Holds pre-resolved metadata references needed by <see cref="ExportMethodDispatchEmitter"/>
/// for generating [Export] method dispatch IL. Created once per emit pass and reused
/// for all export methods.
/// </summary>
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
