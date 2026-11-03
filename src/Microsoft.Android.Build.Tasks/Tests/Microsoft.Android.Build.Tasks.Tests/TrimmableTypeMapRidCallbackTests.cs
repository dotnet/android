using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Microsoft.Android.Sdk.TrimmableTypeMap;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class TrimmableTypeMapRidCallbackTests
{
	const string Arm64Path = "/packages/runtimes/android-arm64/lib/net11.0-android/RidSpecificCallbacks.dll";
	const string X64Path = "/packages/runtimes/android-x64/lib/net11.0-android/RidSpecificCallbacks.dll";

	[TestCase (1)]
	[TestCase (2)]
	public void SupportedCallbackFormatIsAccepted (int version)
	{
		using var assembly = CreateCallbackAssembly (includeCallback: false, callbackFormatVersion: version);
		using var pe = new PEReader (assembly);
		using var scanner = new JavaPeerScanner ();

		Assert.IsEmpty (scanner.Scan ([new AssemblyInput ("RidSpecificCallbacks", Arm64Path, pe)]));
	}

	[TestCase (-1)]
	[TestCase (0)]
	[TestCase (3)]
	public void UnsupportedCallbackFormatIsRejected (int version)
	{
		using var assembly = CreateCallbackAssembly (includeCallback: false, callbackFormatVersion: version);
		using var pe = new PEReader (assembly);
		using var scanner = new JavaPeerScanner ();

		var error = Assert.Throws<NotSupportedException> (() =>
			scanner.Scan ([new AssemblyInput ("RidSpecificCallbacks", Arm64Path, pe)]));
		StringAssert.Contains ($"unsupported Java peer callback format version '{version}'", error.Message);
	}

	[Test]
	public void UnsupportedCallbackFormatOnLaterRidIsRejected ()
	{
		using var arm64Assembly = CreateCallbackAssembly (includeCallback: true);
		using var x64Assembly = CreateCallbackAssembly (includeCallback: true, callbackFormatVersion: 3);
		using var arm64 = new PEReader (arm64Assembly);
		using var x64 = new PEReader (x64Assembly);
		using var scanner = new JavaPeerScanner ();

		var error = Assert.Throws<NotSupportedException> (() => scanner.Scan ([
			new AssemblyInput ("RidSpecificCallbacks", Arm64Path, arm64),
			new AssemblyInput ("RidSpecificCallbacks", X64Path, x64),
		]));
		StringAssert.Contains ("unsupported Java peer callback format version '3'", error.Message);
	}

	[Test]
	public void NonIntegerCallbackFormatIsRejected ()
	{
		using var assembly = CreateCallbackAssembly (includeCallback: false, invalidVersionType: true);
		using var pe = new PEReader (assembly);
		using var scanner = new JavaPeerScanner ();

		var error = Assert.Throws<NotSupportedException> (() =>
			scanner.Scan ([new AssemblyInput ("RidSpecificCallbacks", Arm64Path, pe)]));
		StringAssert.Contains ("without a supported integer version", error.Message);
	}

	[Test]
	public void EquivalentRidSpecificUcoCallbacksAreAccepted ()
	{
		using var arm64Assembly = CreateCallbackAssembly (includeCallback: true);
		using var x64Assembly = CreateCallbackAssembly (includeCallback: true);
		using var arm64 = new PEReader (arm64Assembly);
		using var x64 = new PEReader (x64Assembly);
		using var scanner = new JavaPeerScanner ();

		Assert.IsEmpty (scanner.Scan ([
			new AssemblyInput ("RidSpecificCallbacks", Arm64Path, arm64),
			new AssemblyInput ("RidSpecificCallbacks", X64Path, x64),
		]));
	}

	[Test]
	public void DifferentRidSpecificUcoCallbacksAreRejected ()
	{
		using var arm64Assembly = CreateCallbackAssembly (includeCallback: true);
		using var x64Assembly = CreateCallbackAssembly (includeCallback: false);
		using var arm64 = new PEReader (arm64Assembly);
		using var x64 = new PEReader (x64Assembly);
		using var scanner = new JavaPeerScanner ();

		var error = Assert.Throws<InvalidOperationException> (() => scanner.Scan ([
			new AssemblyInput ("RidSpecificCallbacks", Arm64Path, arm64),
			new AssemblyInput ("RidSpecificCallbacks", X64Path, x64),
		]));
		StringAssert.Contains ("different Java peer callback metadata", error.Message);
		StringAssert.Contains (Arm64Path, error.Message);
		StringAssert.Contains (X64Path, error.Message);
	}

	static MemoryStream CreateCallbackAssembly (bool includeCallback, int callbackFormatVersion = 2, bool invalidVersionType = false)
	{
		var stream = new MemoryStream ();
		var pe = new PEAssemblyBuilder (new Version (11, 0, 0, 0));
		pe.EmitPreamble ("RidSpecificCallbacks", "RidSpecificCallbacks.dll");

		var callbackFormatAttribute = pe.Metadata.AddTypeReference (
			pe.MonoAndroidRef,
			pe.Metadata.GetOrAddString ("Java.Interop"),
			pe.Metadata.GetOrAddString ("JavaPeerCallbackFormatAttribute"));
		var callbackFormatCtor = pe.AddMemberRef (callbackFormatAttribute, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => {
					if (invalidVersionType) {
						p.AddParameter ().Type ().String ();
					} else {
						p.AddParameter ().Type ().Int32 ();
					}
				}));
		pe.Metadata.AddCustomAttribute (
			EntityHandle.AssemblyDefinition,
			callbackFormatCtor,
			pe.BuildAttributeBlob (blob => {
				if (invalidVersionType) {
					blob.WriteSerializedString ("2");
				} else {
					blob.WriteInt32 (callbackFormatVersion);
				}
			}));

		var objectRef = pe.Metadata.AddTypeReference (
			pe.SystemRuntimeRef,
			pe.Metadata.GetOrAddString ("System"),
			pe.Metadata.GetOrAddString ("Object"));
		pe.Metadata.AddTypeDefinition (
			TypeAttributes.Public | TypeAttributes.Class,
			pe.Metadata.GetOrAddString ("Test"),
			pe.Metadata.GetOrAddString ("CallbackHost"),
			objectRef,
			MetadataTokens.FieldDefinitionHandle (pe.Metadata.GetRowCount (TableIndex.Field) + 1),
			MetadataTokens.MethodDefinitionHandle (pe.Metadata.GetRowCount (TableIndex.MethodDef) + 1));

		if (includeCallback) {
			var callback = pe.EmitBody (
				"n_Invoke",
				MethodAttributes.Private | MethodAttributes.Static,
				sig => sig.MethodSignature ().Parameters (2,
					rt => rt.Void (),
					p => {
						p.AddParameter ().Type ().IntPtr ();
						p.AddParameter ().Type ().IntPtr ();
					}),
				encoder => encoder.Return ());
			var unmanagedCallersOnlyAttribute = pe.Metadata.AddTypeReference (
				pe.SystemRuntimeRef,
				pe.Metadata.GetOrAddString ("System.Runtime.InteropServices"),
				pe.Metadata.GetOrAddString ("UnmanagedCallersOnlyAttribute"));
			var unmanagedCallersOnlyCtor = pe.AddMemberRef (unmanagedCallersOnlyAttribute, ".ctor",
				sig => sig.MethodSignature (isInstanceMethod: true).Parameters (0, rt => rt.Void (), _ => { }));
			pe.Metadata.AddCustomAttribute (
				callback,
				unmanagedCallersOnlyCtor,
				pe.BuildAttributeBlob (_ => { }));
		}

		pe.WritePE (stream);
		stream.Position = 0;
		return stream;
	}
}
