using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
	[InlineData ("CoreCLR", "true", "disabled", "proguard-android-optimize.txt")]
	[InlineData ("CoreCLR", "true", "private-members", "proguard-android-optimize.txt")]
	[InlineData ("CoreCLR", "false", "disabled", "proguard-android.txt")]
	[InlineData ("CoreCLR", "false", "private-members", "proguard-android-optimize.txt")]
	[InlineData ("NativeAOT", "true", "disabled", "proguard-android.txt")]
	[InlineData ("NativeAOT", "true", "private-members", "proguard-android.txt")]
	[InlineData ("MonoVM", "false", "disabled", "proguard-android.txt")]
	public void PlatformConfigurationSeparatesCoreClrOptimizationFromObfuscation (string runtime, string typemap, string obfuscation, string expected)
	{
		var source = XDocument.Load (Path.Combine (RepositoryDirectory (), "src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets"));
		var target = new XElement (source.Descendants ().Single (element => element.Name.LocalName == "Target" && (string?) element.Attribute ("Name") == "_CalculateProguardConfigurationFiles"));
		foreach (var element in target.DescendantsAndSelf ()) {
			element.Name = element.Name.LocalName;
		}
		var project = Path.Combine (directory, "configuration.proj");
		new XDocument (new XElement ("Project",
			new XElement ("PropertyGroup",
				new XElement ("_AndroidRuntime", runtime),
				new XElement ("_AndroidUseTypeMapProguardConfiguration", typemap),
				new XElement ("AndroidR8ObfuscationMode", obfuscation),
				new XElement ("AndroidLinkTool", "r8"),
				new XElement ("IntermediateOutputPath", "obj/")),
			target,
			new XElement ("Target", new XAttribute ("Name", "Build"), new XAttribute ("DependsOnTargets", "_CalculateProguardConfigurationFiles"),
				new XElement ("WriteLinesToFile", new XAttribute ("File", "$(MSBuildProjectDirectory)/configurations.txt"),
					new XAttribute ("Lines", "@(_ProguardConfiguration)"), new XAttribute ("Overwrite", "true")))))
			.Save (project);
		Build (project);
		var configurations = File.ReadAllLines (Path.Combine (directory, "configurations.txt"));
		Assert.Single (configurations, path => Path.GetFileName (path).StartsWith ("proguard-android", StringComparison.Ordinal));
		Assert.Contains (configurations, path => Path.GetFileName (path) == expected);
		Build (project, "-p:ProguardConfigFiles=custom.cfg");
		Assert.Equal (["custom.cfg"], File.ReadAllLines (Path.Combine (directory, "configurations.txt")));
	}

	[Fact]
	public void MemberGeneratorScopesRulesToCanonicalKeys ()
	{
		var task = new GenerateTypeMapMemberProguardConfiguration {
			BuildEngine = engine,
			TypeMapKeyFiles = [
				new TaskItem (Write ("first.keys", "test/Peer\ntest/Contract\n")),
				new TaskItem (Write ("second.keys", "test/Peer\ntest/Base\n")),
			],
			OutputFile = Path.Combine (directory, "members.cfg"),
		};
		Assert.True (task.Execute ());
		Assert.Equal ("-keepclassmembers class test.Base { *; }\n-keepclassmembers class test.Contract { *; }\n-keepclassmembers class test.Peer { *; }\n",
			File.ReadAllText (task.OutputFile));
	}

	[Fact]
	public void MemberGeneratorRejectsInvalidKeysWithoutOverwritingOutput ()
	{
		var output = Write ("members.cfg", "previous output");
		var task = new GenerateTypeMapMemberProguardConfiguration {
			BuildEngine = engine,
			TypeMapKeyFiles = [new TaskItem (Write ("invalid.keys", "test/Peer\ntest/*\n"))],
			OutputFile = output,
		};
		Assert.False (task.Execute ());
		Assert.Contains (engine.Errors, error => error.Code == "XA4328");
		Assert.Equal ("previous output", File.ReadAllText (output));
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
		var members = Path.Combine (directory, "obj", "proguard", "proguard_typemap_members.cfg");
		Assert.Equal ("-keep class test.Live\n-keep class test.Second\n", File.ReadAllText (rules));
		Assert.Equal ("-keepclassmembers class test.Live { *; }\n-keepclassmembers class test.Second { *; }\n", File.ReadAllText (members));
		var firstTime = File.GetLastWriteTimeUtc (keys);
		var rulesTime = File.GetLastWriteTimeUtc (rules);
		var membersTime = File.GetLastWriteTimeUtc (members);
		Build (project);
		Assert.Equal (firstTime, File.GetLastWriteTimeUtc (keys));
		Assert.Equal (rulesTime, File.GetLastWriteTimeUtc (rules));
		Assert.Equal (membersTime, File.GetLastWriteTimeUtc (members));
		Assert.Contains (members, File.ReadAllText (Path.Combine (directory, "writes.txt")));

		Build (project, "-p:OneAbi=true");
		Assert.Equal ("test/Live\n", File.ReadAllText (keys));
		Assert.Equal ("-keep class test.Live\n", File.ReadAllText (rules));
		Assert.Equal ("-keepclassmembers class test.Live { *; }\n", File.ReadAllText (members));
		File.Delete (members);
		Build (project, "-p:OneAbi=true");
		Assert.Equal ("-keepclassmembers class test.Live { *; }\n", File.ReadAllText (members));
		File.Delete (keys);
		Build (project, "-p:OneAbi=true");
		Assert.Equal ("test/Live\n", File.ReadAllText (keys));
		File.Delete (rules);
		Build (project, "-p:OneAbi=true");
		Assert.Equal ("-keep class test.Live\n", File.ReadAllText (rules));
		Write ("first.ll", "@java_type_names = dso_local local_unnamed_addr constant [13 x i8] c\"test/Changed\\00\", align 1\n");
		Build (project, "-p:OneAbi=true");
		Assert.Equal ("-keep class test.Changed\n", File.ReadAllText (rules));
		Assert.Equal ("-keepclassmembers class test.Changed { *; }\n", File.ReadAllText (members));

		var writes = File.ReadAllText (Path.Combine (directory, "writes.txt"));
		Assert.Contains ("typemap.keys.txt", writes);
		Assert.Contains ("typemap.keys.inputs", writes);
		Assert.Contains ("proguard_project_references.cfg", writes);
		Assert.Contains ("proguard_typemap_members.cfg", writes);
	}

	[Fact]
	public void RelativeTaskAssemblyPathDoesNotInvalidateIncrementalOutputs ()
	{
		Write ("first.ll", "@java_type_names = dso_local local_unnamed_addr constant [10 x i8] c\"test/Live\\00\", align 1\n");
		var project = CreateProject ("CoreCLR", "llvm-ir",
			new XElement ("_TypeMapAssemblySource", new XAttribute ("Include", "$(MSBuildProjectDirectory)/first.ll")));
		var targetsDirectory = Path.Combine (RepositoryDirectory (), "src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets");
		var assembly = Path.GetRelativePath (targetsDirectory, typeof (GenerateTypeMapProguardConfiguration).Assembly.Location);
		var argument = "-p:_MicrosoftAndroidBuildTasksAssembly=" + assembly;
		Build (project, argument);
		var keys = Path.Combine (directory, "obj", "typemap.keys.txt");
		var members = Path.Combine (directory, "obj", "proguard", "proguard_typemap_members.cfg");
		var keysTime = File.GetLastWriteTimeUtc (keys);
		var membersTime = File.GetLastWriteTimeUtc (members);
		Build (project, argument);
		Assert.Equal (keysTime, File.GetLastWriteTimeUtc (keys));
		Assert.Equal (membersTime, File.GetLastWriteTimeUtc (members));
	}

	[NativeAotObjectFact]
	public void NativeObjectTargetUnionsRidsAndHonorsDisabledTrimming ()
	{
		var first = WriteNativeObject ("first", "test/Live", "test/Outer$Inner[0]");
		var second = WriteNativeObject ("second", "test/Second", "test/Outer$Inner[1]");
		Write ("acw-map.txt", "App.Live, App;test.Live\nApp.Second, App;test.Second\nApp.Dead, App;test.Dead\n");
		var project = CreateProject ("NativeAOT", "trimmable",
			NativeObjectItem ("first.so", first),
			NativeObjectItem ("second.so", second));
		Build (project, "-p:_AndroidEnableTypemapR8Trimming=true");
		var rules = Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg");
		Assert.Equal ("-keep class test.Live\n-keep class test.Outer$Inner\n-keep class test.Second\n", File.ReadAllText (rules));
		Assert.False (File.Exists (Path.Combine (directory, "obj", "proguard", "proguard_typemap_members.cfg")));
		Build (project, "-p:_AndroidEnableTypemapR8Trimming=false");
		Assert.Contains ("legacy ACW configuration", File.ReadAllText (rules));
		File.Delete (second);
		var output = Build (project, false, "-p:_AndroidEnableTypemapR8Trimming=true");
		Assert.Contains ("XA4327", output);
	}

	[NativeAotObjectFact]
	public void NativeObjectTargetSelectsJavaGroupsWithoutGuessingFromKeys ()
	{
		var nativeObject = NativeAotObjectTestFixture.WriteObjectGroups (
			directory, "groups", NativeAotObjectIntegrationTools.LlvmReadObjPath,
			("_ZTV43Mono_Android_Android_Runtime_JavaDictionary",
				["System.Collections.Generic.IDictionary`2[System.Char,System.Int32]", "foreign/LooksLikeJava"]),
			("_ZTV29Mono_Android_Java_Lang_Object",
				["test/Live", "[Ljava/lang/Object;", "[I"]));
		var project = CreateProject ("NativeAOT", "trimmable", NativeObjectItem ("app.so", nativeObject));
		Build (project, "-p:_AndroidEnableTypemapR8Trimming=true");
		var rules = Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg");
		Assert.Equal ("-keep class java.lang.Object\n-keep class test.Live\n", File.ReadAllText (rules));

		NativeAotObjectTestFixture.WriteObjectGroups (
			directory, "groups", NativeAotObjectIntegrationTools.LlvmReadObjPath,
			("_ZTV29Mono_Android_Java_Lang_Object", ["test/*"]));
		Assert.Contains ("XA4327", Build (project, false, "-p:_AndroidEnableTypemapR8Trimming=true"));
	}

	[NativeAotObjectFact]
	public void RuntimeSwitchCannotReuseAnotherRepresentation ()
	{
		Write ("first.ll", "@java_type_names = dso_local local_unnamed_addr constant [10 x i8] c\"test/Llvm\\00\", align 1\n");
		var nativeObject = WriteNativeObject ("app", "test/Native");
		var project = CreateProject ("CoreCLR", "llvm-ir",
			new XElement ("_TypeMapAssemblySource", new XAttribute ("Include", "$(MSBuildProjectDirectory)/first.ll")),
			NativeObjectItem ("app.so", nativeObject));
		var rules = Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg");
		var members = Path.Combine (directory, "obj", "proguard", "proguard_typemap_members.cfg");
		Build (project);
		Assert.Equal ("-keep class test.Llvm\n", File.ReadAllText (rules));
		Assert.Contains ("Members=" + members, File.ReadAllText (Path.Combine (directory, "writes.txt")));
		Build (project, "-p:_AndroidRuntime=NativeAOT", "-p:AndroidTypeMapImplementation=trimmable", "-p:_AndroidEnableTypemapR8Trimming=true");
		Assert.Equal ("-keep class test.Native\n", File.ReadAllText (rules));
		Assert.DoesNotContain ("Members=" + members, File.ReadAllText (Path.Combine (directory, "writes.txt")));
		Build (project);
		Assert.Equal ("-keep class test.Llvm\n", File.ReadAllText (rules));
		Assert.Equal ("-keepclassmembers class test.Llvm { *; }\n", File.ReadAllText (members));
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
		Assert.DoesNotContain ("Members=" + Path.Combine (directory, "obj"), File.ReadAllText (Path.Combine (directory, "writes.txt")));
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
		Assert.False (File.Exists (Path.Combine (directory, "obj", "proguard", "proguard_typemap_members.cfg")));
		Assert.DoesNotContain ("UseTypeMap=true", File.ReadAllText (Path.Combine (directory, "writes.txt")));
		if (runtime == "NativeAOT") {
			Assert.Equal ("# Class roots are supplied by the legacy ACW configuration.",
				File.ReadAllText (Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg")).Trim ());
		}
	}

	[NativeAotObjectTheory]
	[InlineData ("CoreCLR", "llvm-ir")]
	[InlineData ("NativeAOT", "trimmable")]
	public void EnablingDisablingAndUnsettingCannotReuseLegacyRules (string runtime, string representation)
	{
		Write ("first.ll", "@java_type_names = dso_local local_unnamed_addr constant [10 x i8] c\"test/Live\\00\", align 1\n");
		var nativeObject = WriteNativeObject ("app", "test/Live");
		Write ("acw-map.txt", "App.Live, App;test.Live\n");
		var project = CreateProject (runtime, representation,
			new XElement ("_TypeMapAssemblySource", new XAttribute ("Include", "$(MSBuildProjectDirectory)/first.ll")),
			NativeObjectItem ("app.so", nativeObject));
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
			if (runtime == "NativeAOT" && enabled == "") {
				Assert.Contains ("legacy ACW configuration", File.ReadAllText (rules));
				Assert.DoesNotContain ("UseTypeMap=true", File.ReadAllText (Path.Combine (directory, "writes.txt")));
			} else {
				Assert.Equal ("-keep class test.Live\n", File.ReadAllText (rules));
				Assert.Contains ("UseTypeMap=true", File.ReadAllText (Path.Combine (directory, "writes.txt")));
			}
		}
	}

	[Theory]
	[InlineData ("false", "false")]
	[InlineData ("false", "true")]
	[InlineData ("true", "false")]
	[InlineData ("true", "true")]
	public void TypemapInputsDoNotRequestGraphsOrChangeParallelism (string enabled, string diagnostics)
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
		var lines = File.ReadAllLines (Path.Combine (directory, "ilc.txt"));
		var output = string.Join ("\n", lines);
		Assert.DoesNotContain ("--scandgmllog:", output);
		Assert.DoesNotContain ("--dgmllog:", output);
		Assert.Contains ("Parallel=", lines);
		Assert.Contains ("Diagnostics=" + diagnostics, output);
	}

	[NativeAotObjectFact]
	public void UnoptimizedNativeAotReadsObjectWithoutGraphs ()
	{
		var nativeObject = WriteNativeObject ("app", "test/Live");
		var project = CreateProject ("NativeAOT", "trimmable",
			NativeObjectItem ("app.so", nativeObject));
		var keys = Path.Combine (directory, "obj", "typemap.keys.txt");
		var rules = Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg");
		Build (project, "-p:Optimize=false", "-p:_AndroidEnableTypemapR8Trimming=true");
		Assert.Equal ("test/Live\n", File.ReadAllText (keys));
		Assert.Equal ("-keep class test.Live\n", File.ReadAllText (rules));
	}

	[Fact]
	public void NativeAotWithoutOptInDoesNotRequireObjectTools ()
	{
		Write ("acw-map.txt", "App.Live, App;test.Live\n");
		var project = CreateProject ("NativeAOT", "trimmable");
		Build (project, "-p:_MicrosoftAndroidBuildTasksAssembly=missing.dll");
		Assert.False (File.Exists (Path.Combine (directory, "obj", "typemap.keys.txt")));
		Assert.DoesNotContain ("UseTypeMap=true", File.ReadAllText (Path.Combine (directory, "writes.txt")));
	}

	[Theory]
	[InlineData ("NativeAOT", "true", "r8", "true", "true")]
	[InlineData ("NativeAOT", "false", "r8", "true", "false")]
	[InlineData ("NativeAOT", "", "r8", "true", "false")]
	[InlineData ("NativeAOT", "true", "", "true", "false")]
	[InlineData ("NativeAOT", "true", "r8", "false", "false")]
	[InlineData ("CoreCLR", "true", "r8", "true", "false")]
	public void NdkDependencyRequiresNativeObjectOptIn (string runtime, string enabled, string linkTool, string trimmed, string expected)
	{
		var common = XDocument.Load (Path.Combine (RepositoryDirectory (), "src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets"));
		XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
		var dependencyProperties = common.Root?.Elements (ns + "Target")
			.Single (target => (string?) target.Attribute ("Name") == "GetAndroidDependencies")
			.Element (ns + "PropertyGroup") ?? throw new InvalidOperationException ();
		var path = Path.Combine (directory, "dependencies.proj");
		new XDocument (new XElement (ns + "Project",
			new XElement (ns + "PropertyGroup",
				new XElement (ns + "_AndroidRuntime", runtime),
				new XElement (ns + "AndroidTypeMapImplementation", "trimmable"),
				new XElement (ns + "_AndroidEnableTypemapR8Trimming", enabled),
				new XElement (ns + "_AndroidUseWorkloadNativeLinker", "true"),
				new XElement (ns + "PublishAot", "true"),
				new XElement (ns + "PublishTrimmed", trimmed),
				new XElement (ns + "AndroidLinkTool", linkTool)),
			new XElement (ns + "Target", new XAttribute ("Name", "Build"),
				new XElement (dependencyProperties),
				new XElement (ns + "WriteLinesToFile", new XAttribute ("File", "$(MSBuildProjectDirectory)/ndk-required.txt"),
					new XAttribute ("Lines", "$(_NdkRequired)"), new XAttribute ("Overwrite", "true")))))
			.Save (path);
		Build (path);
		Assert.Equal (expected, File.ReadAllText (Path.Combine (directory, "ndk-required.txt")).Trim ());
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
	[InlineData ("NativeAOT", "true", "custom-object/retained.o")]
	[InlineData ("NativeAOT", "false", "custom-object/retained.o")]
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
			new XElement ("NativeObject", "$(MSBuildProjectDirectory)/custom-object/retained.o"),
			new XElement ("_NdkBinDir", "$(MSBuildProjectDirectory)/custom-ndk-bin/"),
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
		var metadata = runtime == "NativeAOT" ? "AndroidTypeMapNativeObject" : "AndroidTypeMapLinkedAssemblies";
		root.Add (new XElement ("Target", new XAttribute ("Name", "Build"),
			new XAttribute ("DependsOnTargets", "_ComputeFilesToPublishForRuntimeIdentifiers"),
			new XElement ("WriteLinesToFile", new XAttribute ("File", "$(MSBuildProjectDirectory)/producer.txt"),
				new XAttribute ("Lines", $"@(ResolvedFileToPublish->'%({metadata})')"),
				new XAttribute ("Overwrite", "true")),
			new XElement ("WriteLinesToFile", new XAttribute ("File", "$(MSBuildProjectDirectory)/readobj.txt"),
				new XAttribute ("Lines", "@(ResolvedFileToPublish->'%(AndroidTypeMapLlvmReadObjPath)')"),
				new XAttribute ("Overwrite", "true"))));
		document.Save (project);
		Build (project);
		Assert.Equal (Path.Combine (directory, expected).Replace ('\\', '/'),
			File.ReadAllText (Path.Combine (directory, "producer.txt")).Trim ().Replace ('\\', '/'));
		if (runtime == "NativeAOT") {
			var executable = OperatingSystem.IsWindows () ? "llvm-readobj.exe" : "llvm-readobj";
			Assert.Equal (Path.Combine (directory, "custom-ndk-bin", executable).Replace ('\\', '/'),
				File.ReadAllText (Path.Combine (directory, "readobj.txt")).Trim ().Replace ('\\', '/'));
		}
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
						new XAttribute ("Lines", "@(FileWrites);UseTypeMap=$(_AndroidUseTypeMapProguardConfiguration);@(_ProguardConfiguration->'Members=%(Identity)')"), new XAttribute ("Overwrite", "true")))))
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
		start.Environment ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
		foreach (var argument in new [] { "msbuild", project, "-t:Build", "-nologo", "-v:minimal", "-nr:false" }) {
			start.ArgumentList.Add (argument);
		}
		foreach (var argument in arguments) {
			start.ArgumentList.Add (argument);
		}
		using var process = Process.Start (start) ?? throw new InvalidOperationException ("Could not start MSBuild.");
		var stdout = process.StandardOutput.ReadToEndAsync ();
		var stderr = process.StandardError.ReadToEndAsync ();
		bool completed = process.WaitForExit (120000);
		if (!completed) {
			process.Kill (entireProcessTree: true);
			process.WaitForExit ();
		}
		var output = stdout.GetAwaiter ().GetResult () + stderr.GetAwaiter ().GetResult ();
		Assert.True (completed, "MSBuild did not finish." + Environment.NewLine + output);
		Assert.True (expectSuccess ? process.ExitCode == 0 : process.ExitCode != 0, output);
		return output;
	}

	string Write (string name, string content)
	{
		var path = Path.Combine (directory, name);
		File.WriteAllText (path, content, new UTF8Encoding (false));
		return path;
	}

	string WriteNativeObject (string name, params string [] keys) =>
		NativeAotObjectTestFixture.WriteObject (directory, name, NativeAotObjectIntegrationTools.LlvmReadObjPath, keys);

	static XElement NativeObjectItem (string output, string nativeObject) =>
		new ("ResolvedFileToPublish", new XAttribute ("Include", output),
			new XAttribute ("AndroidTypeMapNativeObject", nativeObject),
			new XAttribute ("AndroidTypeMapLlvmReadObjPath", NativeAotObjectIntegrationTools.LlvmReadObjPath),
			new XAttribute ("AndroidTypeMapLlvmObjDumpPath", NativeAotObjectIntegrationTools.LlvmObjDumpPath));

	public void Dispose () => Directory.Delete (directory, recursive: true);
}
