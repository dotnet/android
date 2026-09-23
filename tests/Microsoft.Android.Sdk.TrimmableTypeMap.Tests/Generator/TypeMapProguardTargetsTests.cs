using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using Microsoft.Android.Tasks;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class TypeMapProguardTargetsTests : IDisposable
{
	readonly string directory = Path.Combine (Path.GetTempPath (), "typemap-proguard-targets-" + Guid.NewGuid ().ToString ("N"));

	public TypeMapProguardTargetsTests () => Directory.CreateDirectory (directory);

	[Fact]
	public void LlvmTargetTracksAbiUnionInputListAndDeletedOutputs ()
	{
		Write ("first.ll", "@java_type_names = dso_local local_unnamed_addr constant [10 x i8] c\"test/Live\\00\", align 1\n");
		Write ("second.ll", "@java_type_names = dso_local local_unnamed_addr constant [12 x i8] c\"test/Second\\00\", align 1\n");
		var project = CreateProject ("CoreCLR", "llvm-ir",
			"""<_TypeMapAssemblySource Include="$(MSBuildProjectDirectory)/first.ll" />""",
			"""<_TypeMapAssemblySource Include="$(MSBuildProjectDirectory)/second.ll" Condition="'$(OneAbi)' != 'true'" />""");
		Build (project);
		var keys = Path.Combine (directory, "obj", "typemap.keys.txt");
		var rules = Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg");
		var members = Path.Combine (directory, "obj", "proguard", "proguard_typemap_members.cfg");
		Assert.Equal (TypeRules ("test.Live", "test.Second"), File.ReadAllText (rules));
		Assert.Equal (MemberRules ("test.Live", "test.Second"), File.ReadAllText (members));
		var firstTime = File.GetLastWriteTimeUtc (keys);
		var rulesTime = File.GetLastWriteTimeUtc (rules);
		var membersTime = File.GetLastWriteTimeUtc (members);
		Build (project);
		Assert.Equal (firstTime, File.GetLastWriteTimeUtc (keys));
		Assert.Equal (rulesTime, File.GetLastWriteTimeUtc (rules));
		Assert.Equal (membersTime, File.GetLastWriteTimeUtc (members));
		Assert.Contains (
			members.Replace ('\\', '/'),
			File.ReadAllText (Path.Combine (directory, "writes.txt")).Replace ('\\', '/'));

		Build (project, "-p:OneAbi=true");
		Assert.Equal ("test/Live\n", File.ReadAllText (keys));
		Assert.Equal (TypeRules ("test.Live"), File.ReadAllText (rules));
		Assert.Equal (MemberRules ("test.Live"), File.ReadAllText (members));
		File.Delete (members);
		Build (project, "-p:OneAbi=true");
		Assert.Equal (MemberRules ("test.Live"), File.ReadAllText (members));
		File.Delete (keys);
		Build (project, "-p:OneAbi=true");
		Assert.Equal ("test/Live\n", File.ReadAllText (keys));
		File.Delete (rules);
		Build (project, "-p:OneAbi=true");
		Assert.Equal (TypeRules ("test.Live"), File.ReadAllText (rules));
		Write ("first.ll", "@java_type_names = dso_local local_unnamed_addr constant [13 x i8] c\"test/Changed\\00\", align 1\n");
		Build (project, "-p:OneAbi=true");
		Assert.Equal (TypeRules ("test.Changed"), File.ReadAllText (rules));
		Assert.Equal (MemberRules ("test.Changed"), File.ReadAllText (members));

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
			"""<_TypeMapAssemblySource Include="$(MSBuildProjectDirectory)/first.ll" />""");
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
		Assert.Equal (TypeRules ("test.Live", "test.Outer$Inner", "test.Second"), File.ReadAllText (rules));
		Assert.False (File.Exists (Path.Combine (directory, "obj", "proguard", "proguard_typemap_members.cfg")));
		Build (project, "-p:_AndroidEnableTypemapR8Trimming=false");
		Assert.Contains ("legacy ACW configuration", File.ReadAllText (rules));
		File.Delete (second);
		var output = Build (project, false, "-p:_AndroidEnableTypemapR8Trimming=true");
		Assert.Contains ("XA4327", output);
	}

	[NativeAotObjectFact]
	public void RuntimeSwitchCannotReuseAnotherRepresentation ()
	{
		Write ("first.ll", "@java_type_names = dso_local local_unnamed_addr constant [10 x i8] c\"test/Llvm\\00\", align 1\n");
		var nativeObject = WriteNativeObject ("app", "test/Native");
		var project = CreateProject ("CoreCLR", "llvm-ir",
			"""<_TypeMapAssemblySource Include="$(MSBuildProjectDirectory)/first.ll" />""",
			NativeObjectItem ("app.so", nativeObject));
		var rules = Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg");
		var members = Path.Combine (directory, "obj", "proguard", "proguard_typemap_members.cfg");
		Build (project);
		Assert.Equal (TypeRules ("test.Llvm"), File.ReadAllText (rules));
		Assert.Contains (
			"Members=" + members.Replace ('\\', '/'),
			File.ReadAllText (Path.Combine (directory, "writes.txt")).Replace ('\\', '/'));
		Build (project, "-p:_AndroidRuntime=NativeAOT", "-p:AndroidTypeMapImplementation=trimmable", "-p:_AndroidEnableTypemapR8Trimming=true");
		Assert.Equal (TypeRules ("test.Native"), File.ReadAllText (rules));
		Assert.DoesNotContain (
			"Members=" + members.Replace ('\\', '/'),
			File.ReadAllText (Path.Combine (directory, "writes.txt")).Replace ('\\', '/'));
		Build (project);
		Assert.Equal (TypeRules ("test.Llvm"), File.ReadAllText (rules));
		Assert.Equal (MemberRules ("test.Llvm"), File.ReadAllText (members));
	}

	[Fact]
	public void CompleteConfigurationOverridePreservesLegacyR8Policy ()
	{
		Write ("first.ll", "@java_type_names = dso_local local_unnamed_addr constant [10 x i8] c\"test/Live\\00\", align 1\n");
		var project = CreateProject ("CoreCLR", "llvm-ir",
			"""<_TypeMapAssemblySource Include="$(MSBuildProjectDirectory)/first.ll" />""");
		Build (project);
		Assert.Contains ("UseTypeMap=true", File.ReadAllText (Path.Combine (directory, "writes.txt")));
		Build (project, "-p:ProguardConfigFiles=custom.cfg");
		Assert.DoesNotContain ("UseTypeMap=true", File.ReadAllText (Path.Combine (directory, "writes.txt")));
		Assert.DoesNotContain ("Members=" + Path.Combine (directory, "obj"), File.ReadAllText (Path.Combine (directory, "writes.txt")));
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
			"""<_TypeMapAssemblySource Include="$(MSBuildProjectDirectory)/first.ll" />""",
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
				Assert.Equal (TypeRules ("test.Live"), File.ReadAllText (rules));
				Assert.Contains ("UseTypeMap=true", File.ReadAllText (Path.Combine (directory, "writes.txt")));
			}
		}
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
		Assert.Equal (TypeRules ("test.Live"), File.ReadAllText (rules));
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
			$"""<ResolvedFileToPublish Include="arm64/R2R/root.dll" AndroidTypeMapLinkedAssemblies="{SecurityElement.Escape (first + ";" + stub)}" />""",
			$"""<ResolvedFileToPublish Include="x64/R2R/root.dll" AndroidTypeMapLinkedAssemblies="{SecurityElement.Escape (second)}" />""",
			"""<ResolvedFileToPublish Include="pretrim/unused.dll" />""");
		Build (project);
		var rules = Path.Combine (directory, "obj", "proguard", "proguard_project_references.cfg");
		Assert.Equal (TypeRules ("test.Alias", "test.Live", "test.Second"), File.ReadAllText (rules));
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
	[InlineData ("MonoVM", "llvm-ir", "true", "r8", "true", false)]
	[InlineData ("CoreCLR", "llvm-ir", "false", "r8", "true", false)]
	[InlineData ("CoreCLR", "llvm-ir", "true", "", "true", false)]
	[InlineData ("CoreCLR", "llvm-ir", "true", "r8", "false", false)]
	[InlineData ("CoreCLR", "trimmable", "true", "r8", "false", false)]
	[InlineData ("NativeAOT", "trimmable", "true", "r8", "false", false)]
	[InlineData ("NativeAOT", "trimmable", "true", "r8", "", false)]
	[InlineData ("CoreCLR", "trimmable", "true", "r8", "true", true)]
	public void InactivePathsNeedNoTypemapInputsOrModernTasks (
		string runtime, string representation, string trimmed, string linkTool, string enabled, bool innerBuild)
	{
		Write ("acw-map.txt", "App.Live, App;test.Live\n");
		var project = CreateProject (runtime, representation);
		Build (project,
			$"-p:PublishTrimmed={trimmed}",
			$"-p:AndroidLinkTool={linkTool}",
			$"-p:_AndroidEnableTypemapR8Trimming={enabled}",
			$"-p:_ComputeFilesToPublishForRuntimeIdentifiers={innerBuild}",
			"-p:_MicrosoftAndroidBuildTasksAssembly=missing.dll");
		Assert.False (File.Exists (Path.Combine (directory, "obj", "typemap.keys.txt")));
		Assert.False (File.Exists (Path.Combine (directory, "obj", "proguard", "proguard_typemap_members.cfg")));
		Assert.DoesNotContain ("UseTypeMap=true", File.ReadAllText (Path.Combine (directory, "writes.txt")));
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
			  </PropertyGroup>
			  <Import Project="{SecurityElement.Escape (targets)}" />
			  <Target Name="_GenerateJavaStubs">
			    <ItemGroup>
			      {items}
			    </ItemGroup>
			  </Target>
			  <Target Name="_CalculateProguardConfigurationFiles" />
			  <Target Name="_CreatePropertiesCache">
			    <WriteLinesToFile File="$(_AndroidBuildPropertiesCache)" Lines="Enabled=$(_AndroidEnableTypemapR8Trimming)" Overwrite="true" WriteOnlyWhenDifferent="true" />
			  </Target>
			  <Target Name="Build" DependsOnTargets="_CreatePropertiesCache;_CalculateProguardConfigurationFiles;_AndroidGenerateTypeMapProguardConfiguration">
			    <WriteLinesToFile File="$(MSBuildProjectDirectory)/writes.txt" Lines="@(FileWrites);UseTypeMap=$(_AndroidUseTypeMapProguardConfiguration);@(_ProguardConfiguration->'Members=%(Identity)')" Overwrite="true" />
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

	static string NativeObjectItem (string output, string nativeObject) =>
		$"""
		<ResolvedFileToPublish Include="{SecurityElement.Escape (output)}"
		                        AndroidTypeMapNativeObject="{SecurityElement.Escape (nativeObject)}"
		                        AndroidTypeMapLlvmReadObjPath="{SecurityElement.Escape (NativeAotObjectIntegrationTools.LlvmReadObjPath)}"
		                        AndroidTypeMapLlvmObjDumpPath="{SecurityElement.Escape (NativeAotObjectIntegrationTools.LlvmObjDumpPath)}" />
		""";

	public void Dispose () => Directory.Delete (directory, recursive: true);
}
