using System;
using System.IO;
using System.Linq;

using Microsoft.Android.Sdk.TrimmableTypeMap;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable (ParallelScope.Children)]
public class GeneratedJniDebuggerBoundaryTests
{
	const string DebuggerAttribute = "System.Diagnostics.DebuggerDisableUserUnhandledExceptionsAttribute";
	const string UcoAttribute = "System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute";

	[TestCase (true, false, false, false, false)]
	[TestCase (false, true, false, false, false)]
	[TestCase (false, true, true, false, false)]
	[TestCase (false, false, false, false, false)]
	[TestCase (false, false, false, true, false)]
	[TestCase (false, false, true, false, false)]
	[TestCase (false, false, true, true, false)]
	[TestCase (false, false, false, false, true)]
	public void ConstructorVariantsAnnotateTheInlineBoundary (bool matchingManagedCtor, bool invoker,
		bool javaInterop, bool inherited, bool openGeneric)
	{
		var target = PeerType (openGeneric ? "Example.Peer`1" : "Example.Peer");
		var proxy = CreateProxy (target, javaInterop, inherited, openGeneric);
		if (invoker) {
			proxy.InvokerType = PeerType ("Example.PeerInvoker");
			proxy.InvokerActivationCtorStyle = javaInterop ? ActivationCtorStyle.JavaInterop : ActivationCtorStyle.XamarinAndroid;
		}
		proxy.UcoConstructors.Add (new UcoConstructorData {
			WrapperName = "nctor_0_uco",
			TargetType = target,
			JniSignature = "()V",
			HasMatchingManagedCtor = matchingManagedCtor,
		});

		using var stream = Emit (proxy);
		using var assembly = AssemblyDefinition.ReadAssembly (stream);
		var method = GetProxy (assembly).Methods.Single (m => m.Name == "nctor_0_uco");
		AssertBoundary (method);
		Assert.AreEqual ("System.Void", method.ReturnType.FullName);
		CollectionAssert.AreEqual (new [] { "System.IntPtr", "System.IntPtr" },
			method.Parameters.Select (p => p.ParameterType.FullName));

		var handler = method.Body.ExceptionHandlers.Single (h => h.HandlerType == ExceptionHandlerType.Catch);
		var activation = method.Body.Instructions.Where (i => i.Offset >= handler.TryStart.Offset && i.Offset < handler.TryEnd.Offset).ToArray ();
		Assert.IsTrue (activation.Any (i => i.Operand is MethodReference m && m.Name == "ShouldSkipActivation"));
		if (openGeneric) {
			Assert.IsTrue (activation.Any (i => i.OpCode == OpCodes.Throw));
			Assert.IsTrue (activation.Any (i => i.Operand is MethodReference m && m.DeclaringType.FullName == "System.NotSupportedException"));
		} else {
			string declaringType = invoker ? "Example.PeerInvoker" : inherited ? "Example.BasePeer" : "Example.Peer";
			var ctor = activation.Single (i => i.Operand is MethodReference m && m.Name == ".ctor" && m.DeclaringType.FullName == declaringType);
			var ctorReference = (MethodReference) ctor.Operand;
			Assert.AreEqual (matchingManagedCtor || inherited ? OpCodes.Call : OpCodes.Newobj, ctor.OpCode);
			string [] expectedParameters = matchingManagedCtor ? [] : javaInterop
				? new [] { "Java.Interop.JniObjectReference&", "Java.Interop.JniObjectReferenceOptions" }
				: new [] { "System.IntPtr", "Android.Runtime.JniHandleOwnership" };
			CollectionAssert.AreEqual (expectedParameters, ctorReference.Parameters.Select (p => p.ParameterType.FullName));
		}
		Assert.IsFalse (GetProxy (assembly).Methods.Single (m => m.Name == "CreateInstance")
			.CustomAttributes.Any (a => a.AttributeType.FullName == DebuggerAttribute),
			"The managed activation entry point is not a JNI user-unhandled boundary.");
	}

	[Test]
	public void MatchingConstructorKeepsMarshallingInsideTheBoundary ()
	{
		var target = PeerType ("Example.Peer");
		var proxy = CreateProxy (target);
		proxy.UcoConstructors.Add (new UcoConstructorData {
			WrapperName = "nctor_0_uco",
			TargetType = target,
			JniSignature = "(ZLjava/lang/String;[I)V",
			HasMatchingManagedCtor = true,
			ManagedParameterTypes = [SystemType ("Boolean"), SystemType ("String"), SystemType ("Int32[]")],
		});
		using var stream = Emit (proxy);
		using var assembly = AssemblyDefinition.ReadAssembly (stream);
		var method = GetProxy (assembly).Methods.Single (m => m.Name == "nctor_0_uco");
		AssertBoundary (method);
		CollectionAssert.AreEqual (new [] { "System.IntPtr", "System.IntPtr", "System.Byte", "System.IntPtr", "System.IntPtr" },
			method.Parameters.Select (p => p.ParameterType.FullName));
		AssertCallInsideTry (method, "GetActivationPeer");
		AssertCallInsideTry (method, "SetActivationPeerReference");
		AssertCallInsideTry (method, "GetString");
		AssertCallInsideTry (method, "GetArray");
		AssertCallInsideTry (method, "MarkActivationPeerReplaceable");
	}

	[TestCase ("V", "Void", "System.Void")]
	[TestCase ("Z", "Boolean", "System.Byte")]
	[TestCase ("B", "SByte", "System.SByte")]
	[TestCase ("C", "Char", "System.UInt16")]
	[TestCase ("S", "Int16", "System.Int16")]
	[TestCase ("I", "Int32", "System.Int32")]
	[TestCase ("J", "Int64", "System.Int64")]
	[TestCase ("F", "Single", "System.Single")]
	[TestCase ("D", "Double", "System.Double")]
	[TestCase ("Ljava/lang/String;", "String", "System.IntPtr")]
	public void ExportBoundariesPreserveAbiDefaultsAndDispatch (string jniReturn, string managedReturn, string abiReturn)
	{
		var target = PeerType ("Example.Peer");
		var proxy = CreateProxy (target);
		foreach (bool isStatic in new [] { false, true }) {
			proxy.UcoMethods.Add (new UcoMethodData {
				WrapperName = isStatic ? "static_uco" : "instance_uco",
				CallbackMethodName = "n_dispatch",
				CallbackType = target,
				JniSignature = $"(ZLjava/lang/String;[I){jniReturn}",
				ExportMethodDispatch = new ExportMethodDispatchData {
					ManagedMethodName = isStatic ? "StaticDispatch" : "InstanceDispatch",
					IsStatic = isStatic,
					ParameterTypes = [SystemType ("Boolean"), SystemType ("String"), SystemType ("Int32[]")],
					ReturnType = SystemType (managedReturn),
				},
			});
		}
		using var stream = Emit (proxy);
		using var assembly = AssemblyDefinition.ReadAssembly (stream);
		foreach (var method in GetProxy (assembly).Methods.Where (m => m.Name.EndsWith ("_uco", StringComparison.Ordinal))) {
			AssertBoundary (method);
			Assert.AreEqual (abiReturn, method.ReturnType.FullName);
			CollectionAssert.AreEqual (new [] { "System.IntPtr", "System.IntPtr", "System.Byte", "System.IntPtr", "System.IntPtr" },
				method.Parameters.Select (p => p.ParameterType.FullName));
			AssertCallInsideTry (method, "GetString");
			AssertCallInsideTry (method, "GetArray");
			var copyBack = AssertCallInsideTry (method, "CopyArray");
			bool isStatic = method.Name == "static_uco";
			var dispatch = AssertCallInsideTry (method, isStatic ? "StaticDispatch" : "InstanceDispatch");
			Assert.AreEqual (isStatic ? OpCodes.Call : OpCodes.Callvirt, dispatch.OpCode);
			Assert.Less (dispatch.Offset, copyBack.Offset);
			Assert.AreEqual (!isStatic, method.Body.Instructions.Any (i => i.Operand is MethodReference m && m.Name == "GetObject"));
			if (managedReturn == "String") {
				Assert.Less (copyBack.Offset, AssertCallInsideTry (method, "NewString").Offset);
			}

			var instructions = method.Body.Instructions;
			var begin = instructions.Single (i => i.Operand is MethodReference m && m.Name == "BeginMarshalMethod");
			var afterAll = (Instruction) begin.Next.Operand;
			if (managedReturn == "Void") {
				Assert.AreEqual (OpCodes.Ret, afterAll.OpCode);
			} else {
				Assert.AreEqual (OpCodes.Ldloc_S, afterAll.OpCode);
				var abiLocal = (VariableDefinition) afterAll.Operand;
				Assert.AreEqual (abiReturn, abiLocal.VariableType.FullName);
				Assert.AreEqual (OpCodes.Ret, afterAll.Next.OpCode);
				var store = instructions.Single (i => i.OpCode == OpCodes.Stloc_S && i.Operand == abiLocal);
				var catchHandler = method.Body.ExceptionHandlers.Single (h => h.HandlerType == ExceptionHandlerType.Catch);
				Assert.Less (store.Offset, catchHandler.TryEnd.Offset,
					"Only successful dispatch assigns the zero-initialized ABI return local.");
			}
		}
		Assert.AreEqual (1, assembly.MainModule.GetTypeReferences ().Count (t => t.FullName == DebuggerAttribute),
			"All generated boundaries should share one attribute type reference.");
	}

	static void AssertBoundary (MethodDefinition method)
	{
		var attribute = method.CustomAttributes.Single (a => a.AttributeType.FullName == DebuggerAttribute);
		Assert.AreEqual ("System.Runtime", attribute.AttributeType.Scope.Name);
		Assert.AreEqual (new Version (11, 0, 0, 0), ((AssemblyNameReference) attribute.AttributeType.Scope).Version);
		Assert.AreEqual (".ctor", attribute.Constructor.Name);
		Assert.IsTrue (attribute.Constructor.HasThis);
		Assert.AreEqual ("System.Void", attribute.Constructor.ReturnType.FullName);
		Assert.IsEmpty (attribute.Constructor.Parameters);
		Assert.IsEmpty (attribute.ConstructorArguments);
		Assert.IsEmpty (attribute.Properties);
		Assert.IsEmpty (attribute.Fields);
		CollectionAssert.AreEqual (new byte [] { 1, 0, 0, 0 }, attribute.GetBlob ());
		Assert.AreEqual (1, method.CustomAttributes.Count (a => a.AttributeType.FullName == UcoAttribute));
		Assert.IsTrue (method.IsPublic && method.IsStatic);
		Assert.IsTrue (method.Body.InitLocals, "Early exit and handled exceptions retain default ABI returns.");
		Assert.AreEqual (2, method.Body.ExceptionHandlers.Count);
		var catchHandler = method.Body.ExceptionHandlers.Single (h => h.HandlerType == ExceptionHandlerType.Catch);
		var finallyHandler = method.Body.ExceptionHandlers.Single (h => h.HandlerType == ExceptionHandlerType.Finally);
		Assert.AreEqual ("System.Exception", catchHandler.CatchType.FullName);
		Assert.AreEqual ("System.Runtime", catchHandler.CatchType.Scope.Name);
		Assert.AreSame (catchHandler.TryStart, finallyHandler.TryStart);
		Assert.AreSame (catchHandler.TryEnd, catchHandler.HandlerStart);
		Assert.AreSame (catchHandler.HandlerEnd, finallyHandler.HandlerStart);
		Assert.AreSame (finallyHandler.TryEnd, finallyHandler.HandlerStart);
		var instructions = method.Body.Instructions;
		var begin = instructions.Single (i => i.Operand is MethodReference m && m.Name == "BeginMarshalMethod");
		var policy = instructions.Single (i => i.Operand is MethodReference m && m.Name == "OnUserUnhandledException");
		var end = instructions.Single (i => i.Operand is MethodReference m && m.Name == "EndMarshalMethod");
		Assert.AreEqual ("Java.Interop.JniEnvironment", ((MethodReference) begin.Operand).DeclaringType.FullName);
		Assert.AreEqual ("Java.Interop.JniRuntime", ((MethodReference) policy.Operand).DeclaringType.FullName);
		Assert.AreEqual (OpCodes.Call, begin.OpCode);
		Assert.AreEqual (OpCodes.Brfalse, begin.Next.OpCode);
		Assert.AreSame (finallyHandler.HandlerEnd, begin.Next.Operand);
		Assert.Less (begin.Offset, catchHandler.TryStart.Offset);
		Assert.AreEqual (OpCodes.Callvirt, policy.OpCode);
		Assert.GreaterOrEqual (policy.Offset, catchHandler.HandlerStart.Offset);
		Assert.Less (policy.Offset, catchHandler.HandlerEnd.Offset);
		Assert.AreEqual (OpCodes.Brfalse, catchHandler.HandlerStart.Next.Next.OpCode, "A null runtime must skip policy delivery.");
		Assert.AreEqual (OpCodes.Call, end.OpCode);
		Assert.GreaterOrEqual (end.Offset, finallyHandler.HandlerStart.Offset);
		Assert.AreEqual (OpCodes.Endfinally, end.Next.OpCode);
		Assert.IsFalse (instructions.Any (i => i.Operand is MethodReference m &&
			(m.Name == "UnhandledException" || m.Name == "WaitForBridgeProcessing" || m.Name == "Break" || m.Name == "BreakForUserUnhandledException")),
			"The inline boundary delegates policy and notification exactly once rather than duplicating runtime behavior.");
	}

	static Instruction AssertCallInsideTry (MethodDefinition method, string name)
	{
		var instruction = method.Body.Instructions.Single (i => i.Operand is MethodReference m && m.Name == name);
		var handler = method.Body.ExceptionHandlers.Single (h => h.HandlerType == ExceptionHandlerType.Catch);
		Assert.GreaterOrEqual (instruction.Offset, handler.TryStart.Offset);
		Assert.Less (instruction.Offset, handler.TryEnd.Offset);
		return instruction;
	}

	static TypeRefData PeerType (string name) => new () { AssemblyName = "Example", ManagedTypeName = name };
	static TypeRefData SystemType (string name) => new () { AssemblyName = "System.Runtime", ManagedTypeName = $"System.{name}" };

	static JavaPeerProxyData CreateProxy (TypeRefData target, bool javaInterop = false, bool inherited = false, bool openGeneric = false)
		=> new () {
			TypeName = "PeerProxy",
			JniName = "example/Peer",
			TargetType = target,
			IsAcw = true,
			IsGenericDefinition = openGeneric,
			ActivationCtor = new ActivationCtorData {
				DeclaringType = inherited ? PeerType ("Example.BasePeer") : target,
				IsOnLeafType = !inherited,
				Style = javaInterop ? ActivationCtorStyle.JavaInterop : ActivationCtorStyle.XamarinAndroid,
			},
		};

	static MemoryStream Emit (JavaPeerProxyData proxy)
	{
		var model = new TypeMapAssemblyData { AssemblyName = "_Example.TypeMap", ModuleName = "_Example.TypeMap.dll" };
		model.ProxyTypes.Add (proxy);
		var stream = new MemoryStream ();
		new TypeMapAssemblyEmitter (new Version (11, 0, 0, 0)).Emit (model, stream);
		stream.Position = 0;
		return stream;
	}

	static TypeDefinition GetProxy (AssemblyDefinition assembly) => assembly.MainModule.Types.Single (t => t.Name == "PeerProxy");
}
