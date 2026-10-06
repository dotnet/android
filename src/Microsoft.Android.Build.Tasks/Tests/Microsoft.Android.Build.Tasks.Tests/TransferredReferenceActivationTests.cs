using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using Microsoft.Android.Sdk.TrimmableTypeMap;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class TransferredReferenceActivationTests
{
	[TestCase (false, false)]
	[TestCase (true, false)]
	[TestCase (false, true)]
	public void JavaInteropActivationCleanupMatchesConstructorAvailability (bool invoker, bool inherited)
	{
		var target = new TypeRefData { AssemblyName = "Binding", ManagedTypeName = "Binding.Peer" };
		var model = new TypeMapAssemblyData { AssemblyName = "_Binding.TypeMap", ModuleName = "_Binding.TypeMap.dll" };
		model.ProxyTypes.Add (new JavaPeerProxyData {
			TypeName = "PeerProxy",
			JniName = "binding/Peer",
			TargetType = target,
			InvokerType = invoker ? target : null,
			InvokerActivationCtorStyle = invoker ? ActivationCtorStyle.JavaInterop : null,
			ActivationCtor = invoker ? null : new ActivationCtorData {
				DeclaringType = target,
				IsOnLeafType = !inherited,
				Style = ActivationCtorStyle.JavaInterop,
			},
		});
		using var stream = new MemoryStream ();
		new TypeMapAssemblyEmitter (new Version (11, 0, 0, 0)).Emit (model, stream);
		stream.Position = 0;
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var method = reader.MethodDefinitions.Select (reader.GetMethodDefinition)
			.Single (method => reader.GetString (method.Name) == "CreateInstance");
		var body = pe.GetMethodBody (method.RelativeVirtualAddress);
		var il = body.GetILBytes () ?? throw new InvalidOperationException ("Missing IL.");
		if (inherited) {
			// Inherited activation is intentionally unsupported, matching GetConstructor().
			// Do not enable the dormant inherited-ctor emitter as part of a cleanup fix.
			Assert.IsEmpty (body.ExceptionRegions);
			CollectionAssert.AreEqual (new [] { (byte) ILOpCode.Ldnull, (byte) ILOpCode.Ret }, il);
			return;
		}

		Assert.AreEqual (1, body.ExceptionRegions.Length, "Transferred input must be released even if activation throws.");
		var region = body.ExceptionRegions [0];
		Assert.AreEqual (ExceptionRegionKind.Finally, region.Kind);
		Assert.AreEqual ((byte) ILOpCode.Endfinally, il [region.HandlerOffset + region.HandlerLength - 1]);
		Assert.AreEqual ((byte) ILOpCode.Ret, il [il.Length - 1]);

		// The handler must load the original arguments, not the peer's copied reference.
		Assert.AreEqual ((byte) ILOpCode.Ldarg_1, il [region.HandlerOffset]);
		Assert.AreEqual ((byte) ILOpCode.Ldarg_2, il [region.HandlerOffset + 1]);
		Assert.AreEqual ((byte) ILOpCode.Call, il [region.HandlerOffset + 2]);
		var deleteRef = reader.GetMemberReference (
			System.Reflection.Metadata.Ecma335.MetadataTokens.MemberReferenceHandle (
				BitConverter.ToInt32 (il, region.HandlerOffset + 3) & 0x00ffffff));
		Assert.AreEqual ("DeleteRef", reader.GetString (deleteRef.Name));
		Assert.AreEqual (region.TryOffset + region.TryLength, region.HandlerOffset);
		Assert.AreEqual (region.HandlerOffset + region.HandlerLength, il.Length - 2, "Return must be outside the EH region.");
	}
}
