using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Tasks;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class TypeMapProguardTests : IDisposable
{
	readonly string directory = Path.Combine (Path.GetTempPath (), "typemap-proguard-" + Guid.NewGuid ().ToString ("N"));
	readonly TypeMapTaskBuildEngine engine = new ();

	public TypeMapProguardTests () => Directory.CreateDirectory (directory);

	[Fact]
	public void GeneratorUnionsCanonicalKeysAndWritesOnlyClassRules ()
	{
		var first = Write ("first.keys", "test/Outer$Inner\r\nandroid/app/Activity\r\n\r\ntest/Caf\u00e9\n");
		var second = Write ("second.keys", "android/app/Activity\ntest/\U00010428Peer\n");
		var task = CreateGenerator (first, second);
		Assert.True (task.Execute ());
		var expected = "-keep class android.app.Activity\n-keep class test.Caf\u00e9\n-keep class test.Outer$Inner\n-keep class test.\U00010428Peer\n";
		Assert.Equal (new UTF8Encoding (false).GetBytes (expected), File.ReadAllBytes (task.OutputFile));
	}

	[Theory]
	[InlineData ("test/*")]
	[InlineData ("test/Foo { *; }")]
	[InlineData ("test/Foo[1]")]
	[InlineData ("test.Foo")]
	[InlineData ("test//Foo")]
	[InlineData ("/test/Foo")]
	[InlineData ("test/Foo/")]
	[InlineData ("test/Foo;")]
	[InlineData ("test/Foo\n-keep class **")]
	[InlineData ("\ufefftest/Foo")]
	public void GeneratorRejectsRuleInjectionAndNoncanonicalKeys (string input)
	{
		var task = CreateGenerator (Write ("invalid.keys", input));
		Assert.False (task.Execute ());
		Assert.Contains (engine.Errors, error => error.Code == "XA4328");
		Assert.False (File.Exists (task.OutputFile));
	}

	[Fact]
	public void EmptyMapOverwritesAndRefreshesRealOutput ()
	{
		var task = CreateGenerator (Write ("empty.keys", ""));
		Assert.True (task.Execute ());
		var old = DateTime.UtcNow.AddDays (-1);
		File.SetLastWriteTimeUtc (task.OutputFile, old);
		Assert.True (task.Execute ());
		Assert.Empty (File.ReadAllBytes (task.OutputFile));
		Assert.True (File.GetLastWriteTimeUtc (task.OutputFile) > old);
	}

	[Fact]
	public void MissingMapIsAnError ()
	{
		var task = CreateGenerator (Path.Combine (directory, "missing.keys"));
		Assert.False (task.Execute ());
		Assert.Contains (engine.Errors, error => error.Code == "XA4328");
	}

	[Fact]
	public void LlvmTargetTracksAbiUnionInputListAndDeletedOutputs ()
	{
		Write ("first.ll", "@java_type_names = dso_local local_unnamed_addr constant [10 x i8] c\"test/Live\\00\", align 1\n");
		Write ("second.ll", "@java_type_names = dso_local local_unnamed_addr constant [12 x i8] c\"test/Second\\00\", align 1\n");
		var project = CreateProject ("CoreCLR", "llvm-ir",
			new XElement ("_TypeMapAssemblySource", new XAttribute ("Include", "$(MSBuildProjectDirectory)/first.ll")),
			new XElement ("_TypeMapAssemblySource", new XAttribute ("Include", "$(MSBuildProjectDirectory)/second.ll"),
				new XAttribute ("Condition", "'$(OneAbi)' != 'true'")));
		Build (project);
		var keys = Path.Combine (directory, "obj", "typemap.keys.txt");
		var rules = Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg");
		Assert.Equal ("-keep class test.Live\n-keep class test.Second\n", File.ReadAllText (rules));
		var firstTime = File.GetLastWriteTimeUtc (keys);
		var rulesTime = File.GetLastWriteTimeUtc (rules);
		Build (project);
		Assert.Equal (firstTime, File.GetLastWriteTimeUtc (keys));
		Assert.Equal (rulesTime, File.GetLastWriteTimeUtc (rules));

		Build (project, "-p:OneAbi=true");
		Assert.Equal ("test/Live\n", File.ReadAllText (keys));
		Assert.Equal ("-keep class test.Live\n", File.ReadAllText (rules));
		File.Delete (keys);
		Build (project, "-p:OneAbi=true");
		Assert.Equal ("test/Live\n", File.ReadAllText (keys));
		File.Delete (rules);
		Build (project, "-p:OneAbi=true");
		Assert.Equal ("-keep class test.Live\n", File.ReadAllText (rules));
		Write ("first.ll", "@java_type_names = dso_local local_unnamed_addr constant [13 x i8] c\"test/Changed\\00\", align 1\n");
		Build (project, "-p:OneAbi=true");
		Assert.Equal ("-keep class test.Changed\n", File.ReadAllText (rules));

		var writes = File.ReadAllText (Path.Combine (directory, "writes.txt"));
		Assert.Contains ("typemap.keys.txt", writes);
		Assert.Contains ("typemap.keys.inputs", writes);
		Assert.Contains ("proguard_project_references.cfg", writes);
	}

	[Fact]
	public void DgmlTargetUnionsRequestedGraphsAndHonorsDisabledTrimming ()
	{
		Write ("first.dgml", """<DirectedGraph><Nodes><Node Id="1" Label="Type metadata: [App]App.Live" /></Nodes></DirectedGraph>""");
		Write ("second.dgml", """<DirectedGraph><Nodes><Node Id="2" Label="Type metadata: [App]App.Second" /></Nodes></DirectedGraph>""");
		Write ("acw-map.txt", "App.Live, App;test.Live\nApp.Second, App;test.Second\nApp.Dead, App;test.Dead\n");
		var project = CreateProject ("NativeAOT", "trimmable",
			new XElement ("ResolvedFileToPublish", new XAttribute ("Include", "first.so"),
				new XAttribute ("AndroidTypeMapDgmlFile", "$(MSBuildProjectDirectory)/first.dgml")),
			new XElement ("ResolvedFileToPublish", new XAttribute ("Include", "second.so"),
				new XAttribute ("AndroidTypeMapDgmlFile", "$(MSBuildProjectDirectory)/second.dgml")));
		Build (project);
		var rules = Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg");
		Assert.Equal ("-keep class test.Live\n-keep class test.Second\n", File.ReadAllText (rules));
		Build (project, "-p:_AndroidEnableTypemapR8Trimming=false");
		Assert.Contains ("legacy ACW configuration", File.ReadAllText (rules));
		File.Delete (Path.Combine (directory, "second.dgml"));
		var output = Build (project, expectSuccess: false);
		Assert.Contains ("XA4321", output);
	}

	[Fact]
	public void RuntimeSwitchCannotReuseAnotherRepresentation ()
	{
		Write ("first.ll", "@java_type_names = dso_local local_unnamed_addr constant [10 x i8] c\"test/Llvm\\00\", align 1\n");
		Write ("app.dgml", """<DirectedGraph><Nodes><Node Id="1" Label="Type metadata: [App]App.Live" /></Nodes></DirectedGraph>""");
		Write ("acw-map.txt", "App.Live, App;test.Native\n");
		var project = CreateProject ("CoreCLR", "llvm-ir",
			new XElement ("_TypeMapAssemblySource", new XAttribute ("Include", "$(MSBuildProjectDirectory)/first.ll")),
			new XElement ("ResolvedFileToPublish", new XAttribute ("Include", "app.so"),
				new XAttribute ("AndroidTypeMapDgmlFile", "$(MSBuildProjectDirectory)/app.dgml")));
		var rules = Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg");
		Build (project);
		Assert.Equal ("-keep class test.Llvm\n", File.ReadAllText (rules));
		Build (project, "-p:_AndroidRuntime=NativeAOT", "-p:AndroidTypeMapImplementation=trimmable");
		Assert.Equal ("-keep class test.Native\n", File.ReadAllText (rules));
		Build (project);
		Assert.Equal ("-keep class test.Llvm\n", File.ReadAllText (rules));
	}

	[Fact]
	public void CompleteConfigurationOverridePreservesLegacyR8Policy ()
	{
		Write ("first.ll", "@java_type_names = dso_local local_unnamed_addr constant [10 x i8] c\"test/Live\\00\", align 1\n");
		var project = CreateProject ("CoreCLR", "llvm-ir",
			new XElement ("_TypeMapAssemblySource", new XAttribute ("Include", "$(MSBuildProjectDirectory)/first.ll")));
		Build (project);
		Assert.Contains ("UseTypeMap=true", File.ReadAllText (Path.Combine (directory, "writes.txt")));
		Build (project, "-p:ProguardConfigFiles=custom.cfg");
		Assert.DoesNotContain ("UseTypeMap=true", File.ReadAllText (Path.Combine (directory, "writes.txt")));
	}

	[Theory]
	[InlineData ("CoreCLR", "llvm-ir")]
	[InlineData ("CoreCLR", "trimmable")]
	[InlineData ("NativeAOT", "trimmable")]
	public void DisabledPipelineNeedsNoTypemapInputsOrModernTaskAssembly (string runtime, string representation)
	{
		Write ("acw-map.txt", "App.Live, App;test.Live\n");
		var project = CreateProject (runtime, representation);
		Build (project, "-p:_AndroidEnableTypemapR8Trimming=false", "-p:_MicrosoftAndroidBuildTasksAssembly=missing.dll");
		Assert.False (File.Exists (Path.Combine (directory, "obj", "typemap.keys.txt")));
		Assert.DoesNotContain ("UseTypeMap=true", File.ReadAllText (Path.Combine (directory, "writes.txt")));
		if (runtime == "NativeAOT") {
			Assert.Equal ("# Class roots are supplied by the legacy ACW configuration.",
				File.ReadAllText (Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg")).Trim ());
		}
	}

	[Theory]
	[InlineData ("CoreCLR", "llvm-ir")]
	[InlineData ("NativeAOT", "trimmable")]
	public void EnablingDisablingAndUnsettingCannotReuseLegacyRules (string runtime, string representation)
	{
		Write ("first.ll", "@java_type_names = dso_local local_unnamed_addr constant [10 x i8] c\"test/Live\\00\", align 1\n");
		Write ("app.dgml", """<DirectedGraph><Nodes><Node Id="1" Label="Type metadata: [App]App.Live" /></Nodes></DirectedGraph>""");
		Write ("acw-map.txt", "App.Live, App;test.Live\n");
		var project = CreateProject (runtime, representation,
			new XElement ("_TypeMapAssemblySource", new XAttribute ("Include", "$(MSBuildProjectDirectory)/first.ll")),
			new XElement ("ResolvedFileToPublish", new XAttribute ("Include", "app.so"),
				new XAttribute ("AndroidTypeMapDgmlFile", "$(MSBuildProjectDirectory)/app.dgml")));
		var keys = Path.Combine (directory, "obj", "typemap.keys.txt");
		var rules = Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg");
		foreach (var enabled in new [] { "true", "" }) {
			Build (project, "-p:_AndroidEnableTypemapR8Trimming=true");
			var keysTime = File.GetLastWriteTimeUtc (keys);
			Build (project, "-p:_AndroidEnableTypemapR8Trimming=false");
			Assert.Equal (keysTime, File.GetLastWriteTimeUtc (keys));
			Assert.DoesNotContain ("UseTypeMap=true", File.ReadAllText (Path.Combine (directory, "writes.txt")));
			// The CoreCLR legacy producer runs in the RID inner build, outside this fixture.
			if (runtime == "CoreCLR") {
				File.WriteAllText (rules, "# legacy rules");
			}
			Assert.DoesNotContain ("-keep class test.Live", File.ReadAllText (rules));
			Build (project, "-p:_AndroidEnableTypemapR8Trimming=" + enabled);
			Assert.Equal ("-keep class test.Live\n", File.ReadAllText (rules));
			Assert.Contains ("UseTypeMap=true", File.ReadAllText (Path.Combine (directory, "writes.txt")));
		}
	}

	[Theory]
	[InlineData ("false", "false", false)]
	[InlineData ("false", "true", true)]
	[InlineData ("true", "false", true)]
	public void DisablingAutomaticDgmlPreservesExplicitDiagnostics (string enabled, string diagnostics, bool serialBuild)
	{
		var project = CreateProject ("NativeAOT", "trimmable");
		var document = XDocument.Load (project);
		var root = document.Root ?? throw new InvalidOperationException ();
		root.Add (new XElement ("Import", new XAttribute ("Project",
			Path.Combine (RepositoryDirectory (), "src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.TypeMap.Trimmable.NativeAOT.targets"))));
		root.Add (new XElement ("Target", new XAttribute ("Name", "_ReadGeneratedTrimmableTypeMapAssemblies")));
		root.Add (new XElement ("Target", new XAttribute ("Name", "Build"),
			new XAttribute ("DependsOnTargets", "_AddTrimmableTypeMapAssembliesToIlc"),
			new XElement ("WriteLinesToFile", new XAttribute ("File", "$(MSBuildProjectDirectory)/ilc.txt"),
				new XAttribute ("Lines", "@(IlcArg);Diagnostics=$(IlcGenerateDgmlFile);Parallel=$(_AndroidBuildRuntimeIdentifiersInParallel)"),
				new XAttribute ("Overwrite", "true"))));
		document.Save (project);
		Build (project, "-p:_AndroidEnableTypemapR8Trimming=" + enabled, "-p:IlcGenerateDgmlFile=" + diagnostics, "-p:Optimize=true");
		var output = File.ReadAllText (Path.Combine (directory, "ilc.txt"));
		Assert.Equal (enabled == "true" && diagnostics == "false", output.Contains ("--scandgmllog:", StringComparison.Ordinal));
		Assert.Equal (serialBuild, output.Contains ("Parallel=false", StringComparison.Ordinal));
		Assert.Contains ("Diagnostics=" + diagnostics, output);
	}

	[Fact]
	public void UnoptimizedNativeAotRequiresExplicitPipelineOptIn ()
	{
		Write ("app.dgml", """<DirectedGraph><Nodes><Node Id="1" Label="Type metadata: [App]App.Live" /></Nodes></DirectedGraph>""");
		Write ("acw-map.txt", "App.Live, App;test.Live\nApp.Dead, App;test.Dead\n");
		var project = CreateProject ("NativeAOT", "trimmable",
			new XElement ("ResolvedFileToPublish", new XAttribute ("Include", "app.so"),
				new XAttribute ("AndroidTypeMapDgmlFile", "$(MSBuildProjectDirectory)/app.dgml")));
		var keys = Path.Combine (directory, "obj", "typemap.keys.txt");
		var rules = Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg");
		Build (project, "-p:Optimize=false");
		Assert.False (File.Exists (keys));
		Assert.Contains ("legacy ACW configuration", File.ReadAllText (rules));
		Build (project, "-p:Optimize=false", "-p:_AndroidEnableTypemapR8Trimming=true");
		Assert.Equal ("-keep class test.Live\n", File.ReadAllText (rules));
		Build (project, "-p:Optimize=false");
		Assert.Contains ("legacy ACW configuration", File.ReadAllText (rules));
	}

	[Fact]
	public void InnerBuildDoesNotGenerateOuterClassRules ()
	{
		var project = CreateProject ("CoreCLR", "trimmable");
		Build (project, "-p:_ComputeFilesToPublishForRuntimeIdentifiers=true");
		Assert.False (File.Exists (Path.Combine (directory, "obj", "typemap.keys.txt")));
	}

	[Fact]
	public void AssemblyTargetConsumesLinkedMetadataAcrossRidsAndEmptyStubs ()
	{
		string EmitMap (string name, params string [] keys)
		{
			var path = Path.Combine (directory, name + ".dll");
			var model = new TypeMapAssemblyData { AssemblyName = name, ModuleName = name + ".dll" };
			foreach (var key in keys) {
				model.Entries.Add (new TypeMapAttributeData {
					MapKey = key, ProxyTypeReference = "System.Object, System.Runtime",
				});
			}
			using var stream = File.Create (path);
			new TypeMapAssemblyEmitter (new Version (11, 0, 0, 0)).Emit (model, stream);
			return path;
		}
		var first = EmitMap ("first", "test/Live", "test/Alias[0]");
		var second = EmitMap ("second", "test/Second", "test/Alias[1]");
		var stub = EmitMap ("stub");
		var project = CreateProject ("CoreCLR", "trimmable",
			new XElement ("ResolvedFileToPublish", new XAttribute ("Include", "arm64/R2R/root.dll"),
				new XAttribute ("AndroidTypeMapLinkedAssemblies", first + ";" + stub)),
			new XElement ("ResolvedFileToPublish", new XAttribute ("Include", "x64/R2R/root.dll"),
				new XAttribute ("AndroidTypeMapLinkedAssemblies", second)),
			new XElement ("ResolvedFileToPublish", new XAttribute ("Include", "pretrim/unused.dll")));
		Build (project);
		var rules = Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg");
		Assert.Equal ("-keep class test.Alias\n-keep class test.Live\n-keep class test.Second\n", File.ReadAllText (rules));
		File.Delete (second);
		Assert.Contains ("XA4327", Build (project, expectSuccess: false));
	}

	[Theory]
	[InlineData ("NativeAOT", "true", "custom-native/App.scan.dgml.xml")]
	[InlineData ("NativeAOT", "false", "custom-native/App.codegen.dgml.xml")]
	[InlineData ("CoreCLR", "true", "obj/linked/_Binding.TypeMap.dll")]
	public void InnerBuildReturnsExactProducerPaths (string runtime, string optimize, string expected)
	{
		var project = CreateProject (runtime, "trimmable");
		var document = XDocument.Load (project);
		var root = document.Root ?? throw new InvalidOperationException ();
		root.AddFirst (new XElement ("Import", new XAttribute ("Project",
			Path.Combine (RepositoryDirectory (), "src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.AssemblyResolution.targets"))));
		root.Add (new XElement ("PropertyGroup",
			new XElement ("RuntimeIdentifier", "android-x64"),
			new XElement ("NativeIntermediateOutputPath", "$(MSBuildProjectDirectory)/custom-native/"),
			new XElement ("TargetName", "App"),
			new XElement ("Optimize", optimize),
			new XElement ("_AndroidEnableTypemapR8Trimming", "true"),
			new XElement ("_TypeMapAssemblyName", "_Microsoft.Android.TypeMaps")));
		foreach (var target in new [] { "BuildOnlySettings", "_CheckForInvalidConfigurationAndPlatform", "_FixupIntermediateAssembly", "_PatchNuGetReferenceMetadata", "ResolveReferences", "_AndroidAot" }) {
			root.Add (new XElement ("Target", new XAttribute ("Name", target)));
		}
		root.Add (new XElement ("Target", new XAttribute ("Name", "ComputeFilesToPublish"),
			new XElement ("ItemGroup",
				new XElement ("_GeneratedTypeMapAssembliesFromList", new XAttribute ("Include", "generated/_Binding.TypeMap.dll")),
				new XElement ("ResolvedFileToPublish", new XAttribute ("Include", "R2R/_Microsoft.Android.TypeMaps.dll")))));
		var metadata = runtime == "NativeAOT" ? "AndroidTypeMapDgmlFile" : "AndroidTypeMapLinkedAssemblies";
		root.Add (new XElement ("Target", new XAttribute ("Name", "Build"),
			new XAttribute ("DependsOnTargets", "_ComputeFilesToPublishForRuntimeIdentifiers"),
			new XElement ("WriteLinesToFile", new XAttribute ("File", "$(MSBuildProjectDirectory)/producer.txt"),
				new XAttribute ("Lines", $"@(ResolvedFileToPublish->'%({metadata})')"),
				new XAttribute ("Overwrite", "true"))));
		document.Save (project);
		Build (project);
		Assert.Equal (Path.Combine (directory, expected).Replace ('\\', '/'),
			File.ReadAllText (Path.Combine (directory, "producer.txt")).Trim ().Replace ('\\', '/'));
	}

	[Theory]
	[InlineData ("MonoVM", "llvm-ir", "true", "r8")]
	[InlineData ("CoreCLR", "llvm-ir", "false", "r8")]
	[InlineData ("CoreCLR", "llvm-ir", "true", "")]
	public void InactivePathsDoNotConsumeOrGenerateKeys (string runtime, string representation, string trimmed, string linkTool)
	{
		var project = CreateProject (runtime, representation);
		Build (project, $"-p:PublishTrimmed={trimmed}", $"-p:AndroidLinkTool={linkTool}", "-p:_AndroidEnableTypemapR8Trimming=true");
		Assert.False (File.Exists (Path.Combine (directory, "obj", "typemap.keys.txt")));
	}

	GenerateTypeMapProguardConfiguration CreateGenerator (params string [] inputs)
	{
		var items = new ITaskItem [inputs.Length];
		for (int i = 0; i < inputs.Length; i++) {
			items [i] = new TaskItem (inputs [i]);
		}
		return new GenerateTypeMapProguardConfiguration {
			BuildEngine = engine,
			TypeMapKeyFiles = items,
			OutputFile = Path.Combine (directory, "rules", "classes.cfg"),
		};
	}

	string CreateProject (string runtime, string representation, params XElement [] sourceItems)
	{
		var targets = Path.Combine (RepositoryDirectory (), "src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.TypeMap.Proguard.targets");
		var path = Path.Combine (directory, "pipeline.proj");
		Directory.CreateDirectory (Path.Combine (directory, "obj"));
		new XDocument (
			new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("_MicrosoftAndroidBuildTasksAssembly", typeof (GenerateTypeMapProguardConfiguration).Assembly.Location),
					new XElement ("_XamarinAndroidBuildTasksAssembly", "unused-legacy-tasks.dll"),
					new XElement ("_AndroidRuntime", runtime),
					new XElement ("AndroidTypeMapImplementation", representation),
					new XElement ("PublishTrimmed", "true"),
					new XElement ("AndroidLinkTool", "r8"),
					new XElement ("Optimize", "true"),
					new XElement ("IntermediateOutputPath", "$(MSBuildProjectDirectory)/obj/"),
					new XElement ("_AndroidBuildPropertiesCache", "$(MSBuildProjectDirectory)/obj/build.props.cache"),
					new XElement ("_AcwMapFile", "$(MSBuildProjectDirectory)/acw-map.txt")),
				new XElement ("Import", new XAttribute ("Project", targets)),
				new XElement ("Target", new XAttribute ("Name", "_GenerateJavaStubs"),
					new XElement ("ItemGroup", sourceItems)),
				new XElement ("Target", new XAttribute ("Name", "_CalculateProguardConfigurationFiles")),
				new XElement ("Target", new XAttribute ("Name", "_CreatePropertiesCache"),
					new XElement ("WriteLinesToFile", new XAttribute ("File", "$(_AndroidBuildPropertiesCache)"),
						new XAttribute ("Lines", "Enabled=$(_AndroidEnableTypemapR8Trimming)"),
						new XAttribute ("Overwrite", "true"), new XAttribute ("WriteOnlyWhenDifferent", "true"))),
				new XElement ("Target", new XAttribute ("Name", "Build"),
					new XAttribute ("DependsOnTargets", "_CreatePropertiesCache;_CalculateProguardConfigurationFiles;_AndroidGenerateTypeMapProguardConfiguration"),
					new XElement ("WriteLinesToFile", new XAttribute ("File", "$(MSBuildProjectDirectory)/writes.txt"),
						new XAttribute ("Lines", "@(FileWrites);UseTypeMap=$(_AndroidUseTypeMapProguardConfiguration)"), new XAttribute ("Overwrite", "true")))))
			.Save (path);
		return path;
	}

	static string RepositoryDirectory ()
	{
		var repository = new DirectoryInfo (AppContext.BaseDirectory);
		while (repository != null && !File.Exists (Path.Combine (repository.FullName, "Configuration.props"))) {
			repository = repository.Parent;
		}
		Assert.NotNull (repository);
		return repository.FullName;
	}

	string Build (string project, params string [] arguments) => Build (project, true, arguments);

	string Build (string project, bool expectSuccess, params string [] arguments)
	{
		var start = new ProcessStartInfo ("dotnet") {
			WorkingDirectory = directory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		foreach (var argument in new [] { "msbuild", project, "-t:Build", "-nologo", "-v:minimal", "-nr:false" }) {
			start.ArgumentList.Add (argument);
		}
		foreach (var argument in arguments) {
			start.ArgumentList.Add (argument);
		}
		using var process = Process.Start (start) ?? throw new InvalidOperationException ("Could not start MSBuild.");
		var stdout = process.StandardOutput.ReadToEndAsync ();
		var stderr = process.StandardError.ReadToEndAsync ();
		Assert.True (process.WaitForExit (120000), "MSBuild did not finish.");
		var output = stdout.GetAwaiter ().GetResult () + stderr.GetAwaiter ().GetResult ();
		Assert.True (expectSuccess ? process.ExitCode == 0 : process.ExitCode != 0, output);
		return output;
	}

	string Write (string name, string content)
	{
		var path = Path.Combine (directory, name);
		File.WriteAllText (path, content, new UTF8Encoding (false));
		return path;
	}

	public void Dispose () => Directory.Delete (directory, recursive: true);
}
