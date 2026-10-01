using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Android.Sdk.TrimmableTypeMap;
using Microsoft.Android.Tasks;
using Xamarin.Android.Tasks;
using NUnit.Framework;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable]
public class ExtractTypeMapKeysFromAssembliesTests : IDisposable
{
	static readonly Version RuntimeVersion = new (11, 0, 0, 0);
	readonly string directory = Path.Combine (Path.GetTempPath (), nameof (ExtractTypeMapKeysFromAssembliesTests), Guid.NewGuid ().ToString ("N"));

	[SetUp]
	public void SetUp () => Directory.CreateDirectory (directory);

	[TearDown]
	public void Dispose ()
	{
		if (Directory.Exists (directory)) {
			Directory.Delete (directory, recursive: true);
		}
	}

	static TypeMapAttributeData Entry (string key, string? target = null) => new () {
		MapKey = key,
		ProxyTypeReference = "System.Object, System.Runtime",
		TargetTypeReference = target,
	};

	string Emit (string name, params TypeMapAttributeData [] entries)
	{
		string path = Path.Combine (directory, name + ".dll");
		Directory.CreateDirectory (Path.GetDirectoryName (path) ?? throw new InvalidOperationException ());
		var model = new TypeMapAssemblyData {
			AssemblyName = Path.GetFileName (name),
			ModuleName = Path.GetFileName (path),
		};
		model.Entries.AddRange (entries);
		using var stream = File.Create (path);
		new TypeMapAssemblyEmitter (RuntimeVersion).Emit (model, stream);
		return path;
	}

	(ExtractTypeMapKeysFromAssemblies task, TypeMapTaskBuildEngine engine) CreateTask (params string [] inputs)
	{
		var engine = new TypeMapTaskBuildEngine ();
		return (new ExtractTypeMapKeysFromAssemblies {
			BuildEngine = engine,
			LinkedAssemblies = inputs.Select (p => new TaskItem (p)).ToArray (),
			OutputFile = Path.Combine (directory, "output", "keys.txt"),
		}, engine);
	}

	[Test]
	public void UnionsAllAssembliesAndRidsWithExactCanonicalEncoding ()
	{
		string arm64 = Emit ("android-arm64/_Bindings.TypeMap",
			Entry ("test/Zebra"), Entry ("test/Outer$Inner"), Entry ("test/Alias[0]"), Entry ("test/\u00e9clair"));
		string x64 = Emit ("android-x64/_Bindings.TypeMap",
			Entry ("test/Alpha", "System.String, System.Runtime"), Entry ("test/Outer$Inner"), Entry ("test/Alias[1]"));
		var (task, _) = CreateTask (arm64, x64, arm64);

		Assert.IsTrue (task.Execute ());
		byte [] expected = new UTF8Encoding (false).GetBytes ("test/Alias\ntest/Alpha\ntest/Outer$Inner\ntest/Zebra\ntest/\u00e9clair\n");
		Assert.AreEqual (expected, File.ReadAllBytes (task.OutputFile));

		File.SetLastWriteTimeUtc (task.OutputFile, new DateTime (2000, 1, 1));
		task.LinkedAssemblies = task.LinkedAssemblies.Reverse ().ToArray ();
		Assert.IsTrue (task.Execute ());
		Assert.AreEqual (expected, File.ReadAllBytes (task.OutputFile));
		Assert.IsTrue (File.GetLastWriteTimeUtc (task.OutputFile).Year > 2000);
	}

	[TestCase ("test/A[123]", "test/A")]
	[TestCase ("test/A[000]", "test/A")]
	[TestCase ("test/A[9999999999999999999999999999999]", "test/A")]
	[TestCase ("test/Outer$Inner[2]", "test/Outer$Inner")]
	[TestCase ("test/\u2160[1]", "test/\u2160")]
	public void NormalizesOnlyFinalNonemptyAsciiDecimalAlias (string key, string expected)
	{
		var (task, _) = CreateTask (Emit ("Map", Entry (key)));
		Assert.IsTrue (task.Execute ());
		Assert.AreEqual (expected + "\n", File.ReadAllText (task.OutputFile));
	}

	[TestCase ("[Ljava/lang/Object;", "java/lang/Object\n")]
	[TestCase ("[[Ltest/Outer$Inner;", "test/Outer$Inner\n")]
	[TestCase ("[Ltest/Outer$Inner;[000]", "test/Outer$Inner\n")]
	[TestCase ("[B", "")]
	[TestCase ("[[Z", "")]
	[TestCase ("[I[0]", "")]
	public void ArrayEntriesKeepOnlyTheirObjectElementClass (string key, string expected)
	{
		var (task, engine) = CreateTask (Emit ("Map", Entry (key)));
		Assert.IsTrue (task.Execute ());
		Assert.IsEmpty (engine.Errors);
		Assert.AreEqual (new UTF8Encoding (false).GetBytes (expected), File.ReadAllBytes (task.OutputFile));
	}

	[Test]
	public void EmptyStubsAndOrdinaryManagedAssembliesContributeNoKeys ()
	{
		string stub = EmitStub ("Stub");
		var (task, _) = CreateTask (stub, typeof (TaskItem).Assembly.Location);
		Assert.IsTrue (task.Execute ());
		Assert.IsEmpty (File.ReadAllBytes (task.OutputFile));

		task.LinkedAssemblies = [new TaskItem (Emit ("Map", Entry ("test/Present")))];
		Assert.IsTrue (task.Execute ());
		task.LinkedAssemblies = [new TaskItem (stub)];
		Assert.IsTrue (task.Execute ());
		Assert.IsEmpty (File.ReadAllBytes (task.OutputFile));
	}

	[Test]
	public void MissingAssemblyInputsFail ()
	{
		var (task, engine) = CreateTask ();
		Assert.IsFalse (task.Execute ());
		Assert.IsTrue (engine.Errors.Any (e => e.Code == "XA4327"));
		Assert.IsFalse (File.Exists (task.OutputFile));
	}

	[Test]
	public void IgnoresAliasArraysAssociationsAssemblyTargetsAndUnrelatedAttributes ()
	{
		var model = new TypeMapAssemblyData { AssemblyName = "Map", ModuleName = "Map.dll" };
		model.Entries.Add (Entry ("test/Retained"));
		model.AliasHolders.Add (new AliasHolderData {
			Namespace = "_TypeMap.Aliases", TypeName = "StaleAliases", AliasKeys = ["test/Removed[0]", "test/Removed[1]"],
		});
		model.Associations.Add (new TypeMapAssociationData {
			SourceTypeReference = "Unrelated.Source, Unrelated",
			AliasProxyTypeReference = "Unrelated.Proxy, Unrelated",
		});
		string map = Path.Combine (directory, "Map.dll");
		using (var stream = File.Create (map)) {
			new TypeMapAssemblyEmitter (RuntimeVersion).Emit (model, stream);
		}
		string root = Path.Combine (directory, "Root.dll");
		using (var stream = File.Create (root)) {
			new RootTypeMapAssemblyGenerator (RuntimeVersion).Generate (["Map"], useSharedTypemapUniverse: false, stream);
		}
		var (task, _) = CreateTask (root, map);
		Assert.IsTrue (task.Execute ());
		Assert.AreEqual ("test/Retained\n", File.ReadAllText (task.OutputFile));
	}

	[TestCase ("missing")]
	[TestCase ("malformed")]
	[TestCase ("zero-byte")]
	[TestCase ("directory")]
	public void BadRequiredAssemblyFailsWithoutWritingOutput (string kind)
	{
		string path = Path.Combine (directory, "bad.dll");
		if (kind == "malformed") {
			File.WriteAllBytes (path, [1, 2, 3]);
		} else if (kind == "zero-byte") {
			File.WriteAllBytes (path, []);
		} else if (kind == "directory") {
			Directory.CreateDirectory (path);
		}
		var (task, engine) = CreateTask (Emit ("Valid", Entry ("test/Valid")), path);
		Assert.IsFalse (task.Execute ());
		Assert.IsTrue (engine.Errors.Any (e => e.Code == "XA4327" && e.Message != null && e.Message.Contains (path, StringComparison.Ordinal)));
		Assert.IsFalse (File.Exists (task.OutputFile));
	}

	[TestCase ("")]
	[TestCase ("test/Invalid\nName")]
	[TestCase ("test/Invalid\rName")]
	[TestCase ("test/Invalid\0Name")]
	[TestCase ("test/A[]")]
	[TestCase ("test/A[-1]")]
	[TestCase ("test/A[1x]")]
	[TestCase ("test/A[1]Extra")]
	[TestCase ("test/A[1")]
	[TestCase ("test/A[")]
	[TestCase ("[0]")]
	[TestCase ("test/A[\u0661]")]
	[TestCase ("test/A[1][2]")]
	[TestCase ("test.Invalid")]
	[TestCase ("test/*")]
	[TestCase ("test/")]
	[TestCase ("/test")]
	[TestCase ("test//Invalid")]
	[TestCase ("test/Invalid\u200bName")]
	[TestCase ("[L;")]
	[TestCase ("[V")]
	[TestCase ("[Ltest/A")]
	[TestCase ("[[")]
	public void InvalidKeyFails (string key)
	{
		var (task, engine) = CreateTask (Emit ("Map", Entry (key)));
		Assert.IsFalse (task.Execute ());
		Assert.IsTrue (engine.Errors.Any (e => e.Code == "XA4327"));
	}

	[Test]
	public void NetmoduleIsNotAValidAssemblyInput ()
	{
		var metadata = new MetadataBuilder ();
		metadata.AddModule (0, metadata.GetOrAddString ("Invalid.netmodule"), metadata.GetOrAddGuid (Guid.NewGuid ()), default, default);
		metadata.AddTypeDefinition (default, default, metadata.GetOrAddString ("<Module>"), default,
			MetadataTokens.FieldDefinitionHandle (1), MetadataTokens.MethodDefinitionHandle (1));
		var image = new BlobBuilder ();
		new ManagedPEBuilder (new PEHeaderBuilder (imageCharacteristics: Characteristics.Dll),
			new MetadataRootBuilder (metadata), new BlobBuilder ()).Serialize (image);
		string path = Path.Combine (directory, "Invalid.netmodule");
		using (var stream = File.Create (path)) {
			image.WriteContentTo (stream);
		}
		var (task, engine) = CreateTask (path);
		Assert.IsFalse (task.Execute ());
		Assert.IsTrue (engine.Errors.Any (e => e.Code == "XA4327"));
	}

	[Test]
	public void OutputWriteFailureIsReported ()
	{
		var (task, engine) = CreateTask (Emit ("Map", Entry ("test/Valid")));
		task.OutputFile = directory;
		Assert.IsFalse (task.Execute ());
		Assert.IsTrue (engine.Errors.Any (e => e.Code == "XA4327"));
	}

	[TestCase ("prolog")]
	[TestCase ("null-key")]
	[TestCase ("null-type")]
	[TestCase ("missing-type")]
	[TestCase ("named-arguments")]
	[TestCase ("trailing-bytes")]
	[TestCase ("argument-count")]
	[TestCase ("key-type")]
	[TestCase ("type-parameter")]
	[TestCase ("static-method")]
	[TestCase ("method-name")]
	public void MalformedTypeMapAttributeFails (string defect)
	{
		var (task, engine) = CreateTask (EmitAttribute (defect));
		Assert.IsFalse (task.Execute ());
		Assert.IsTrue (engine.Errors.Any (e => e.Code == "XA4327"));
		Assert.IsFalse (File.Exists (task.OutputFile));
	}

	[TestCase ("type-attribute")]
	[TestCase ("other-namespace")]
	public void OnlyAssemblyTypeMapAttributesAreEntries (string kind)
	{
		var (task, _) = CreateTask (EmitAttribute (kind));
		Assert.IsTrue (task.Execute ());
		Assert.IsEmpty (File.ReadAllBytes (task.OutputFile));
	}

	string EmitAttribute (string kind)
	{
		var pe = new PEAssemblyBuilder (RuntimeVersion);
		pe.EmitPreamble ("Attributes", "Attributes.dll");
		var metadata = pe.Metadata;
		var systemType = metadata.AddTypeReference (pe.SystemRuntimeRef,
			metadata.GetOrAddString ("System"), metadata.GetOrAddString ("Type"));
		var openAttribute = metadata.AddTypeReference (pe.SystemRuntimeInteropServicesRef,
			metadata.GetOrAddString (kind == "other-namespace" ? "Unrelated" : "System.Runtime.InteropServices"),
			metadata.GetOrAddString ("TypeMapAttribute`1"));
		var attributeType = pe.MakeGenericTypeSpec (openAttribute, systemType);
		var constructor = pe.AddMemberRef (attributeType, kind == "method-name" ? "NotAConstructor" : ".ctor", s =>
			s.MethodSignature (isInstanceMethod: kind != "static-method").Parameters (kind == "argument-count" ? 1 : 2,
				r => r.Void (), p => {
					if (kind == "key-type") {
						p.AddParameter ().Type ().Int32 ();
					} else {
						p.AddParameter ().Type ().String ();
					}
					if (kind != "argument-count") {
						if (kind == "type-parameter") {
							p.AddParameter ().Type ().String ();
						} else {
							p.AddParameter ().Type ().Type (systemType, isValueType: false);
						}
					}
				}));
		var value = new BlobBuilder ();
		value.WriteUInt16 ((ushort) (kind == "prolog" ? 2 : 1));
		value.WriteSerializedString (kind == "null-key" ? null : "test/Key");
		if (kind != "missing-type") {
			value.WriteSerializedString (kind == "null-type" ? null : "System.Object, System.Runtime");
		}
		value.WriteUInt16 ((ushort) (kind == "named-arguments" ? 1 : 0));
		if (kind == "trailing-bytes") {
			value.WriteByte (42);
		}
		EntityHandle parent = EntityHandle.AssemblyDefinition;
		if (kind == "type-attribute") {
			parent = metadata.AddTypeDefinition (TypeAttributes.Public, default, metadata.GetOrAddString ("NotAnAssembly"),
				default, MetadataTokens.FieldDefinitionHandle (1), MetadataTokens.MethodDefinitionHandle (1));
		}
		metadata.AddCustomAttribute (parent, constructor, metadata.GetOrAddBlob (value));
		string path = Path.Combine (directory, "Attributes.dll");
		using var stream = File.Create (path);
		pe.WritePE (stream);
		return path;
	}

	[Test]
	public async Task RealILLinkRetainsOnlyLiveTypeMapAttributes ()
	{
		// Resolve unused emitter references and optional host-runtime facades without
		// weakening the linker's unresolved-reference checks.
		EmitStub ("Mono.Android");
		EmitStub ("Java.Interop");
		EmitStub ("Mono.Android.Runtime");
		EmitStub ("System.Configuration.ConfigurationManager");
		EmitStub ("System.Drawing.Common");
		EmitStub ("System.Security.Permissions");
		EmitTargets ();
		string input = Emit ("_Bindings.TypeMap",
			Entry ("test/Unconditional"),
			Entry ("test/Surviving", "Targets.Surviving, Targets"),
			Entry ("test/Removed", "Targets.Removed, Targets"),
			Entry ("test/Alias[0]", "Targets.Surviving, Targets"),
			Entry ("test/Alias[1]", "Targets.Removed, Targets"));
		string root = EmitLinkerRoot ("_Bindings.TypeMap");
		var (before, _) = CreateTask (input);
		Assert.IsTrue (before.Execute ());
		StringAssert.Contains ("test/Removed\n", File.ReadAllText (before.OutputFile));

		string linkedDirectory = Path.Combine (directory, "linked");
		await RunLinker (root, linkedDirectory);
		string linked = Path.Combine (linkedDirectory, "_Bindings.TypeMap.dll");
		Assert.IsTrue (File.Exists (linked));
		var (after, _) = CreateTask (linked);
		Assert.IsTrue (after.Execute ());
		Assert.AreEqual ("test/Alias\ntest/Surviving\ntest/Unconditional\n", File.ReadAllText (after.OutputFile));

		using var pe = new PEReader (File.OpenRead (linked));
		var reader = pe.GetMetadataReader ();
		var keys = new List<string> ();
		foreach (var handle in reader.GetAssemblyDefinition ().GetCustomAttributes ()) {
			var attribute = reader.GetCustomAttribute (handle);
			if (reader.GetCustomAttributeFullName (attribute, after.Log) != "System.Runtime.InteropServices.TypeMapAttribute`1") {
				continue;
			}
			Assert.AreEqual (HandleKind.MemberReference, attribute.Constructor.Kind);
			Assert.AreEqual (HandleKind.TypeSpecification, reader.GetMemberReference ((MemberReferenceHandle) attribute.Constructor).Parent.Kind);
			var blob = reader.GetBlobReader (attribute.Value);
			Assert.AreEqual (1, blob.ReadUInt16 ());
			keys.Add (blob.ReadSerializedString () ?? throw new InvalidOperationException ());
		}
		Assert.AreEqual (new [] { "test/Alias[0]", "test/Surviving", "test/Unconditional" }, keys.OrderBy (k => k, StringComparer.Ordinal));
	}

	string EmitStub (string name)
	{
		string path = Path.Combine (directory, name + ".dll");
		using var stream = File.Create (path);
		new TypeMapAssemblyGenerator (RuntimeVersion).GenerateEmpty (stream, name);
		return path;
	}

	void EmitTargets ()
	{
		var pe = new PEAssemblyBuilder (RuntimeVersion);
		pe.EmitPreamble ("Targets", "Targets.dll");
		var objectType = pe.Metadata.AddTypeReference (pe.SystemRuntimeRef, pe.Metadata.GetOrAddString ("System"), pe.Metadata.GetOrAddString ("Object"));
		foreach (string name in new [] { "Surviving", "Removed" }) {
			pe.Metadata.AddTypeDefinition (TypeAttributes.Public, pe.Metadata.GetOrAddString ("Targets"), pe.Metadata.GetOrAddString (name),
				objectType, MetadataTokens.FieldDefinitionHandle (1), MetadataTokens.MethodDefinitionHandle (1));
		}
		using var stream = File.Create (Path.Combine (directory, "Targets.dll"));
		pe.WritePE (stream);
	}

	string EmitLinkerRoot (string mapName)
	{
		var pe = new PEAssemblyBuilder (RuntimeVersion);
		pe.EmitPreamble ("Root", "Root.dll");
		var metadata = pe.Metadata;
		TypeReferenceHandle TypeRef (EntityHandle scope, string ns, string name) =>
			metadata.AddTypeReference (scope, metadata.GetOrAddString (ns), metadata.GetOrAddString (name));
		var anchor = TypeRef (pe.FindOrAddAssemblyRef (mapName), "", "__TypeMapAnchor");
		var attributeType = TypeRef (pe.SystemRuntimeInteropServicesRef, "System.Runtime.InteropServices", "TypeMapAssemblyTargetAttribute`1");
		var attributeCtor = pe.AddMemberRef (pe.MakeGenericTypeSpec (attributeType, anchor), ".ctor",
			s => s.MethodSignature (isInstanceMethod: true).Parameters (1, r => r.Void (), p => p.AddParameter ().Type ().String ()));
		metadata.AddCustomAttribute (EntityHandle.AssemblyDefinition, attributeCtor, pe.BuildAttributeBlob (b => b.WriteSerializedString (mapName)));
		var mappingType = TypeRef (pe.SystemRuntimeInteropServicesRef, "System.Runtime.InteropServices", "TypeMapping");
		var dictionaryType = TypeRef (pe.SystemRuntimeRef, "System.Collections.Generic", "IReadOnlyDictionary`2");
		var systemType = TypeRef (pe.SystemRuntimeRef, "System", "Type");
		var getMapping = pe.AddMemberRef (mappingType, "GetOrCreateExternalTypeMapping",
			s => s.MethodSignature (genericParameterCount: 1).Parameters (0, r => {
				var args = r.Type ().GenericInstantiation (dictionaryType, 2, isValueType: false);
				args.AddArgument ().String ();
				args.AddArgument ().Type (systemType, isValueType: false);
			}, p => { }));
		var methodSignature = new BlobBuilder ();
		new BlobEncoder (methodSignature).MethodSpecificationSignature (1).AddArgument ().Type (anchor, isValueType: false);
		var method = metadata.AddMethodSpecification (getMapping, metadata.GetOrAddBlob (methodSignature));
		metadata.AddTypeDefinition (TypeAttributes.Public, default, metadata.GetOrAddString ("Root"),
			TypeRef (pe.SystemRuntimeRef, "System", "Object"), MetadataTokens.FieldDefinitionHandle (1), MetadataTokens.MethodDefinitionHandle (1));
		var survivingType = TypeRef (pe.FindOrAddAssemblyRef ("Targets"), "Targets", "Surviving");
		pe.EmitBody ("Run", MethodAttributes.Public | MethodAttributes.Static,
			s => s.MethodSignature ().Parameters (0, r => r.Void (), p => { }),
			il => {
				il.LoadToken (survivingType);
				il.PopValue ();
				il.Call (method, 0, returnsValue: true);
				il.PopValue ();
				il.Return ();
			});
		string path = Path.Combine (directory, "Root.dll");
		using var stream = File.Create (path);
		pe.WritePE (stream);
		return path;
	}

	async Task RunLinker (string root, string output)
	{
		string linker = typeof (ExtractTypeMapKeysFromAssembliesTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute> ()
			.Single (a => a.Key == "ILLinkPath").Value ?? throw new InvalidOperationException ("Missing ILLinkPath.");
		string runtimeDirectory = Path.GetDirectoryName (typeof (object).Assembly.Location) ?? throw new InvalidOperationException ();
		var start = new ProcessStartInfo (Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet") {
			RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
		};
		foreach (string arg in new [] {
			"exec", "--fx-version", Path.GetFileName (runtimeDirectory), linker,
			"-a", Path.GetFileNameWithoutExtension (root), "-reference", root, "-d", directory, "-d", runtimeDirectory, "-out", output,
			"--typemap-entry-assembly", "Root", "--skip-unresolved", "false",
		}) {
			start.ArgumentList.Add (arg);
		}
		using var process = Process.Start (start) ?? throw new InvalidOperationException ("Cannot start ILLink.");
		var stdout = process.StandardOutput.ReadToEndAsync ();
		var stderr = process.StandardError.ReadToEndAsync ();
		using var timeout = new CancellationTokenSource (TimeSpan.FromMinutes (2));
		try {
			await process.WaitForExitAsync (timeout.Token);
		} catch (OperationCanceledException) {
			process.Kill (entireProcessTree: true);
			throw;
		}
		Assert.IsTrue (process.ExitCode == 0, await stdout + await stderr);
	}

}
