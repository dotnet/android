using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Xml.Linq;
using Microsoft.Android.Sdk.TrimmableTypeMap;
using Microsoft.Android.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable (ParallelScope.Children)]
public class TypeMapProguardTargetsTests : BaseTest
{
	string directory => Path.Combine (Root, "temp", TestName);

	[SetUp]
	public void SetUp () => Directory.CreateDirectory (directory);

	[TestCase ("CoreCLR", "true", "disabled", "proguard-android-optimize.txt")]
	[TestCase ("CoreCLR", "true", "private-members", "proguard-android-optimize.txt")]
	[TestCase ("CoreCLR", "false", "disabled", "proguard-android.txt")]
	[TestCase ("CoreCLR", "false", "private-members", "proguard-android-optimize.txt")]
	[TestCase ("NativeAOT", "true", "disabled", "proguard-android.txt")]
	[TestCase ("NativeAOT", "true", "private-members", "proguard-android.txt")]
	[TestCase ("MonoVM", "false", "disabled", "proguard-android.txt")]
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
				new XElement ("IntermediateOutputPath", "obj/"),
				new XElement ("_ProguardProjectConfiguration", "obj/proguard/proguard_project_references.cfg")),
			target,
			new XElement ("Target", new XAttribute ("Name", "Build"), new XAttribute ("DependsOnTargets", "_CalculateProguardConfigurationFiles"),
				new XElement ("WriteLinesToFile", new XAttribute ("File", "$(MSBuildProjectDirectory)/configurations.txt"),
					new XAttribute ("Lines", "@(_ProguardConfiguration)"), new XAttribute ("Overwrite", "true")))))
			.Save (project);
		Build (project);
		var configurations = File.ReadAllLines (Path.Combine (directory, "configurations.txt"));
		Assert.AreEqual (1, configurations.Count (path => Path.GetFileName (path).StartsWith ("proguard-android", StringComparison.Ordinal)));
		Assert.IsTrue (configurations.Any (path => Path.GetFileName (path) == expected));
		var generatedReferenceConfiguration = Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg");
		Directory.CreateDirectory (Path.GetDirectoryName (generatedReferenceConfiguration) ?? throw new InvalidOperationException ());
		File.WriteAllText (generatedReferenceConfiguration, "rules");
		Build (project, "-p:ProguardConfigFiles=custom.cfg");
		CollectionAssert.AreEqual (new [] {
			"custom.cfg",
			Path.Combine ("obj", "proguard", "proguard_xamarin.cfg"),
			Path.Combine ("obj", "proguard", "proguard_project_references.cfg"),
			Path.Combine ("obj", "proguard", "proguard_project_primary.cfg"),
		}, File.ReadAllLines (Path.Combine (directory, "configurations.txt")));
		File.Delete (generatedReferenceConfiguration);
		Build (project, "-p:ProguardConfigFiles=custom.cfg");
		CollectionAssert.AreEqual (new [] {
			"custom.cfg",
			Path.Combine ("obj", "proguard", "proguard_xamarin.cfg"),
			Path.Combine ("obj", "proguard", "proguard_project_primary.cfg"),
		}, File.ReadAllLines (Path.Combine (directory, "configurations.txt")));
	}

	[Test]
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
		Assert.AreEqual (TypeRules ("test.Live", "test.Outer$Inner", "test.Second"), File.ReadAllText (rules));
		Assert.IsFalse (File.Exists (Path.Combine (directory, "obj", "proguard", "proguard_typemap_members.cfg")));
		Build (project, "-p:_AndroidEnableTypemapR8Trimming=false");
		StringAssert.Contains ("legacy ACW configuration", File.ReadAllText (rules));
		File.Delete (second);
		var output = Build (project, false, "-p:_AndroidEnableTypemapR8Trimming=true");
		StringAssert.Contains ("XA4327", output);
	}

	[Test]
	public void NativeAotModeRoundTripsCannotReuseWrongRules ()
	{
		var nativeObject = WriteNativeObject ("app", "test/Live");
		Write ("acw-map.txt", "App.Live, App;test.Live\n");
		var project = CreateProject ("NativeAOT", "trimmable",
			NativeObjectItem ("app.so", nativeObject));
		var keys = Path.Combine (directory, "obj", "typemap.keys.txt");
		var rules = Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg");
		Build (project, "-p:_AndroidEnableTypemapR8Trimming=false");
		AssertLegacyRules ();
		Build (project, "-p:_AndroidEnableTypemapR8Trimming=true");
		AssertModernRules ();
		var keysTime = File.GetLastWriteTimeUtc (keys);
		Build (project, "-p:_AndroidEnableTypemapR8Trimming=false");
		Assert.AreEqual (keysTime, File.GetLastWriteTimeUtc (keys));
		AssertLegacyRules ();
		Build (project, "-p:_AndroidEnableTypemapR8Trimming=true");
		AssertModernRules ();
		Build (project, "-p:_AndroidEnableTypemapR8Trimming=");
		AssertLegacyRules ();

		void AssertLegacyRules ()
		{
			StringAssert.Contains ("legacy ACW configuration", File.ReadAllText (rules));
			StringAssert.DoesNotContain ("UseTypeMap=true", File.ReadAllText (Path.Combine (directory, "writes.txt")));
		}

		void AssertModernRules ()
		{
			Assert.AreEqual (TypeRules ("test.Live"), File.ReadAllText (rules));
			FileAssert.DoesNotExist (Path.Combine (directory, "obj", "proguard", "proguard_typemap_members.cfg"));
			var writes = File.ReadAllText (Path.Combine (directory, "writes.txt"));
			StringAssert.Contains ("UseTypeMap=true", writes);
			StringAssert.DoesNotContain ("Members=", writes);
		}
	}

	[TestCase ("")]
	[TestCase ("false")]
	public void UnoptimizedNativeAotReadsObjectWithoutGraphs (string runILLink)
	{
		var nativeObject = WriteNativeObject ("app", "test/Live");
		var project = CreateProject ("NativeAOT", "trimmable",
			NativeObjectItem ("app.so", nativeObject));
		var keys = Path.Combine (directory, "obj", "typemap.keys.txt");
		var rules = Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg");
		Build (project, "-p:Optimize=false", "-p:_AndroidEnableTypemapR8Trimming=true",
			$"-p:RunILLink={runILLink}");
		Assert.AreEqual ("test/Live\n", File.ReadAllText (keys));
		Assert.AreEqual (TypeRules ("test.Live"), File.ReadAllText (rules));
	}

	[TestCase ("false", "false")]
	[TestCase ("false", "true")]
	[TestCase ("true", "false")]
	[TestCase ("true", "true")]
	public void TypemapInputsDoNotRequestGraphsOrChangeParallelism (string enabled, string diagnostics)
	{
		var project = CreateProject ("NativeAOT", "trimmable");
		var document = XDocument.Load (project);
		var root = document.Root ?? throw new InvalidOperationException ();
		root.Add (new XElement ("Import", new XAttribute ("Project",
			Path.Combine (RepositoryDirectory (), "src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.TypeMap.Trimmable.NativeAOT.targets"))));
		root.Add (new XElement ("Import", new XAttribute ("Project",
			Path.Combine (RepositoryDirectory (), "src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.TypeMap.Proguard.targets"))));
		root.Add (new XElement ("Target", new XAttribute ("Name", "_ReadGeneratedTrimmableTypeMapAssemblies")));
		root.Add (new XElement ("Target", new XAttribute ("Name", "Build"),
			new XAttribute ("DependsOnTargets", "_AndroidConfigureTypeMapProguard;_AddTrimmableTypeMapAssembliesToIlc"),
			new XElement ("WriteLinesToFile", new XAttribute ("File", "$(MSBuildProjectDirectory)/ilc.txt"),
				new XAttribute ("Lines", "@(IlcArg);Diagnostics=$(IlcGenerateDgmlFile);Parallel=$(_AndroidBuildRuntimeIdentifiersInParallel);Scoped=$(_AndroidUseScopedTypeMapMembers)"),
				new XAttribute ("Overwrite", "true"))));
		document.Save (project);
		Build (project, "-p:_AndroidEnableTypemapR8Trimming=" + enabled, "-p:IlcGenerateDgmlFile=" + diagnostics, "-p:Optimize=true");
		var lines = File.ReadAllLines (Path.Combine (directory, "ilc.txt"));
		var output = string.Join ("\n", lines);
		StringAssert.DoesNotContain ("--scandgmllog:", output);
		StringAssert.DoesNotContain ("--dgmllog:", output);
		CollectionAssert.Contains (lines, "Parallel=");
		CollectionAssert.Contains (lines, "Scoped=");
		StringAssert.Contains ("Diagnostics=" + diagnostics, output);
	}

	[Test]
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
			$"""<ResolvedFileToPublish Include="arm64/R2R/root.dll" AndroidTypeMapLinkedAssemblies="{SecurityElement.Escape (first + ";" + stub)}" />""",
			$"""<ResolvedFileToPublish Include="x64/R2R/root.dll" AndroidTypeMapLinkedAssemblies="{SecurityElement.Escape (second)}" />""",
			"""<ResolvedFileToPublish Include="pretrim/unused.dll" />""");
		Build (project);
		var rules = Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg");
		Assert.AreEqual (TypeRules ("test.Alias", "test.Live", "test.Second"), File.ReadAllText (rules));
		File.Delete (second);
		StringAssert.Contains ("XA4327", Build (project, expectSuccess: false));
	}

	[Test]
	public void LinkedTypeMapChangeInvalidatesCompileToDalvik ()
	{
		string EmitMap (string key)
		{
			var path = Path.Combine (directory, "app.dll");
			var model = new TypeMapAssemblyData { AssemblyName = "app", ModuleName = "app.dll" };
			model.Entries.Add (new TypeMapAttributeData {
				MapKey = key,
				ProxyTypeReference = "System.Object, System.Runtime",
			});
			using var stream = File.Create (path);
			new TypeMapAssemblyEmitter (new Version (11, 0, 0, 0)).Emit (model, stream);
			return path;
		}

		var map = EmitMap ("test/First");
		var project = CreateProject ("CoreCLR", "trimmable",
			$"""<ResolvedFileToPublish Include="app.dll" AndroidTypeMapLinkedAssemblies="{SecurityElement.Escape (map)}" />""");
		Build (project, "-t:_CompileToDalvik");
		var stamp = Path.Combine (directory, "dalvik.stamp");
		var firstTime = File.GetLastWriteTimeUtc (stamp);
		Build (project, "-t:_CompileToDalvik");
		Assert.AreEqual (firstTime, File.GetLastWriteTimeUtc (stamp));

		EmitMap ("test/Second");
		File.SetLastWriteTimeUtc (map, DateTime.UtcNow.AddSeconds (1));
		Build (project, "-t:_CompileToDalvik");
		Assert.AreNotEqual (firstTime, File.GetLastWriteTimeUtc (stamp));
	}

	[TestCase ("-p:_AndroidEnableTypemapR8Trimming=false")]
	[TestCase ("-p:RunILLink=false")]
	[TestCase ("-p:ProguardConfigFiles=custom.cfg")]
	public void CoreClrModeRoundTripsInvalidateCompileToDalvik (string disableArgument)
	{
		var map = Path.Combine (directory, "app.dll");
		var model = new TypeMapAssemblyData { AssemblyName = "app", ModuleName = "app.dll" };
		model.Entries.Add (new TypeMapAttributeData {
			MapKey = "test/Live",
			ProxyTypeReference = "System.Object, System.Runtime",
		});
		using (var stream = File.Create (map)) {
			new TypeMapAssemblyEmitter (new Version (11, 0, 0, 0)).Emit (model, stream);
		}
		var project = CreateProject ("CoreCLR", "trimmable",
			$"""<ResolvedFileToPublish Include="app.dll" AndroidTypeMapLinkedAssemblies="{SecurityElement.Escape (map)}" />""");
		var stamp = Path.Combine (directory, "dalvik.stamp");
		var policy = Path.Combine (directory, "dalvik-policy.txt");
		var cache = Path.Combine (directory, "obj", "build.props.cache");
		var keys = Path.Combine (directory, "obj", "typemap.keys.txt");
		var mode = Path.Combine (directory, "obj", "typemap.proguard-mode.inputs");
		var mapTime = File.GetLastWriteTimeUtc (map);

		Build (project, "-t:_CompileToDalvik", "-p:_AndroidEnableTypemapR8Trimming=true");
		StringAssert.Contains ("UseTypeMap=true", File.ReadAllText (policy));
		var cacheTime = File.GetLastWriteTimeUtc (cache);
		var keysTime = File.GetLastWriteTimeUtc (keys);
		AssertIncremental ("true");
		var enabledTime = File.GetLastWriteTimeUtc (stamp);

		Build (project, "-t:_CompileToDalvik", disableArgument,
			"-p:_MicrosoftAndroidBuildTasksAssembly=missing.dll");
		Assert.AreNotEqual (enabledTime, File.GetLastWriteTimeUtc (stamp), "Opt-out must invalidate Dalvik.");
		StringAssert.DoesNotContain ("UseTypeMap=true", File.ReadAllText (policy));
		StringAssert.DoesNotContain ("Members=", File.ReadAllText (policy));
		StringAssert.DoesNotContain ("UseTypeMap=true", File.ReadAllText (mode));
		Assert.AreEqual (keysTime, File.GetLastWriteTimeUtc (keys), "Opt-out must not extract keys.");
		AssertIncremental ("", disableArgument, "-p:_MicrosoftAndroidBuildTasksAssembly=missing.dll");
		var disabledTime = File.GetLastWriteTimeUtc (stamp);

		Build (project, "-t:_CompileToDalvik", "-p:_AndroidEnableTypemapR8Trimming=true");
		Assert.AreNotEqual (disabledTime, File.GetLastWriteTimeUtc (stamp), "Re-enabling must invalidate Dalvik.");
		StringAssert.Contains ("UseTypeMap=true", File.ReadAllText (policy));
		StringAssert.Contains ("Members=", File.ReadAllText (policy));
		AssertIncremental ("true");
		Assert.AreEqual (mapTime, File.GetLastWriteTimeUtc (map));
		Assert.AreEqual (cacheTime, File.GetLastWriteTimeUtc (cache));

		void AssertIncremental (string enabled, params string [] arguments)
		{
			var before = File.GetLastWriteTimeUtc (stamp);
			var modeTime = File.GetLastWriteTimeUtc (mode);
			Build (project, new [] { "-t:_CompileToDalvik", $"-p:_AndroidEnableTypemapR8Trimming={enabled}" }.Concat (arguments).ToArray ());
			Assert.AreEqual (before, File.GetLastWriteTimeUtc (stamp), "An unchanged mode should skip Dalvik.");
			Assert.AreEqual (modeTime, File.GetLastWriteTimeUtc (mode), "An unchanged policy must preserve the state timestamp.");
		}
	}

	[TestCase ("NativeAOT", "true", "r8", "true", "", "true")]
	[TestCase ("NativeAOT", "true", "r8", "true", "custom.cfg", "true")]
	[TestCase ("NativeAOT", "false", "r8", "true", "", "true")]
	[TestCase ("NativeAOT", "", "r8", "true", "", "true")]
	[TestCase ("NativeAOT", "true", "", "true", "", "true")]
	[TestCase ("NativeAOT", "true", "r8", "false", "", "true")]
	[TestCase ("CoreCLR", "true", "r8", "true", "", "false")]
	public void NdkDependencyFollowsRuntimeRatherThanTypemapPolicy (string runtime, string enabled, string linkTool, string trimmed, string proguardConfigFiles, string expected)
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
				new XElement (ns + "PublishAot", "true"),
				new XElement (ns + "PublishTrimmed", trimmed),
				new XElement (ns + "ProguardConfigFiles", proguardConfigFiles),
				new XElement (ns + "AndroidLinkTool", linkTool)),
			new XElement (ns + "Target", new XAttribute ("Name", "Build"),
				new XElement (dependencyProperties),
				new XElement (ns + "WriteLinesToFile", new XAttribute ("File", "$(MSBuildProjectDirectory)/ndk-required.txt"),
					new XAttribute ("Lines", "$(_NdkRequired)"), new XAttribute ("Overwrite", "true")))))
			.Save (path);
		Build (path);
		Assert.AreEqual (expected, File.ReadAllText (Path.Combine (directory, "ndk-required.txt")).Trim ());
	}

	[TestCase ("NativeAOT", "true", "", "custom-object/retained.o")]
	[TestCase ("NativeAOT", "false", "", "custom-object/retained.o")]
	[TestCase ("NativeAOT", "true", "false", "custom-object/retained.o")]
	[TestCase ("CoreCLR", "true", "", "obj/linked/_Binding.TypeMap.dll")]
	[TestCase ("CoreCLR", "true", "true", "obj/linked/_Binding.TypeMap.dll")]
	[TestCase ("CoreCLR", "true", "false", "")]
	public void InnerBuildReturnsExactProducerPaths (string runtime, string optimize, string runILLink, string expected)
	{
		var project = CreateProject (runtime, "trimmable");
		var assemblyResolutionTargets = Path.Combine (RepositoryDirectory (), "src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.AssemblyResolution.targets");
		var metadata = runtime == "NativeAOT" ? "AndroidTypeMapNativeObject" : "AndroidTypeMapLinkedAssemblies";
		var contents = File.ReadAllText (project)
			.Replace ("<Project>",
				$"""
				<Project>
				  <Import Project="{SecurityElement.Escape (assemblyResolutionTargets)}" />
				""", StringComparison.Ordinal)
			.Replace ("</Project>",
				$"""
				  <PropertyGroup>
				    <RuntimeIdentifier>android-x64</RuntimeIdentifier>
				    <NativeIntermediateOutputPath>$(MSBuildProjectDirectory)/custom-native/</NativeIntermediateOutputPath>
				    <NativeObject>$(MSBuildProjectDirectory)/custom-object/retained.o</NativeObject>
				    <_NdkBinDir>$(MSBuildProjectDirectory)/custom-ndk-bin/</_NdkBinDir>
				    <TargetName>App</TargetName>
				    <Optimize>{SecurityElement.Escape (optimize)}</Optimize>
				    <_AndroidEnableTypemapR8Trimming>true</_AndroidEnableTypemapR8Trimming>
				    <_TypeMapAssemblyName>_Microsoft.Android.TypeMaps</_TypeMapAssemblyName>
				  </PropertyGroup>
				  <Target Name="BuildOnlySettings" />
				  <Target Name="_CheckForInvalidConfigurationAndPlatform" />
				  <Target Name="_FixupIntermediateAssembly" />
				  <Target Name="_PatchNuGetReferenceMetadata" />
				  <Target Name="ResolveReferences" />
				  <Target Name="_AndroidAot" />
				  <Target Name="ComputeFilesToPublish">
				    <ItemGroup>
				      <_GeneratedTypeMapAssembliesFromList Include="generated/_Binding.TypeMap.dll" />
				      <ResolvedFileToPublish Include="R2R/_Microsoft.Android.TypeMaps.dll" />
				    </ItemGroup>
				  </Target>
				  <Target Name="Build" DependsOnTargets="_ComputeFilesToPublishForRuntimeIdentifiers">
				    <WriteLinesToFile File="$(MSBuildProjectDirectory)/producer.txt" Lines="@(ResolvedFileToPublish->'%({metadata})')" Overwrite="true" />
				    <WriteLinesToFile File="$(MSBuildProjectDirectory)/readobj.txt" Lines="@(ResolvedFileToPublish->'%(AndroidTypeMapLlvmReadObjPath)')" Overwrite="true" />
				  </Target>
				</Project>
				""", StringComparison.Ordinal);
		File.WriteAllText (project, contents);
		Build (project, $"-p:RunILLink={runILLink}");
		Assert.AreEqual (expected == "" ? "" : Path.Combine (directory, expected).Replace ('\\', '/'),
			File.ReadAllText (Path.Combine (directory, "producer.txt")).Trim ().Replace ('\\', '/'));
		if (runtime == "NativeAOT") {
			var executable = OperatingSystem.IsWindows () ? "llvm-readobj.exe" : "llvm-readobj";
			Assert.AreEqual (Path.Combine (directory, "custom-ndk-bin", executable).Replace ('\\', '/'),
				File.ReadAllText (Path.Combine (directory, "readobj.txt")).Trim ().Replace ('\\', '/'));
		}
	}

	[TestCase ("")]
	[TestCase ("true")]
	public void CoreClrWithoutILLinkNeedsNoLinkedInputsOrModernTasks (string enabled)
	{
		var project = CreateProject ("CoreCLR", "trimmable",
			"""<ResolvedFileToPublish Include="generated/root.dll" AndroidTypeMapLinkedAssemblies="$(MSBuildProjectDirectory)/obj/linked/missing.dll" />""");
		Assert.IsFalse (Directory.Exists (Path.Combine (directory, "obj", "linked")));
		Build (project, "-p:RunILLink=false", $"-p:_AndroidEnableTypemapR8Trimming={enabled}",
			"-p:_MicrosoftAndroidBuildTasksAssembly=missing.dll");
		foreach (var output in new [] { "typemap.keys.inputs", "typemap.keys.txt",
			"proguard/proguard_project_references.cfg", "proguard/proguard_typemap_members.cfg" }) {
			Assert.IsFalse (File.Exists (Path.Combine (directory, "obj", output)), output);
		}
		StringAssert.DoesNotContain ("UseTypeMap=true", File.ReadAllText (Path.Combine (directory, "writes.txt")));
	}

	[TestCase ("false", "", true)]
	[TestCase ("false", "true", true)]
	[TestCase ("false", "false", true)]
	[TestCase ("true", "", false)]
	[TestCase ("true", "true", false)]
	[TestCase ("true", "false", true)]
	public void CoreClrLegacyProducerRemainsEligibleWithoutILLink (string runILLink, string enabled, bool expected)
	{
		var targets = XDocument.Load (Path.Combine (RepositoryDirectory (), "src", "Xamarin.Android.Build.Tasks",
			"Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.TypeMap.Trimmable.CoreCLR.targets"));
		var root = targets.Root ?? throw new InvalidOperationException ();
		XNamespace ns = root.Name.Namespace;
		var prepare = new XElement (root.Elements (ns + "Target")
			.Single (target => (string?) target.Attribute ("Name") == "_PrepareLinkedAssembliesForProguard"));
		var generate = new XElement (root.Elements (ns + "Target")
			.Single (target => (string?) target.Attribute ("Name") == "_GenerateProguardConfiguration"));
		// Exercise the shipped target's eligibility and hooks without requiring the legacy task assembly.
		generate.Element (ns + "GenerateProguardConfiguration")?.ReplaceWith (
			new XElement (ns + "WriteLinesToFile", new XAttribute ("File", "$(_ProguardProjectConfiguration)"),
				new XAttribute ("Lines", "@(_LinkedAssemblyForProguard)"), new XAttribute ("Overwrite", "true")));
		var assembly = Write ("unlinked.dll", "input");
		var output = Path.Combine (directory, "proguard_project_references.cfg");
		var project = Path.Combine (directory, "legacy.proj");
		new XDocument (new XElement (ns + "Project",
			new XElement (ns + "PropertyGroup",
				new XElement (ns + "PublishTrimmed", "true"),
				new XElement (ns + "AndroidLinkTool", "r8"),
				new XElement (ns + "RunILLink", runILLink),
				new XElement (ns + "_AndroidEnableTypemapR8Trimming", enabled),
				new XElement (ns + "_ComputeFilesToPublishForRuntimeIdentifiers", "true"),
				new XElement (ns + "_ProguardProjectConfiguration", output),
				new XElement (ns + "_GenerateProguardAfterTargets", "ComputeFilesToPublish")),
			new XElement (ns + "ItemGroup", new XElement (ns + "ResolvedFileToPublish", new XAttribute ("Include", assembly))),
			new XElement (ns + "Target", new XAttribute ("Name", "_AndroidInvalidateProguardConfigurationStamp")),
			prepare, generate,
			new XElement (ns + "Target", new XAttribute ("Name", "ComputeFilesToPublish")),
			new XElement (ns + "Target", new XAttribute ("Name", "Build"),
				new XAttribute ("DependsOnTargets", "ComputeFilesToPublish")))).Save (project);

		Build (project);
		Assert.AreEqual (expected, File.Exists (output));
		if (expected) {
			Assert.AreEqual (assembly, File.ReadAllText (output).Trim ());
			var timestamp = File.GetLastWriteTimeUtc (output);
			Build (project);
			Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (output), "Unchanged fallback inputs should skip generation.");
		}
	}

	[TestCase ("MonoVM", "trimmable", "true", "r8", "true", false, "")]
	[TestCase ("CoreCLR", "trimmable", "false", "r8", "true", false, "")]
	[TestCase ("CoreCLR", "trimmable", "true", "", "true", false, "")]
	[TestCase ("CoreCLR", "trimmable", "true", "r8", "false", false, "")]
	[TestCase ("NativeAOT", "trimmable", "true", "r8", "false", false, "")]
	[TestCase ("NativeAOT", "trimmable", "true", "r8", "", false, "")]
	[TestCase ("CoreCLR", "trimmable", "true", "r8", "true", true, "")]
	[TestCase ("CoreCLR", "trimmable", "true", "r8", "true", false, "custom.cfg")]
	[TestCase ("NativeAOT", "trimmable", "true", "r8", "false", false, "custom.cfg")]
	[TestCase ("NativeAOT", "trimmable", "true", "r8", "true", false, "custom.cfg")]
	public void InactivePathsNeedNoTypemapInputsOrModernTasks (
		string runtime, string representation, string trimmed, string linkTool, string enabled, bool innerBuild, string proguardConfigFiles)
	{
		Write ("acw-map.txt", "App.Live, App;test.Live\n");
		var project = CreateProject (runtime, representation);
		Build (project,
			$"-p:PublishTrimmed={trimmed}",
			$"-p:AndroidLinkTool={linkTool}",
			$"-p:_AndroidEnableTypemapR8Trimming={enabled}",
			$"-p:_ComputeFilesToPublishForRuntimeIdentifiers={innerBuild}",
			$"-p:ProguardConfigFiles={proguardConfigFiles}",
			"-p:_MicrosoftAndroidBuildTasksAssembly=missing.dll");
		Assert.IsFalse (File.Exists (Path.Combine (directory, "obj", "typemap.keys.txt")));
		Assert.IsFalse (File.Exists (Path.Combine (directory, "obj", "proguard", "proguard_typemap_members.cfg")));
		if (proguardConfigFiles != "") {
			Assert.IsFalse (File.Exists (Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg")));
		}
		StringAssert.DoesNotContain ("UseTypeMap=true", File.ReadAllText (Path.Combine (directory, "writes.txt")));
	}

	static string TypeRules (params string [] names) =>
		string.Concat (names.Select (name => $"-keep class {name}\n-keep interface {name}\n"));

	static string MemberRules (params string [] names) =>
		string.Concat (names.Select (name => $"-keepclassmembers class {name} {{ *; }}\n-keepclassmembers interface {name} {{ *; }}\n"));

	string CreateProject (string runtime, string representation, params string [] sourceItems)
	{
		var targets = Path.Combine (RepositoryDirectory (), "src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.TypeMap.Proguard.targets");
		var path = Path.Combine (directory, "pipeline.proj");
		var items = string.Join (Environment.NewLine + "      ", sourceItems);
		Directory.CreateDirectory (Path.Combine (directory, "obj"));
		File.WriteAllText (path,
			$"""
			<Project>
			  <PropertyGroup>
			    <_MicrosoftAndroidBuildTasksAssembly>{SecurityElement.Escape (typeof (GenerateTypeMapProguardConfiguration).Assembly.Location)}</_MicrosoftAndroidBuildTasksAssembly>
			    <_XamarinAndroidBuildTasksAssembly>unused-legacy-tasks.dll</_XamarinAndroidBuildTasksAssembly>
			    <_AndroidRuntime>{SecurityElement.Escape (runtime)}</_AndroidRuntime>
			    <AndroidTypeMapImplementation>{SecurityElement.Escape (representation)}</AndroidTypeMapImplementation>
			    <PublishTrimmed>true</PublishTrimmed>
			    <AndroidLinkTool>r8</AndroidLinkTool>
			    <Optimize>true</Optimize>
			    <IntermediateOutputPath>$(MSBuildProjectDirectory)/obj/</IntermediateOutputPath>
			    <_AndroidBuildPropertiesCache>$(MSBuildProjectDirectory)/obj/build.props.cache</_AndroidBuildPropertiesCache>
			    <_AcwMapFile>$(MSBuildProjectDirectory)/acw-map.txt</_AcwMapFile>
			    <_CompileToDalvikDependsOnTargets>_CreatePropertiesCache;_CalculateProguardConfigurationFiles</_CompileToDalvikDependsOnTargets>
			    <_CompileToDalvikInputs>$(_AndroidBuildPropertiesCache);@(ProguardConfiguration)</_CompileToDalvikInputs>
			  </PropertyGroup>
			  <Import Project="{SecurityElement.Escape (targets)}" />
			  <Target Name="_GenerateJavaStubs">
			    <ItemGroup>
			      {items}
			    </ItemGroup>
			  </Target>
			  <Target Name="_CalculateProguardConfigurationFiles" />
			  <Target Name="_CreatePropertiesCache">
			    <WriteLinesToFile File="$(_AndroidBuildPropertiesCache)" Lines="ProductionCacheContent" Overwrite="true" WriteOnlyWhenDifferent="true" />
			  </Target>
			  <Target Name="Build" DependsOnTargets="_CreatePropertiesCache;_CalculateProguardConfigurationFiles;_AndroidGenerateTypeMapProguardConfiguration;_AndroidGenerateTypeMapMemberProguardConfiguration">
			    <WriteLinesToFile File="$(MSBuildProjectDirectory)/writes.txt" Lines="@(FileWrites);UseTypeMap=$(_AndroidUseTypeMapProguardConfiguration);@(_ProguardConfiguration->'Members=%(Identity)')" Overwrite="true" />
			  </Target>
			  <Target Name="_CompileToDalvik"
			      DependsOnTargets="$(_CompileToDalvikDependsOnTargets)"
			      Inputs="$(_CompileToDalvikInputs)"
			      Outputs="$(MSBuildProjectDirectory)/dalvik.stamp">
			    <WriteLinesToFile File="$(MSBuildProjectDirectory)/dalvik-policy.txt" Lines="UseTypeMap=$(_AndroidUseTypeMapProguardConfiguration);@(_ProguardConfiguration->'Members=%(Identity)')" Overwrite="true" />
			    <Touch Files="$(MSBuildProjectDirectory)/dalvik.stamp" AlwaysCreate="true" />
			  </Target>
			</Project>
			""");
		return path;
	}

	static string RepositoryDirectory ()
	{
		var repository = new DirectoryInfo (AppContext.BaseDirectory);
		while (repository != null && !File.Exists (Path.Combine (repository.FullName, "Configuration.props"))) {
			repository = repository.Parent;
		}
		return repository?.FullName ?? throw new InvalidOperationException ("Could not locate the repository.");
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
		Assert.IsTrue (completed, "MSBuild did not finish." + Environment.NewLine + output);
		Assert.IsTrue (expectSuccess ? process.ExitCode == 0 : process.ExitCode != 0, output);
		return output;
	}

	string Write (string name, string content)
	{
		var path = Path.Combine (directory, name);
		File.WriteAllText (path, content, new UTF8Encoding (false));
		return path;
	}

	string WriteNativeObject (string name, params string [] keys) =>
		TypeMapProguardNativeObject.WriteObject (directory, name, keys);

	static string NativeObjectItem (string output, string nativeObject) =>
		$"""
		<ResolvedFileToPublish Include="{SecurityElement.Escape (output)}"
		                        AndroidTypeMapNativeObject="{SecurityElement.Escape (nativeObject)}"
		                        AndroidTypeMapLlvmReadObjPath="{SecurityElement.Escape (TypeMapProguardNativeObject.LlvmReadObjPath)}"
		                        AndroidTypeMapLlvmObjDumpPath="{SecurityElement.Escape (TypeMapProguardNativeObject.LlvmObjDumpPath)}" />
		""";
}
