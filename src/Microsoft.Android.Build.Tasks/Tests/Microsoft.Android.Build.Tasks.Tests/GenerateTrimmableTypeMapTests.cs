using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Android.Sdk.TrimmableTypeMap;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class GenerateTrimmableTypeMapTests : BaseTest {

		[Test]
		public void AssemblyQualifiedApplicationTypeReferencesSelectCorrectPeer ()
		{
			var wrongPeer = new JavaPeerInfo {
				JavaName = "wrong/Agent",
				CompatJniName = "wrong/Agent",
				ManagedTypeName = "Example.Agent",
				ManagedTypeNamespace = "Example",
				ManagedTypeShortName = "Agent",
				AssemblyName = "Wrong",
				BaseJavaName = "android/app/Activity",
			};
			var expectedPeer = new JavaPeerInfo {
				JavaName = "expected/Agent",
				CompatJniName = "expected/Agent",
				ManagedTypeName = "Example.Agent",
				ManagedTypeNamespace = "Example",
				ManagedTypeShortName = "Agent",
				AssemblyName = "Expected",
				BaseJavaName = "android/app/backup/BackupAgent",
			};
			var peers = new List<JavaPeerInfo> { wrongPeer, expectedPeer };
			var properties = new Dictionary<string, object?> {
				["BackupAgent"] = "Example.Agent, Expected",
			};

			TrimmableTypeMapGenerator.RootAssemblyApplicationTypeReferences (peers, properties);
			Assert.IsFalse (wrongPeer.IsUnconditional);
			Assert.IsTrue (expectedPeer.IsUnconditional);

			var application = new XElement ("application");
			AssemblyLevelElementBuilder.ApplyApplicationProperties (application, properties, peers);
			XNamespace android = "http://schemas.android.com/apk/res/android";
			Assert.AreEqual ("expected.Agent", (string?)application.Attribute (android + "backupAgent"));
		}

		[TestCase ("GetZzaHandler")]
		[TestCase ("")]
		[TestCase (null)]
		public void RegisteredMethodsDoNotRequireJavaOverrides (string? connector)
		{
			var peer = new JavaPeerInfo {
				JavaName = "example/Listener",
				CompatJniName = "example/Listener",
				ManagedTypeName = "Example.Listener",
				ManagedTypeNamespace = "Example",
				ManagedTypeShortName = "Listener",
				AssemblyName = "Example",
				BaseJavaName = "java/lang/Object",
				ImplementedInterfaceJavaNames = ["example/ListenerInterface"],
				MarshalMethods = [new MarshalMethodInfo {
					JniName = "zza",
					JniSignature = "(Ljava/lang/String;)V",
					NativeCallbackName = "n_zza",
					ManagedMethodName = "Zza",
					Connector = connector,
					IsInterfaceImplementation = true,
				}],
			};

			using var writer = new StringWriter ();
			new JcwJavaSourceGenerator ().Generate (peer, writer);
			var java = writer.ToString ();

			StringAssert.Contains ("example.ListenerInterface", java);
			StringAssert.Contains ("public void zza (java.lang.String p0)", java);
			StringAssert.Contains ("n_zza (p0);", java);
			StringAssert.Contains ("public native void n_zza (java.lang.String p0);", java);
			StringAssert.DoesNotContain ("@Override", java,
				"A registered compatibility method can exist only in the managed interface, not its Java counterpart.");
		}

		[TestCase ("method", true, false)]
		[TestCase ("method", true, true)]
		[TestCase ("constructor", true, false)]
		[TestCase ("constructor", true, true)]
		[TestCase ("register", false, false)]
		[TestCase ("register", false, true)]
		[TestCase ("binding", false, false)]
		[TestCase ("binding", false, true)]
		[TestCase ("interface", false, false)]
		[TestCase ("interface", false, true)]
		public void ExplicitExportsAreUnconditionalEntries (string shape, bool unconditional, bool alias)
		{
			var peer = new JavaPeerInfo {
				JavaName = "example/Peer",
				CompatJniName = "example/Peer",
				ManagedTypeName = "Example.Peer",
				ManagedTypeNamespace = "Example",
				ManagedTypeShortName = "Peer",
				AssemblyName = "Example",
				BaseJavaName = "java/lang/Object",
				DoNotGenerateAcw = shape == "binding",
				IsInterface = shape == "interface",
				ActivationCtor = new ActivationCtorInfo {
					DeclaringTypeName = "Example.Peer",
					DeclaringAssemblyName = "Example",
					DeclaringType = new TypeRefData { ManagedTypeName = "Example.Peer", AssemblyName = "Example" },
					Style = ActivationCtorStyle.XamarinAndroid,
				},
				JavaConstructors = [new JavaConstructorInfo { JniSignature = "()V", ConstructorIndex = 0 }],
				MarshalMethods = [new MarshalMethodInfo {
					JniName = shape == "constructor" ? ".ctor" : "call",
					JniSignature = "()V",
					NativeCallbackName = "n_call",
					ManagedMethodName = shape == "constructor" ? ".ctor" : "Call",
					IsConstructor = shape == "constructor",
					IsExport = shape != "register",
				}],
			};
			var peers = new List<JavaPeerInfo> { peer };
			if (alias) {
				peers.Add (peer with {
					ManagedTypeName = "Example.OtherPeer",
					ManagedTypeShortName = "OtherPeer",
					MarshalMethods = [],
				});
			}

			var generator = new TypeMapAssemblyGenerator (new Version (11, 0));
			var model = generator.CreateModel (peers, "_Example.TypeMap");
			var entry = model.Entries.Single (entry => entry.MapKey == (alias ? "example/Peer[1]" : "example/Peer"));
			Assert.AreEqual (unconditional, entry.TargetTypeReference is null);
			if (alias) {
				Assert.AreEqual (unconditional, model.Entries.Single (entry => entry.MapKey == "example/Peer").TargetTypeReference is null,
					"The alias holder must survive when an explicitly exporting alias survives.");
				Assert.IsNotNull (model.Entries.Single (entry => entry.MapKey == "example/Peer[0]").TargetTypeReference,
					"An export on one alias must not unconditionally retain another alias.");
			}

			using var stream = new MemoryStream ();
			generator.Generate (model, stream, useSharedTypemapUniverse: true);
			stream.Position = 0;
			using var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly (stream);
			var attribute = assembly.CustomAttributes.Single (attribute =>
				attribute.AttributeType.Name == "TypeMapAttribute`1" &&
				(string) attribute.ConstructorArguments [0].Value == entry.MapKey);
			Assert.AreEqual (unconditional ? 2 : 3, attribute.ConstructorArguments.Count,
				"Only unconditional entries should use the rooting two-argument TypeMap attribute.");
		}

		[TestCase (false, false, false)]
		[TestCase (false, false, true)]
		[TestCase (false, true, false)]
		[TestCase (false, true, true)]
		[TestCase (true, false, false)]
		[TestCase (true, false, true)]
		[TestCase (true, true, false)]
		[TestCase (true, true, true)]
		public void ConstructorCallbacksMatchJavaCallSites (bool isAbstract, bool deferred, bool hasCallback)
		{
			var constructor = new MarshalMethodInfo {
				JniName = ".ctor",
				JniSignature = "()V",
				NativeCallbackName = "n_ctor",
				ManagedMethodName = ".ctor",
				IsConstructor = true,
			};
			var callback = new MarshalMethodInfo {
				JniName = "onCallback",
				JniSignature = "()V",
				NativeCallbackName = "n_onCallback",
				ManagedMethodName = "OnCallback",
				Connector = "GetOnCallbackHandler",
			};
			var peer = new JavaPeerInfo {
				JavaName = "example/Peer",
				CompatJniName = "example/Peer",
				ManagedTypeName = "Example.Peer",
				ManagedTypeNamespace = "Example",
				ManagedTypeShortName = "Peer",
				AssemblyName = "Example",
				BaseJavaName = "java/lang/Object",
				IsAbstract = isAbstract,
				CannotRegisterInStaticConstructor = deferred,
				ActivationCtor = new ActivationCtorInfo {
					DeclaringTypeName = "Example.Peer",
					DeclaringAssemblyName = "Example",
					DeclaringType = new TypeRefData { ManagedTypeName = "Example.Peer", AssemblyName = "Example" },
					Style = ActivationCtorStyle.XamarinAndroid,
				},
				JavaConstructors = [new JavaConstructorInfo { JniSignature = "()V", ConstructorIndex = 0 }],
				MarshalMethods = hasCallback ? [constructor, callback] : [constructor],
			};
			using var writer = new StringWriter ();
			new JcwJavaSourceGenerator ().Generate (peer, writer);
			var java = writer.ToString ();
			StringAssert.Contains ("public Peer ()", java);
			StringAssert.Contains ("super ();", java);
			StringAssert.Contains ("mono.android.IGCUserPeer", java);
			StringAssert.Contains ("private java.util.ArrayList refList;", java);
			StringAssert.DoesNotContain ("__md_methods", java);

			bool hasNativeConstructor = !isAbstract && !deferred;
			Assert.AreEqual (hasNativeConstructor, java.Contains ("if (getClass () == Peer.class) nctor_0 ();", StringComparison.Ordinal));
			Assert.AreEqual (hasNativeConstructor, java.Contains ("private native void nctor_0 ();", StringComparison.Ordinal));
			Assert.AreEqual (deferred && hasCallback, java.Contains ("private static boolean __md_natives_registered;", StringComparison.Ordinal));
			Assert.AreEqual (deferred && hasCallback, java.Contains ("private static synchronized void __md_registerNatives ()", StringComparison.Ordinal));
			Assert.AreEqual (deferred && hasCallback, java.Contains ("__md_registerNatives ();", StringComparison.Ordinal));
			Assert.AreEqual (hasCallback, java.Contains ("public native void n_onCallback ();", StringComparison.Ordinal));

			var generator = new TypeMapAssemblyGenerator (new Version (11, 0));
			var model = generator.CreateModel ([peer], "_Example.TypeMap");
			var proxy = model.ProxyTypes.Single ();
			Assert.AreEqual (hasNativeConstructor ? 1 : 0, proxy.UcoConstructors.Count);
			string [] expectedRegistrations = hasNativeConstructor
				? (hasCallback ? ["nctor_0", "n_onCallback"] : ["nctor_0"])
				: (hasCallback ? ["n_onCallback"] : []);
			CollectionAssert.AreEquivalent (expectedRegistrations,
				proxy.NativeRegistrations.Select (registration => registration.JniMethodName));
			Assert.IsNotNull (proxy.ActivationCtor, "Managed peer creation must retain its activation constructor.");

			using var stream = new MemoryStream ();
			generator.Generate (model, stream, useSharedTypemapUniverse: false);
			stream.Position = 0;
			using var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly (stream);
			var emittedProxy = assembly.MainModule.Types.Single (type => type.Name == proxy.TypeName);
			Assert.AreEqual (hasNativeConstructor, emittedProxy.Methods.Any (method => method.Name == "nctor_0_uco"));
			Assert.IsTrue (emittedProxy.Methods.Any (method => method.Name == "CreateInstance"));
			foreach (var uco in proxy.UcoMethods) {
				var wrapper = emittedProxy.Methods.Single (method => method.Name == uco.WrapperName);
				var handler = wrapper.Body.ExceptionHandlers.Single ();
				Assert.AreEqual (wrapper.Body.Instructions [0], handler.TryStart, "No bridge wait is needed before the callback.");
				var unhandledException = wrapper.Body.Instructions.Select (instruction => instruction.Operand)
					.OfType<Mono.Cecil.MethodReference> ().Single (method => method.Name == "UnhandledException");
				Assert.AreEqual ("Android.Runtime.AndroidEnvironment", unhandledException.DeclaringType.FullName);
				Assert.AreEqual ("Mono.Android", unhandledException.DeclaringType.Scope.Name);
			}
			TestContext.Out.WriteLine ($"abstract={isAbstract}, deferred={deferred}, callback={hasCallback}: Java nctor={hasNativeConstructor}, native registrations=[{string.Join (", ", proxy.NativeRegistrations.Select (registration => registration.JniMethodName))}]");
		}

		[Test]
		public void Execute_EmptyAssemblyList_Succeeds ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			var task = CreateTask ([], outputDir, javaDir);

			Assert.IsTrue (task.Execute (), "Task should succeed with empty assembly list.");
			Assert.IsEmpty (task.GeneratedAssemblies);
			Assert.IsEmpty (task.GeneratedJavaFiles);
		}

		[TestCase ("java", "java/../java", TestName = "Execute_EquivalentJavaDirectoriesAreRejected_DotSegments")]
		[TestCase ("java", "java/", TestName = "Execute_EquivalentJavaDirectoriesAreRejected_OutputSeparator")]
		[TestCase ("java/", "java", TestName = "Execute_EquivalentJavaDirectoriesAreRejected_InputSeparator")]
		public void Execute_EquivalentJavaDirectoriesAreRejected (string inputDirectory, string outputDirectory)
		{
			var directory = Path.Combine (Root, "temp", TestName);
			var javaDirectory = Path.Combine (directory, "java");
			Directory.CreateDirectory (javaDirectory);
			var inputFile = Path.Combine (javaDirectory, "Input.java");
			File.WriteAllText (inputFile, "input");
			var errors = new List<BuildErrorEventArgs> ();
			var task = CreateTask ([], "typemap", outputDirectory, errors: errors);
			task.TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ());
			task.JavaSourceInputDirectory = inputDirectory;
			task.CleanJavaSourceOutputDirectory = true;

			Assert.IsFalse (task.Execute (), "Equivalent input and output directories must be rejected before cleanup.");
			Assert.IsTrue (errors.Any (error => error.Code == "XA4254"));
			FileAssert.Exists (inputFile, "Cleanup must not delete input Java sources.");
		}

		[TestCase (false)]
		[TestCase (true)]
		public void Execute_DeletesOnlyStaleJavaSources (bool cleanOutput)
		{
			var directory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (directory);
			var assembly = CreateRegisteredPeerAssembly (directory);
			var generatedPath = Path.Combine (directory, "java", "com", "example", "Peer.java");
			var stalePath = Path.Combine (directory, "java", "old", "Stale.java");
			Directory.CreateDirectory (Path.GetDirectoryName (generatedPath));
			Directory.CreateDirectory (Path.GetDirectoryName (stalePath));
			File.WriteAllText (generatedPath, "old generated content");
			File.WriteAllText (stalePath, "stale");
			var task = CreateTask ([new TaskItem (assembly)], "typemap", "java");
			task.TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ());
			task.GenerateTypeMapAssemblies = false;
			task.CleanJavaSourceOutputDirectory = cleanOutput;

			Assert.IsTrue (task.Execute ());
			Assert.AreEqual (1, task.GeneratedJavaFiles.Length);
			FileAssert.Exists (generatedPath, "A regenerated Java source must not be deleted as stale.");
			FileAssert.DoesNotExist (stalePath);
			var deleted = task.DeletedJavaFiles.Single ();
			Assert.AreEqual (stalePath, deleted.ItemSpec);
			Assert.AreEqual (Path.Combine ("old", "Stale.java"), deleted.GetMetadata ("RelativePath"));
		}

		[Test]
		public void Execute_EquivalentAssemblyPathsAreRecognized ()
		{
			var directory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (directory);
			CreateRegisteredPeerAssembly (directory);
			var messages = new List<BuildMessageEventArgs> ();
			var task = CreateTask ([
				new TaskItem ("Fixture.dll"),
				new TaskItem (Path.Combine (".", "Fixture.dll")),
			], "typemap", "java", messages: messages);
			task.TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ());
			task.ResolvedFrameworkAssemblies = [new TaskItem (Path.Combine (".", ".", "Fixture.dll"))];

			Assert.IsTrue (task.Execute ());
			Assert.IsTrue (messages.Any (message => message.Message?.Contains ("Scanned 1 assemblies", StringComparison.Ordinal) == true));
			using var typeMap = Mono.Cecil.AssemblyDefinition.ReadAssembly (Path.Combine (directory, "typemap", "_Fixture.TypeMap.dll"));
			var peer = typeMap.CustomAttributes.Single (attribute =>
				attribute.AttributeType.Name == "TypeMapAttribute`1" &&
				(string) attribute.ConstructorArguments [0].Value == "com/example/Peer");
			Assert.AreEqual (3, peer.ConstructorArguments.Count, "The equivalent framework path must identify the peer as a framework type.");
		}

		[Test]
		public void Execute_RelativePathsUseTaskDirectory ()
		{
			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			for (int i = 0; i < 2; i++) {
				var directory = Path.Combine (Root, "temp", TestName, $"project{i}");
				Directory.CreateDirectory (Path.Combine (directory, "input"));
				File.Copy (monoAndroidItem.ItemSpec, Path.Combine (directory, "input", "Mono.Android.dll"));
				File.WriteAllText (Path.Combine (directory, "input", "AndroidManifest.xml"),
					$"""<manifest xmlns:android="http://schemas.android.com/apk/res/android"><application android:label="Project{i}" /></manifest>""");
				File.WriteAllText (Path.Combine (directory, "input", "custom-view-map.txt"), "Android.Views.View;android.view.View\n");
				var environment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ());
				var output = Path.Combine ($"obj{i}", "typemap");
				var java = Path.Combine ($"obj{i}", "java");
				var linkedJava = Path.Combine ($"obj{i}", "linked-java");
				var assemblies = new ITaskItem [] { new TaskItem (Path.Combine ("input", "Mono.Android.dll")) };
				var first = CreateTask (assemblies, output, java);
				first.TaskEnvironment = environment;
				first.ResolvedFrameworkAssemblies = [new TaskItem (Path.Combine ("input", ".", "Mono.Android.dll"))];
				first.ManifestTemplate = Path.Combine ("input", "AndroidManifest.xml");
				first.CustomViewMapFile = Path.Combine ("input", "custom-view-map.txt");
				first.MergedAndroidManifestOutput = Path.Combine (output, "AndroidManifest.xml");
				first.AcwMapOutputFile = Path.Combine (output, "acw-map.txt");
				first.ApplicationRegistrationOutputFile = Path.Combine (output, "ApplicationRegistration.java");
				first.GeneratedAssembliesListFile = Path.Combine (output, "assemblies.txt");
				first.PackageName = $"com.project{i}";
				first.AndroidApiLevel = "35";
				first.SupportedOSPlatformVersion = "24";
				first.RuntimeProviderJavaName = "mono.MonoRuntimeProvider";

				Assert.IsTrue (first.Execute ());
				CollectionAssert.AreEqual (first.GeneratedAssemblies.Select (item => item.ItemSpec),
					File.ReadAllLines (Path.Combine (directory, first.GeneratedAssembliesListFile)));
				Assert.IsTrue (first.GeneratedAssemblies.All (item => !Path.IsPathRooted (item.ItemSpec)));
				FileAssert.Exists (Path.Combine (directory, first.AcwMapOutputFile));
				FileAssert.Exists (Path.Combine (directory, first.ApplicationRegistrationOutputFile));
				StringAssert.Contains ($"Project{i}", File.ReadAllText (Path.Combine (directory, first.MergedAndroidManifestOutput)));
				using (var typeMap = Mono.Cecil.AssemblyDefinition.ReadAssembly (Path.Combine (directory, output, "_Mono.Android.TypeMap.dll"))) {
					var view = typeMap.CustomAttributes.Single (attribute =>
						attribute.AttributeType.Name == "TypeMapAttribute`1" &&
						(string) attribute.ConstructorArguments [0].Value == "android/view/View");
					Assert.AreEqual (2, view.ConstructorArguments.Count, "The custom-view map must root View.");
				}

				var javaItem = first.GeneratedJavaFiles.First ();
				Assert.IsFalse (Path.IsPathRooted (javaItem.ItemSpec));
				File.AppendAllText (Path.Combine (directory, javaItem.ItemSpec), "\n// copied input");
				var staleRelativePath = Path.Combine ("old", "Stale.java");
				var stalePath = Path.Combine (directory, linkedJava, staleRelativePath);
				Directory.CreateDirectory (Path.GetDirectoryName (stalePath));
				File.WriteAllText (stalePath, "stale");
				var messages = new List<BuildMessageEventArgs> ();
				var second = CreateTask (assemblies, output, linkedJava, messages: messages);
				second.TaskEnvironment = environment;
				second.ResolvedFrameworkAssemblies = first.ResolvedFrameworkAssemblies;
				second.CustomViewMapFile = first.CustomViewMapFile;
				second.JavaSourceInputDirectory = java;
				second.CleanJavaSourceOutputDirectory = i == 1;

				Assert.IsTrue (second.Execute ());
				Assert.IsTrue (messages.Any (message => message.Message?.Contains ("_Mono.Android.TypeMap: unchanged, skipping emission", StringComparison.Ordinal) == true));
				var copiedPath = Path.Combine (directory, linkedJava, Path.GetRelativePath (java, javaItem.ItemSpec));
				StringAssert.Contains ("// copied input", File.ReadAllText (copiedPath));
				FileAssert.DoesNotExist (stalePath);
				var deleted = second.DeletedJavaFiles.Single ();
				Assert.AreEqual (stalePath, deleted.ItemSpec);
				Assert.AreEqual (staleRelativePath, deleted.GetMetadata ("RelativePath"));
			}
		}

		[Test]
		public void Execute_InvalidTargetFrameworkVersion_Fails ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			var errors = new List<BuildErrorEventArgs> ();
			var task = new GenerateTrimmableTypeMap {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors),
				ResolvedAssemblies = [],
				OutputDirectory = outputDir,
				JavaSourceOutputDirectory = javaDir,
				TargetFrameworkVersion = "not-a-version",
			};

			Assert.IsFalse (task.Execute (), "Task should fail with invalid TargetFrameworkVersion.");
			Assert.IsNotEmpty (errors, "Should have logged an error.");
		}

		[Test]
		public void LoadCustomViewTypeNames_ParsesKeysAndIgnoresBlankLines ()
		{
			var directory = Path.Combine (Root, "temp", TestName);
			var mapFile = Path.Combine (directory, "custom-view-map.txt");
			Directory.CreateDirectory (directory);
			File.WriteAllText (mapFile, "Example.View;example.View\n\nExample.View;example.OtherView\nOther.View;other.View\n");

			var typeNames = GenerateTrimmableTypeMap.LoadCustomViewTypeNames (mapFile);

			CollectionAssert.AreEquivalent (new [] { "Example.View", "Other.View" }, typeNames);
		}

		[Test]
		public void LoadCustomViewTypeNames_InvalidEntryThrows ()
		{
			var directory = Path.Combine (Root, "temp", TestName);
			var mapFile = Path.Combine (directory, "custom-view-map.txt");
			Directory.CreateDirectory (directory);
			File.WriteAllText (mapFile, "invalid");

			var exception = Assert.Throws<InvalidDataException> (() => GenerateTrimmableTypeMap.LoadCustomViewTypeNames (mapFile));

			StringAssert.Contains ("Invalid custom view map entry 'invalid'", exception?.Message);
		}

		[Test]
		public void Execute_WithMonoAndroid_ProducesOutputs ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			var task = CreateTask (new [] { monoAndroidItem }, outputDir, javaDir);

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			Assert.IsNotNull (task.GeneratedAssemblies);
			Assert.IsNotEmpty (task.GeneratedAssemblies);

			var assemblyPaths = task.GeneratedAssemblies.Select (i => i.ItemSpec).ToList ();
			Assert.IsTrue (assemblyPaths.Any (p => p.Contains ("_Microsoft.Android.TypeMaps.dll")),
				"Should produce root _Microsoft.Android.TypeMaps.dll");
			Assert.IsTrue (assemblyPaths.Any (p => p.Contains ("_Mono.Android.TypeMap.dll")),
				"Should produce _Mono.Android.TypeMap.dll");

			foreach (var assembly in task.GeneratedAssemblies) {
				FileAssert.Exists (assembly.ItemSpec);
			}
		}

		[Test]
		public void Execute_SecondRun_OutputsAreUpToDate ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			var assemblies = new [] { monoAndroidItem };

			// First run: generates everything
			var task1 = CreateTask (assemblies, outputDir, javaDir);
			Assert.IsTrue (task1.Execute (), "First run should succeed.");

			var typeMapPath = task1.GeneratedAssemblies
				.Select (i => i.ItemSpec)
				.First (p => p.Contains ("_Mono.Android.TypeMap.dll"));
			var firstWriteTime = File.GetLastWriteTimeUtc (typeMapPath);

			// Second run: the persisted model fingerprint should avoid PE emission entirely.
			var messages = new List<BuildMessageEventArgs> ();
			var task2 = CreateTask (assemblies, outputDir, javaDir, messages: messages);
			Assert.IsTrue (task2.Execute (), "Second run should succeed.");

			var secondWriteTime = File.GetLastWriteTimeUtc (typeMapPath);
			Assert.AreEqual (firstWriteTime, secondWriteTime,
				"Typemap assembly should NOT be rewritten when content hasn't changed.");
			Assert.IsTrue (messages.Any (message => message.Message?.Contains ("_Mono.Android.TypeMap: unchanged, skipping emission", StringComparison.Ordinal) == true),
				"Second run should skip typemap PE emission based on the persisted model fingerprint.");
		}

		[Test]
		public void Execute_MissingTypeMapAssembly_RegeneratesWithMatchingFingerprint ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			var assemblies = new [] { monoAndroidItem };
			var task1 = CreateTask (assemblies, outputDir, javaDir);
			Assert.IsTrue (task1.Execute (), "First run should succeed.");

			var typeMapPath = task1.GeneratedAssemblies
				.Select (i => i.ItemSpec)
				.First (p => p.Contains ("_Mono.Android.TypeMap.dll"));
			File.Delete (typeMapPath);

			var messages = new List<BuildMessageEventArgs> ();
			var task2 = CreateTask (assemblies, outputDir, javaDir, messages: messages);
			Assert.IsTrue (task2.Execute (), "Second run should recover the missing typemap assembly.");

			FileAssert.Exists (typeMapPath, "A missing typemap assembly should be regenerated even when its persisted fingerprint matches.");
			Assert.IsTrue (messages.Any (message => message.Message?.Contains ("_Mono.Android.TypeMap: changed, generating", StringComparison.Ordinal) == true),
				"A missing typemap assembly should be reported as changed rather than unchanged.");
		}

		[Test]
		public void ReadTypeMapFingerprints_UnreadableCache_Regenerates ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");
			var fingerprintsFile = Path.Combine (outputDir, "typemap-fingerprints.txt");
			Directory.CreateDirectory (outputDir);
			File.WriteAllText (fingerprintsFile, "_Existing.TypeMap\tfingerprint");

			using var fingerprintsLock = File.Open (fingerprintsFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
			var task = CreateTask ([], outputDir, javaDir);

			Assert.IsEmpty (task.ReadTypeMapFingerprints (), "An unreadable incremental cache should regenerate every typemap assembly.");
		}

		[Test]
		public void ReadTypeMapFingerprints_InvalidCache_Regenerates ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");
			Directory.CreateDirectory (outputDir);
			File.WriteAllText (Path.Combine (outputDir, "typemap-fingerprints.txt"), "invalid");
			var task = CreateTask ([], outputDir, javaDir);

			Assert.IsEmpty (task.ReadTypeMapFingerprints (), "An invalid incremental cache should regenerate every typemap assembly.");
		}

		[Test]
		public void Execute_MissingJavaSource_DoesNotPruneExistingOutput ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaInputDir = Path.Combine (Root, path, "java");
			var javaOutputDir = Path.Combine (Root, path, "linked-java");

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			var firstTask = CreateTask (new [] { monoAndroidItem }, outputDir, javaInputDir);
			Assert.IsTrue (firstTask.Execute (), "First run should generate Java inputs.");
			Assert.IsNotEmpty (firstTask.GeneratedJavaFiles, "Test setup should generate Java sources.");

			var missingInput = firstTask.GeneratedJavaFiles [0].ItemSpec;
			var relativePath = Path.GetRelativePath (javaInputDir, missingInput);
			var existingOutput = Path.Combine (javaOutputDir, relativePath);
			var existingOutputDirectory = Path.GetDirectoryName (existingOutput);
			if (existingOutputDirectory is null) {
				throw new InvalidOperationException ("Could not determine the linked Java output directory.");
			}
			Directory.CreateDirectory (existingOutputDirectory);
			File.Copy (missingInput, existingOutput);
			File.Delete (missingInput);

			var errors = new List<BuildErrorEventArgs> ();
			var secondTask = CreateTask (new [] { monoAndroidItem }, outputDir, javaOutputDir, errors: errors);
			secondTask.JavaSourceInputDirectory = javaInputDir;
			secondTask.GenerateTypeMapAssemblies = false;

			Assert.IsFalse (secondTask.Execute (), "The missing pre-trim Java source should fail with XA4255.");
			Assert.IsTrue (errors.Any (e => e.Code == "XA4255"), "The task should report the missing Java source.");
			FileAssert.Exists (existingOutput, "A failing in-place update should preserve the last known-good linked Java source.");
		}

		[Test]
		public void Execute_WritesGeneratedAssembliesListFile ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");
			var listFile = Path.Combine (outputDir, "typemap-assemblies.txt");
			var staleAssembly = Path.Combine (outputDir, "_Stale.TypeMap.dll");

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			Directory.CreateDirectory (outputDir);
			File.WriteAllText (staleAssembly, "stale");

			var task = CreateTask (new [] { monoAndroidItem }, outputDir, javaDir);
			task.GeneratedAssembliesListFile = listFile;

			Assert.IsTrue (task.Execute (), "Task should succeed.");

			var generatedAssemblies = task.GeneratedAssemblies.Select (i => i.ItemSpec).ToArray ();
			var listedAssemblies = File.ReadAllLines (listFile);
			CollectionAssert.AreEqual (generatedAssemblies, listedAssemblies);
			CollectionAssert.DoesNotContain (listedAssemblies, staleAssembly);
		}

		[Test]
		public void Execute_GeneratesFrameworkJcws ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			var task = CreateTask (new [] { monoAndroidItem }, outputDir, javaDir);
			task.ResolvedFrameworkAssemblies = new [] { monoAndroidItem };

			Assert.IsTrue (task.Execute (), "Task should succeed.");

			var generatedJavaFiles = task.GeneratedJavaFiles.Select (i => i.ItemSpec).ToArray ();
			CollectionAssert.Contains (
				generatedJavaFiles,
				Path.Combine (javaDir, "android/runtime/JavaProxyThrowable.java"));
			CollectionAssert.Contains (
				generatedJavaFiles,
				Path.Combine (javaDir, "xamarin/android/net/ServerCertificateCustomValidator_TrustManager.java"));
			CollectionAssert.Contains (
				generatedJavaFiles,
				Path.Combine (javaDir, "xamarin/android/net/ServerCertificateCustomValidator_TrustManager_FakeSSLSession.java"));
			CollectionAssert.Contains (
				generatedJavaFiles,
				Path.Combine (javaDir, "xamarin/android/net/ServerCertificateCustomValidator_AlwaysAcceptingHostnameVerifier.java"));
			CollectionAssert.DoesNotContain (
				generatedJavaFiles,
				Path.Combine (javaDir, "android/app/Activity.java"));
		}

		[TestCase ("v11.0")]
		[TestCase ("v10.0")]
		[TestCase ("11.0")]
		public void Execute_ParsesTargetFrameworkVersion (string tfv)
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			var task = CreateTask ([], outputDir, javaDir, tfv: tfv);
			Assert.IsTrue (task.Execute (), $"Task should succeed with TargetFrameworkVersion='{tfv}'.");
		}

		[Test]
		public void Execute_ManifestPlaceholdersAreResolvedForRooting ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");
			var manifestTemplate = Path.Combine (Root, path, "AndroidManifest.xml");
			var mergedManifest = Path.Combine (Root, path, "obj", "android", "AndroidManifest.xml");
			var applicationRegistration = Path.Combine (Root, path, "src", "net", "dot", "android", "ApplicationRegistration.java");
			var warnings = new List<BuildWarningEventArgs> ();

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			var manifestDirectory = Path.GetDirectoryName (manifestTemplate);
			if (manifestDirectory is null) {
				Assert.Fail ("Could not determine manifest template directory.");
			}
			Directory.CreateDirectory (manifestDirectory);
			File.WriteAllText (manifestTemplate, """
				<?xml version="1.0" encoding="utf-8"?>
				<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="${applicationId}">
				  <application android:name=".Application" />
				  <instrumentation android:name=".Instrumentation" />
				</manifest>
				""");

			var task = CreateTask (new [] { monoAndroidItem }, outputDir, javaDir, warnings: warnings);
			task.ManifestTemplate = manifestTemplate;
			task.MergedAndroidManifestOutput = mergedManifest;
			task.ApplicationRegistrationOutputFile = applicationRegistration;
			task.PackageName = "android.app";
			task.AndroidApiLevel = "35";
			task.SupportedOSPlatformVersion = "24";
			task.RuntimeProviderJavaName = "mono.MonoRuntimeProvider";
			task.ManifestPlaceholders = "applicationId=android.app";

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			FileAssert.Exists (applicationRegistration);

			var registrationText = File.ReadAllText (applicationRegistration);
			StringAssert.Contains ("mono.android.Runtime.registerNatives (android.app.Application.class);", registrationText);
			StringAssert.Contains ("mono.android.Runtime.registerNatives (android.app.Instrumentation.class);", registrationText);
			StringAssert.DoesNotContain ("android.test.InstrumentationTestRunner.class", registrationText);
			StringAssert.DoesNotContain ("android.test.mock.MockApplication.class", registrationText);
			Assert.IsFalse (warnings.Any (w => w.Code == "XA4250"), "Resolved placeholder-based manifest references should not log XA4250.");
		}

		GenerateTrimmableTypeMap CreateTask (ITaskItem [] assemblies, string outputDir, string javaDir,
			IList<BuildMessageEventArgs>? messages = null, IList<BuildWarningEventArgs>? warnings = null,
			IList<BuildErrorEventArgs>? errors = null, string tfv = "v11.0")
		{
			return new GenerateTrimmableTypeMap {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors: errors, warnings: warnings, messages: messages),
				ResolvedAssemblies = assemblies,
				OutputDirectory = outputDir,
				JavaSourceOutputDirectory = javaDir,
				TargetFrameworkVersion = tfv,
				TypeMapFingerprintsFile = Path.Combine (outputDir, "typemap-fingerprints.txt"),
			};
		}

		static string CreateRegisteredPeerAssembly (string directory)
		{
			var path = Path.Combine (directory, "Fixture.dll");
			using var assembly = Mono.Cecil.AssemblyDefinition.CreateAssembly (
				new Mono.Cecil.AssemblyNameDefinition ("Fixture", new Version (1, 0)),
				"Fixture", Mono.Cecil.ModuleKind.Dll);
			var module = assembly.MainModule;
			var interop = new Mono.Cecil.AssemblyNameReference ("Java.Interop", new Version (1, 0));
			module.AssemblyReferences.Add (interop);
			var attributeType = new Mono.Cecil.TypeReference ("Java.Interop", "JniTypeSignatureAttribute", module, interop);
			var constructor = new Mono.Cecil.MethodReference (".ctor", module.TypeSystem.Void, attributeType) { HasThis = true };
			constructor.Parameters.Add (new Mono.Cecil.ParameterDefinition (module.TypeSystem.String));
			var attribute = new Mono.Cecil.CustomAttribute (constructor);
			attribute.ConstructorArguments.Add (new Mono.Cecil.CustomAttributeArgument (module.TypeSystem.String, "com/example/Peer"));
			var peer = new Mono.Cecil.TypeDefinition ("Example", "Peer",
				Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class, module.TypeSystem.Object);
			peer.CustomAttributes.Add (attribute);
			module.Types.Add (peer);
			assembly.Write (path);
			return path;
		}

		static ITaskItem? FindMonoAndroidDll ()
		{
			var repositoryRoot = Path.GetFullPath (Path.Combine (AppContext.BaseDirectory, "..", "..", ".."));
			var binDirectory = Path.Combine (repositoryRoot, "bin");
			if (!Directory.Exists (binDirectory)) {
				return null;
			}
			var path = Directory.EnumerateFiles (binDirectory, "Mono.Android.dll", SearchOption.AllDirectories)
				.FirstOrDefault (candidate =>
					candidate.Contains ($"{Path.DirectorySeparatorChar}Microsoft.Android.Ref.", StringComparison.Ordinal) &&
					candidate.Contains ($"{Path.DirectorySeparatorChar}ref{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
			if (path is null) {
				return null;
			}
			var item = new TaskItem (path);
			item.SetMetadata ("HasMonoAndroidReference", "True");
			return item;
		}
	}
}
