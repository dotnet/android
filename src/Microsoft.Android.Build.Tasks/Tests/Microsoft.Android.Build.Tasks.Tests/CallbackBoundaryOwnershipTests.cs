using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using MethodDefinitionHandle = System.Reflection.Metadata.MethodDefinitionHandle;
using Microsoft.Android.Sdk.TrimmableTypeMap;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class CallbackBoundaryOwnershipTests
{
	[TestCase ("raw", false)]
	[TestCase ("helper", true)]
	[TestCase ("typed", true)]
	[TestCase ("inline", true)]
	[TestCase ("inline-guard-nop", true)]
	[TestCase ("missing-attribute", false)]
	[TestCase ("fake-attribute", false)]
	[TestCase ("fake-helper", false)]
	[TestCase ("unsigned-helper", false)]
	[TestCase ("wrong-helper-signature", false)]
	[TestCase ("wrong-arguments", false)]
	[TestCase ("helper-before", false)]
	[TestCase ("helper-after", false)]
	[TestCase ("narrow-catch", false)]
	[TestCase ("missing-guard", false)]
	[TestCase ("wrong-environment", false)]
	[TestCase ("wrong-transition", false)]
	[TestCase ("mutated-runtime", false)]
	[TestCase ("mutated-transition", false)]
	[TestCase ("boundary-local-exposed", false)]
	[TestCase ("fake-transition-scope", false)]
	[TestCase ("conditional-escape", false)]
	[TestCase ("switch-escape", false)]
	[TestCase ("extra-finally", false)]
	[TestCase ("before-inline", false)]
	[TestCase ("after-inline", false)]
	[TestCase ("missing-finally", false)]
	[TestCase ("missing-notify", false)]
	[TestCase ("rethrow", false)]
	[TestCase ("skipped-notify", false)]
	[TestCase ("reference-stub", false)]
	public void RecognizesOnlyCompleteTrustedBoundaries (string shape, bool protectedCallback)
	{
		using var stream = CreateFixture (shape);
		using var pe = new PEReader (stream);
		var index = AssemblyIndex.Create (pe, "BoundaryFixture");
		var result = index.ClassifyCallback (FindCallback (index));
		Assert.AreEqual (protectedCallback, result.IsProtected, result.Reason);
		if (shape == "reference-stub")
			Assert.AreEqual (CallbackBoundaryKind.Unavailable, result.Kind);
	}

	[TestCase ("System.Int32", "I")]
	[TestCase ("System.IntPtr", "Ljava/lang/Object;")]
	[TestCase ("System.Boolean", "Z")]
	[TestCase ("System.Char", "C")]
	[TestCase ("System.Int64", "J")]
	[TestCase ("System.Single", "F")]
	[TestCase ("System.Double", "D")]
	public void InlineReturnDefaultsAreRecognized (string type, string jniReturn)
	{
		using var stream = CreateFixture ("inline", type);
		using var pe = new PEReader (stream);
		var index = AssemblyIndex.Create (pe, "BoundaryFixture");
		Assert.IsTrue (index.ClassifyCallback (FindCallback (index)).IsProtected);
	}

	[Test]
	public void UnmarkedUcoIsNeverManagedCallable ()
	{
		using var stream = CreateFixture ("uco");
		using var pe = new PEReader (stream);
		var index = AssemblyIndex.Create (pe, "BoundaryFixture");
		Assert.AreEqual (CallbackBoundaryKind.UnmanagedCallersOnly, index.ClassifyCallback (FindCallback (index)).Kind);
	}

	[TestCase ("helper", false)]
	[TestCase ("inline", false)]
	[TestCase ("raw", true)]
	[TestCase ("missing-attribute", true)]
	[TestCase ("reference-stub", true)]
	public void EmittedForwarderUsesClassifiedOwnership (string shape, bool safeInvoke)
	{
		using var fixture = CreateFixture (shape);
		using var pe = new PEReader (fixture);
		var index = AssemblyIndex.Create (pe, "BoundaryFixture");
		var peer = CreatePeer (index.ClassifyCallback (FindCallback (index)));
		var generator = new TypeMapAssemblyGenerator (new Version (11, 0));
		using var output = new MemoryStream ();
		generator.Generate ([peer], output, "_BoundaryFixture.TypeMap");
		output.Position = 0;
		using var emitted = AssemblyDefinition.ReadAssembly (output);
		var forwarder = emitted.MainModule.Types.SelectMany (t => t.Methods).Single (m => m.Name == "n_Invoke_uco_0");
		Assert.IsEmpty (forwarder.Body.ExceptionHandlers);
		Assert.AreEqual (safeInvoke, Calls (forwarder).Any (m => m.Name == "SafeInvokeAction"));
		Assert.AreEqual (!safeInvoke, Calls (forwarder).Any (m => m.Name == "n_Invoke"));
		Assert.AreEqual (safeInvoke, forwarder.Body.Instructions.Any (i => i.OpCode == OpCodes.Ldftn));
		Assert.IsFalse (Calls (forwarder).Any (m => m.Name is "BeginMarshalMethod" or "EndMarshalMethod" or "WaitForBridgeProcessing" or "UnhandledException"));
		Assert.IsFalse (emitted.MainModule.GetMemberReferences ().Any (m => m.Name == "GetInvokeHandler"), "Connectors are not invoked or rooted.");
	}

	[TestCase ("System.Boolean", "Z", "System.Byte")]
	[TestCase ("System.SByte", "Z", "System.Byte")]
	[TestCase ("System.Byte", "Z", "System.Byte")]
	[TestCase ("System.Char", "C", "System.UInt16")]
	[TestCase ("System.UInt16", "C", "System.UInt16")]
	[TestCase ("System.IntPtr", "Ljava/lang/Object;", "System.IntPtr")]
	[TestCase ("System.Double", "D", "System.Double")]
	public void SafeInvokeFunctionPointerUsesManagedSignature (string managedType, string jniType, string abiType)
	{
		var peer = CreatePeer (CallbackBoundary.Unavailable) with {
			MarshalMethods = [CreateMethod (CallbackBoundary.Unavailable) with {
				JniSignature = $"({jniType}){jniType}",
				NativeCallbackParameterTypeNames = [managedType],
				NativeCallbackReturnTypeName = managedType,
			}],
		};
		using var stream = Emit (peer);
		using var assembly = AssemblyDefinition.ReadAssembly (stream);
		var forwarder = assembly.MainModule.Types.SelectMany (t => t.Methods).Single (m => m.Name == "n_Invoke_uco_0");
		Assert.AreEqual (abiType, forwarder.ReturnType.FullName);
		Assert.AreEqual (abiType, forwarder.Parameters [2].ParameterType.FullName);
		var target = (MethodReference) forwarder.Body.Instructions.Single (i => i.OpCode == OpCodes.Ldftn).Operand;
		Assert.AreEqual (managedType, target.ReturnType.FullName);
		Assert.AreEqual (managedType, target.Parameters [2].ParameterType.FullName);
		var helper = Calls (forwarder).OfType<GenericInstanceMethod> ().Single (m => m.Name == "SafeInvokeFunc");
		CollectionAssert.AreEqual (new [] { managedType, managedType }, helper.GenericArguments.Select (a => a.FullName));
		var pointer = (FunctionPointerType) helper.ElementMethod.Parameters [^1].ParameterType;
		Assert.AreEqual (MethodCallingConvention.Default, pointer.CallingConvention);
		Assert.AreEqual (3, pointer.Parameters.Count);
		Assert.AreEqual ("!!0", pointer.Parameters [2].ParameterType.FullName);
		Assert.AreEqual ("!!1", pointer.ReturnType.FullName);
	}

	[TestCase (17, "System.Int32")]
	[TestCase (1, "System.Void*")]
	public void HelperIneligibleSignaturesKeepFullPolicy (int count, string returnType)
	{
		var peer = CreatePeer (CallbackBoundary.Unavailable) with {
			MarshalMethods = [CreateMethod (CallbackBoundary.Unavailable) with {
				JniSignature = $"({new string ('I', count)}){(returnType == "System.Int32" ? "I" : "Ljava/lang/Object;")}",
				NativeCallbackParameterTypeNames = Enumerable.Repeat ("System.Int32", count).ToArray (),
				NativeCallbackReturnTypeName = returnType,
			}],
		};
		using var stream = Emit (peer);
		using var assembly = AssemblyDefinition.ReadAssembly (stream);
		var forwarder = assembly.MainModule.Types.SelectMany (t => t.Methods).Single (m => m.Name == "n_Invoke_uco_0");
		CollectionAssert.AreEquivalent (new [] { ExceptionHandlerType.Catch, ExceptionHandlerType.Finally },
			forwarder.Body.ExceptionHandlers.Select (h => h.HandlerType));
		foreach (var name in new [] { "BeginMarshalMethod", "OnUserUnhandledException", "EndMarshalMethod", "n_Invoke" })
			Assert.AreEqual (1, Calls (forwarder).Count (m => m.Name == name));
		Assert.IsTrue (forwarder.CustomAttributes.Any (a => a.AttributeType.Name == "DebuggerDisableUserUnhandledExceptionsAttribute"));
		Assert.IsFalse (Calls (forwarder).Any (m => m.Name is "UnhandledException" or "WaitForBridgeProcessing"));
	}

	[Test]
	public void BodyOnlyOwnershipChangeInvalidatesModelFingerprint ()
	{
		var generator = new TypeMapAssemblyGenerator (new Version (11, 0));
		var raw = generator.CreateModel (ScanFixture ("raw"), "_BoundaryFixture.TypeMap");
		var inline = generator.CreateModel (ScanFixture ("inline"), "_BoundaryFixture.TypeMap");
		CollectionAssert.AreNotEqual (generator.ComputeFingerprints (raw, false, true).Content, generator.ComputeFingerprints (inline, false, true).Content);
	}

	[TestCase (true)]
	[TestCase (false)]
	public void ImplementationWinsOverReferenceRegardlessOfOrdering (bool referenceFirst)
	{
		using var reference = CreateFixture ("reference-stub");
		using var implementation = CreateFixture ("inline");
		using var refReader = new PEReader (reference);
		using var implReader = new PEReader (implementation);
		using var scanner = new JavaPeerScanner ();
		var inputs = new [] {
			new AssemblyInput ("BoundaryFixture", "/ref/BoundaryFixture.dll", refReader),
			new AssemblyInput ("BoundaryFixture", "/impl/BoundaryFixture.dll", implReader),
		};
		var peer = scanner.Scan (referenceFirst ? inputs : inputs.Reverse ()).Single ();
		Assert.IsNotNull (peer.MarshalMethods);
		var callback = peer.MarshalMethods.Single (m => m.NativeCallbackName == "n_Invoke");
		Assert.AreEqual (CallbackBoundaryKind.ProtectedInline, callback.NativeCallbackBoundary.Kind);
		Assert.IsEmpty (callback.NativeCallbackParameterTypeNames);
		Assert.AreEqual ("System.Void", callback.NativeCallbackReturnTypeName);
	}

	static List<JavaPeerInfo> ScanFixture (string shape)
	{
		using var fixture = CreateFixture (shape);
		using var pe = new PEReader (fixture);
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan ([new AssemblyInput ("BoundaryFixture", "/impl/BoundaryFixture.dll", pe)]);
		Assert.AreEqual (1, peers.Count);
		return peers;
	}

	[Test]
	public void RidSpecificLegacyOwnershipMismatchIsRejected ()
	{
		using var raw = CreateFixture ("raw");
		using var inline = CreateFixture ("inline");
		using var left = new PEReader (raw);
		using var right = new PEReader (inline);
		using var scanner = new JavaPeerScanner ();
		var exception = Assert.Throws<InvalidOperationException> (() => scanner.Scan ([
			new AssemblyInput ("BoundaryFixture", "/arm64/BoundaryFixture.dll", left),
			new AssemblyInput ("BoundaryFixture", "/x64/BoundaryFixture.dll", right),
		]));
		StringAssert.Contains ("different Java peer callback metadata", exception?.Message);
	}

	[Test]
	public void DifferentProtectedBodiesHaveEquivalentRidCodegenDecisions ()
	{
		using var helper = CreateFixture ("helper");
		using var inline = CreateFixture ("inline");
		using var left = new PEReader (helper);
		using var right = new PEReader (inline);
		CollectionAssert.AreEqual (AssemblyIndex.Create (left, "BoundaryFixture").GetCallbackMetadata (),
			AssemblyIndex.Create (right, "BoundaryFixture").GetCallbackMetadata ());
	}

	[Test]
	[Explicit ("Requires locally built Mono.Android and the optional pinned public-package corpus; never downloads packages.")]
	public void ProductionClassifierMatchesImplementationCorpus ()
	{
		string mono = Environment.GetEnvironmentVariable ("JNI_CALLBACK_MONO_ANDROID")
			?? throw new InvalidOperationException ("Set JNI_CALLBACK_MONO_ANDROID to the locally built implementation DLL.");
		string corpus = Environment.GetEnvironmentVariable ("JNI_CALLBACK_CORPUS")
			?? throw new InvalidOperationException ("Set JNI_CALLBACK_CORPUS to the extracted pinned public-package corpus.");
		var framework = ClassifyAssembly (mono);
		Assert.AreEqual (29967, framework.Count (b => b.Kind == CallbackBoundaryKind.ProtectedHelper));
		Assert.AreEqual (9, framework.Count (b => b.Kind == CallbackBoundaryKind.ProtectedInline));
		Assert.AreEqual (29976, framework.Count);
		foreach (var (asset, count, kind) in new [] {
			("karamunting.android.chrisbanes.photoview.2.3.0/lib/monoandroid90", 78, CallbackBoundaryKind.Raw),
			("square.okhttp3.4.12.0.1/lib/monoandroid12.0", 120, CallbackBoundaryKind.Raw),
			("square.okhttp3.4.12.0.1/lib/net6.0-android31.0", 120, CallbackBoundaryKind.Raw),
			("square.okhttp3.4.12.0.1/lib/net7.0-android33.0", 120, CallbackBoundaryKind.Raw),
			("square.okhttp3.android.5.5.0/lib/net9.0-android35.0", 174, CallbackBoundaryKind.Raw),
			("square.okhttp3.android.5.5.0/lib/net10.0-android36.0", 174, CallbackBoundaryKind.ProtectedInline),
			("xamarin.androidx.appcompat.1.6.1.5/lib/monoandroid12.0", 1600, CallbackBoundaryKind.Raw),
			("xamarin.androidx.appcompat.1.6.1.5/lib/net6.0-android31.0", 1600, CallbackBoundaryKind.Raw),
			("xamarin.androidx.appcompat.1.7.1.4/lib/net9.0-android35.0", 1638, CallbackBoundaryKind.Raw),
			("xamarin.androidx.appcompat.1.7.1.4/lib/net10.0-android36.0", 1638, CallbackBoundaryKind.ProtectedInline),
		}) {
			string path = Directory.GetFiles (Path.Combine (corpus, asset), "*.dll").Single ();
			var boundaries = ClassifyAssembly (path);
			TestContext.Out.WriteLine ($"{asset}: {boundaries.Count} callbacks, {boundaries.Count (b => b.Kind == kind)} {kind}");
			Assert.AreEqual (count, boundaries.Count, asset);
			Assert.IsTrue (boundaries.All (b => b.Kind == kind), asset);
		}
	}

	static List<CallbackBoundary> ClassifyAssembly (string path)
	{
		using var stream = File.OpenRead (path);
		using var pe = new PEReader (stream);
		var index = AssemblyIndex.Create (pe, Path.GetFileNameWithoutExtension (path));
		var results = new List<CallbackBoundary> ();
		foreach (var handle in index.Reader.MethodDefinitions) {
			var method = index.Reader.GetMethodDefinition (handle);
			if (!index.Reader.GetString (method.Name).StartsWith ("n_", StringComparison.Ordinal) ||
			    (method.Attributes & System.Reflection.MethodAttributes.Static) == 0)
				continue;
			var signature = method.DecodeSignature (SignatureTypeProvider.Instance, null);
			if (signature.ParameterTypes.Length < 2 || signature.ParameterTypes [0] != "System.IntPtr" || signature.ParameterTypes [1] != "System.IntPtr")
				continue;
			var result = index.ClassifyCallback (handle);
			results.Add (result);
			if (!result.IsProtected && result.Kind != CallbackBoundaryKind.Raw)
				TestContext.Out.WriteLine ($"{index.Reader.GetString (method.Name)}: {result.Kind}: {result.Reason}");
		}
		return results;
	}

	static IEnumerable<MethodReference> Calls (MethodDefinition method)
		=> method.Body.Instructions.Where (i => i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt).Select (i => (MethodReference) i.Operand);

	static MethodDefinitionHandle FindCallback (AssemblyIndex index)
		=> index.Reader.MethodDefinitions.Single (h => index.Reader.GetString (index.Reader.GetMethodDefinition (h).Name) == "n_Invoke");

	static MemoryStream Emit (JavaPeerInfo peer)
	{
		var output = new MemoryStream ();
		new TypeMapAssemblyGenerator (new Version (11, 0)).Generate ([peer], output, "_BoundaryFixture.TypeMap");
		output.Position = 0;
		return output;
	}

	static JavaPeerInfo CreatePeer (CallbackBoundary boundary) => new () {
		JavaName = "test/BoundaryFixture", CompatJniName = "test/BoundaryFixture",
		ManagedTypeName = "Test.BoundaryFixture", ManagedTypeNamespace = "Test", ManagedTypeShortName = "BoundaryFixture",
		AssemblyName = "BoundaryFixture", BaseJavaName = "java/lang/Object", MarshalMethods = [CreateMethod (boundary)],
		ActivationCtor = new ActivationCtorInfo {
			DeclaringTypeName = "Test.BoundaryFixture", DeclaringAssemblyName = "BoundaryFixture",
			DeclaringType = new TypeRefData { ManagedTypeName = "Test.BoundaryFixture", AssemblyName = "BoundaryFixture" },
			Style = ActivationCtorStyle.XamarinAndroid,
		},
	};
	static MarshalMethodInfo CreateMethod (CallbackBoundary boundary) => new () {
		JniName = "Invoke", JniSignature = "()V", NativeCallbackName = "n_Invoke", ManagedMethodName = "Invoke",
		Connector = "GetInvokeHandler", NativeCallbackParameterTypeNames = [], NativeCallbackReturnTypeName = "System.Void",
		NativeCallbackBoundary = boundary,
	};

	internal static MemoryStream CreateFixture (string shape, string returnType = "System.Void")
	{
		using var assembly = AssemblyDefinition.CreateAssembly (new AssemblyNameDefinition ("BoundaryFixture", new Version (1, 0)), "BoundaryFixture", ModuleKind.Dll);
		var module = assembly.MainModule;
		var runtime = new AssemblyNameReference ("System.Runtime", new Version (11, 0)) { PublicKeyToken = Convert.FromHexString ("B03F5F7F11D50A3A") };
		var interop = new AssemblyNameReference (shape == "fake-helper" ? "Not.Java.Interop" : "Java.Interop", new Version (1, 0)) {
			PublicKeyToken = shape == "unsigned-helper" ? [] : Convert.FromHexString ("84E04FF9CFB79065"),
		};
		module.AssemblyReferences.Add (runtime);
		module.AssemblyReferences.Add (interop);
		TypeReference Type (string name, IMetadataScope scope, bool valueType = false)
		{
			int dot = name.LastIndexOf ('.');
			return new TypeReference (name.Substring (0, dot), name.Substring (dot + 1), module, scope, valueType);
		}
		var owner = new TypeDefinition ("Test", "BoundaryFixture", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
		module.Types.Add (owner);
		TypeReference resultType = returnType switch {
			"System.Void" => module.TypeSystem.Void, "System.Int32" => module.TypeSystem.Int32, "System.Int64" => module.TypeSystem.Int64,
			"System.IntPtr" => module.TypeSystem.IntPtr, "System.Boolean" => module.TypeSystem.Boolean, "System.Char" => module.TypeSystem.Char,
			"System.Single" => module.TypeSystem.Single, "System.Double" => module.TypeSystem.Double,
			_ => throw new ArgumentException (returnType),
		};
		var target = new MethodDefinition ("Body", MethodAttributes.Private | MethodAttributes.Static, resultType);
		var callback = new MethodDefinition ("n_Invoke", MethodAttributes.Public | MethodAttributes.Static, resultType);
		foreach (var method in new [] { target, callback }) {
			method.Parameters.Add (new ParameterDefinition (module.TypeSystem.IntPtr));
			method.Parameters.Add (new ParameterDefinition (module.TypeSystem.IntPtr));
			owner.Methods.Add (method);
		}
		void Attribute (ICustomAttributeProvider provider, string name, IMetadataScope scope)
			=> provider.CustomAttributes.Add (new CustomAttribute (new MethodReference (".ctor", module.TypeSystem.Void, Type (name, scope)) { HasThis = true }));
		var android = new AssemblyNameReference ("Mono.Android", new Version (1, 0)) {
			PublicKeyToken = Convert.FromHexString ("84E04FF9CFB79065"),
		};
		module.AssemblyReferences.Add (android);
		void Register (ICustomAttributeProvider provider, string name, string signature, string connector)
		{
			var constructor = new MethodReference (".ctor", module.TypeSystem.Void, Type ("Android.Runtime.RegisterAttribute", android)) { HasThis = true };
			for (int i = 0; i < 3; i++)
				constructor.Parameters.Add (new ParameterDefinition (module.TypeSystem.String));
			var attribute = new CustomAttribute (constructor);
			foreach (var argument in new [] { name, signature, connector })
				attribute.ConstructorArguments.Add (new CustomAttributeArgument (module.TypeSystem.String, argument));
			provider.CustomAttributes.Add (attribute);
		}
		Register (owner, "test/BoundaryFixture", "", "");
		var managedMethod = new MethodDefinition ("Invoke", MethodAttributes.Public | MethodAttributes.Virtual, resultType);
		owner.Methods.Add (managedMethod);
		Register (managedMethod, "invoke", returnType == "System.Void" ? "()V" : "()I", "GetInvokeHandler");
		var activation = new MethodDefinition (".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
		activation.Parameters.Add (new ParameterDefinition (module.TypeSystem.IntPtr));
		activation.Parameters.Add (new ParameterDefinition (Type ("Android.Runtime.JniHandleOwnership", android, true)));
		activation.Body.GetILProcessor ().Emit (OpCodes.Ret);
		owner.Methods.Add (activation);
		var il = callback.Body.GetILProcessor ();
		void Default ()
		{
			switch (returnType) {
			case "System.Void": break;
			case "System.Single": il.Emit (OpCodes.Ldc_R4, 0f); break;
			case "System.Double": il.Emit (OpCodes.Ldc_R8, 0d); break;
			case "System.IntPtr": il.Emit (OpCodes.Ldc_I4_0); il.Emit (OpCodes.Conv_I); break;
			case "System.Int64": il.Emit (OpCodes.Ldc_I4_0); il.Emit (OpCodes.Conv_I8); break;
			default: il.Emit (OpCodes.Ldc_I4_0); break;
			}
		}
		if (shape == "reference-stub") {
			Attribute (assembly, "System.Runtime.CompilerServices.ReferenceAssemblyAttribute", runtime);
			il.Emit (OpCodes.Ldnull);
			il.Emit (OpCodes.Throw);
		} else if (shape == "raw" || shape == "uco") {
			Default ();
			il.Emit (OpCodes.Ret);
			if (shape == "uco")
				Attribute (callback, "System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute", runtime);
		} else if (shape.Contains ("helper", StringComparison.Ordinal) || shape is "wrong-arguments" or "typed") {
			if (shape == "helper-before")
				il.Emit (OpCodes.Call, target);
			bool typed = shape == "typed";
			var monoAndroid = new AssemblyNameReference ("Mono.Android", new Version (1, 0)) {
				PublicKeyToken = Convert.FromHexString ("84E04FF9CFB79065"),
			};
			module.AssemblyReferences.Add (monoAndroid);
			var helperOwner = typed ? Type ("Java.Interop.JniMarshalTyped", monoAndroid) : Type ("Java.Interop.JniMarshal", interop);
			var helper = new MethodReference (typed ? "Invoke_X" : "SafeInvokeAction", module.TypeSystem.Void, helperOwner);
			if (shape == "wrong-helper-signature")
				helper.Name = "SafeInvokeFunc";
			helper.Parameters.Add (new ParameterDefinition (module.TypeSystem.IntPtr));
			helper.Parameters.Add (new ParameterDefinition (module.TypeSystem.IntPtr));
			var pointer = new FunctionPointerType { ReturnType = module.TypeSystem.Void };
			if (typed) {
				var generic = new GenericParameter (helper);
				helper.GenericParameters.Add (generic);
				pointer.Parameters.Add (new ParameterDefinition (generic));
				target.Parameters.Clear ();
				target.Parameters.Add (new ParameterDefinition (module.TypeSystem.Object));
			} else {
				pointer.Parameters.Add (new ParameterDefinition (module.TypeSystem.IntPtr));
				pointer.Parameters.Add (new ParameterDefinition (module.TypeSystem.IntPtr));
			}
			helper.Parameters.Add (new ParameterDefinition (pointer));
			MethodReference helperCall = helper;
			if (typed) {
				var instance = new GenericInstanceMethod (helper);
				instance.GenericArguments.Add (module.TypeSystem.Object);
				helperCall = instance;
			}
			il.Emit (shape == "wrong-arguments" ? OpCodes.Ldarg_1 : OpCodes.Ldarg_0);
			il.Emit (shape == "wrong-arguments" ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1);
			il.Emit (OpCodes.Ldftn, target);
			il.Emit (OpCodes.Call, helperCall);
			if (shape == "helper-after")
				il.Emit (OpCodes.Call, target);
			il.Emit (OpCodes.Ret);
		} else {
			var transition = Type ("Java.Interop.JniTransition", shape == "fake-transition-scope" ? runtime : interop, true);
			var jniRuntime = Type ("Java.Interop.JniRuntime", interop);
			var environment = Type ("Java.Interop.JniEnvironment", interop);
			var exception = Type ("System.Exception", runtime);
			var begin = new MethodReference ("BeginMarshalMethod", module.TypeSystem.Boolean, environment);
			begin.Parameters.Add (new ParameterDefinition (module.TypeSystem.IntPtr));
			begin.Parameters.Add (new ParameterDefinition (new ByReferenceType (transition)));
			begin.Parameters.Add (new ParameterDefinition (new ByReferenceType (jniRuntime)));
			var end = new MethodReference ("EndMarshalMethod", module.TypeSystem.Void, environment);
			end.Parameters.Add (new ParameterDefinition (new ByReferenceType (transition)));
			var notify = new MethodReference ("OnUserUnhandledException", module.TypeSystem.Void, jniRuntime) { HasThis = true };
			notify.Parameters.Add (new ParameterDefinition (new ByReferenceType (transition)));
			notify.Parameters.Add (new ParameterDefinition (exception));
			callback.Body.InitLocals = true;
			callback.Body.Variables.Add (new VariableDefinition (transition));
			callback.Body.Variables.Add (new VariableDefinition (jniRuntime));
			callback.Body.Variables.Add (new VariableDefinition (exception));
			callback.Body.Variables.Add (new VariableDefinition (resultType == module.TypeSystem.Void ? module.TypeSystem.Int32 : resultType));
			callback.Body.Variables.Add (new VariableDefinition (transition));
			var start = Instruction.Create (OpCodes.Ldarg_0);
			var guardNop = Instruction.Create (OpCodes.Nop);
			var catchStart = Instruction.Create (OpCodes.Stloc_2);
			var finallyStart = Instruction.Create (OpCodes.Ldloca_S, callback.Body.Variables [shape == "wrong-transition" ? 4 : 0]);
			var after = Instruction.Create (resultType == module.TypeSystem.Void ? OpCodes.Ret : OpCodes.Ldloc_3);
			if (shape == "before-inline")
				il.Emit (OpCodes.Call, target);
			il.Emit (shape == "wrong-environment" ? OpCodes.Ldarg_1 : OpCodes.Ldarg_0);
			il.Emit (OpCodes.Ldloca_S, callback.Body.Variables [0]);
			il.Emit (OpCodes.Ldloca_S, callback.Body.Variables [1]);
			il.Emit (OpCodes.Call, begin);
			if (shape == "missing-guard")
				il.Emit (OpCodes.Pop);
			else
				il.Emit (OpCodes.Brtrue, shape == "inline-guard-nop" ? guardNop : start);
			Default ();
			il.Emit (OpCodes.Ret);
			if (shape == "inline-guard-nop")
				il.Append (guardNop);
			il.Append (start);
			il.Emit (OpCodes.Ldarg_1);
			if (shape == "conditional-escape") {
				il.Emit (OpCodes.Ldc_I4_0);
				il.Emit (OpCodes.Ldc_I4_0);
				il.Emit (OpCodes.Beq, after);
			}
			if (shape == "switch-escape") {
				il.Emit (OpCodes.Ldc_I4_0);
				il.Emit (OpCodes.Switch, new [] { after });
			}
			if (shape == "mutated-runtime") {
				il.Emit (OpCodes.Ldnull);
				il.Emit (OpCodes.Stloc_1);
			}
			if (shape == "mutated-transition" || shape == "boundary-local-exposed") {
				il.Emit (OpCodes.Ldloca_S, callback.Body.Variables [0]);
				if (shape == "mutated-transition")
					il.Emit (OpCodes.Initobj, transition);
				else
					il.Emit (OpCodes.Pop);
			}
			il.Emit (OpCodes.Call, target);
			if (returnType != "System.Void")
				il.Emit (OpCodes.Stloc_3);
			il.Emit (OpCodes.Leave, after);
			il.Append (catchStart);
			if (shape == "skipped-notify")
				il.Emit (OpCodes.Br, finallyStart);
			il.Emit (OpCodes.Ldloc_1);
			il.Emit (OpCodes.Ldloca_S, callback.Body.Variables [0]);
			il.Emit (OpCodes.Ldloc_2);
			if (shape == "missing-notify") {
				il.Emit (OpCodes.Pop);
				il.Emit (OpCodes.Pop);
				il.Emit (OpCodes.Pop);
			} else {
				il.Emit (OpCodes.Callvirt, notify);
			}
			if (shape == "rethrow")
				il.Emit (OpCodes.Rethrow);
			if (returnType != "System.Void") {
				Default ();
				il.Emit (OpCodes.Stloc_3);
			}
			il.Emit (OpCodes.Leave, after);
			il.Append (finallyStart);
			il.Emit (OpCodes.Call, end);
			if (shape == "extra-finally")
				il.Emit (OpCodes.Call, target);
			il.Emit (OpCodes.Endfinally);
			il.Append (after);
			if (shape == "after-inline")
				il.Emit (OpCodes.Call, target);
			if (returnType != "System.Void")
				il.Emit (OpCodes.Ret);
			callback.Body.ExceptionHandlers.Add (new ExceptionHandler (ExceptionHandlerType.Catch) {
				CatchType = shape == "narrow-catch" ? Type ("System.InvalidOperationException", runtime) : exception,
				TryStart = start, TryEnd = catchStart, HandlerStart = catchStart, HandlerEnd = finallyStart,
			});
			if (shape != "missing-finally")
				callback.Body.ExceptionHandlers.Add (new ExceptionHandler (ExceptionHandlerType.Finally) {
					TryStart = start, TryEnd = finallyStart, HandlerStart = finallyStart, HandlerEnd = after,
				});
			if (shape != "missing-attribute")
				Attribute (callback, "System.Diagnostics.DebuggerDisableUserUnhandledExceptionsAttribute",
					shape == "fake-attribute" ? interop : runtime);
		}
		var output = new MemoryStream ();
		assembly.Write (output);
		output.Position = 0;
		return output;
	}
}
