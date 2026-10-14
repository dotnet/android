using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable (ParallelScope.Children)]
public class DesignTimeResourceTargetsTests : BaseTest
{
	[Test]
	public void ResourcePipelineAndJavaValidationSkipDesignTimeBuilds (
		[Values ("Windows_NT", "Unix")] string os,
		[Values ("true", "False")] string designTimeBuild,
		[Values] bool useDesignerAssembly,
		[Values] bool buildingInsideVisualStudio)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string tasks = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src", "Xamarin.Android.Build.Tasks");
		var setup = LoadTarget (Path.Combine (tasks, "Xamarin.Android.DesignTime.targets"), "_SetupDesignTimeBuildForCompile");
		var resources = LoadTarget (Path.Combine (tasks, "Xamarin.Android.Common.targets"), "_UpdateAndroidResources");
		var update = LoadTarget (Path.Combine (tasks, "Xamarin.Android.Common.targets"), "UpdateAndroidResources");
		var aapt = LoadTarget (Path.Combine (tasks, "MSBuild", "Xamarin", "Android", "Xamarin.Android.Aapt2.targets"), "_InjectAaptDependencies");
		var tooling = LoadTarget (Path.Combine (tasks, "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.Tooling.targets"), "_ResolveAndroidTooling");
		var validation = tooling.Element ("ValidateJavaVersion") ?? throw new InvalidOperationException ("Missing Java version validation.");
		var validationCondition = validation.Attribute ("Condition") ?? throw new InvalidOperationException ("Missing Java version validation condition.");
		tooling.RemoveNodes ();
		tooling.Add (new XElement ("PropertyGroup", new XAttribute (validationCondition), new XElement ("JavaValidation", "true")));

		var imports = LoadTarget (Path.Combine (tasks, "Xamarin.Android.EmbeddedResource.targets"), "_ResolveLibraryProjectImports");
		imports.Attribute ("DependsOnTargets")?.Remove ();
		imports.Attribute ("Inputs")?.Remove ();
		imports.Attribute ("Outputs")?.Remove ();
		imports.RemoveNodes ();
		imports.Add (new XElement ("PropertyGroup", new XElement ("LibraryImports", "true")));

		string project = Path.Combine (directory, "resources.proj");
		string output = Path.Combine (directory, "pipeline.txt");
		new XDocument (new XElement ("Project",
			new XElement ("PropertyGroup",
				new XElement ("OS", os),
				new XElement ("DesignTimeBuild", designTimeBuild),
				new XElement ("AndroidUseDesignerAssembly", useDesignerAssembly),
				new XElement ("BuildingInsideVisualStudio", buildingInsideVisualStudio),
				new XElement ("IntermediateOutputPath", Path.Combine (directory, "obj") + Path.DirectorySeparatorChar),
				new XElement ("_AndroidIntermediateDesignTimeBuildDirectory", Path.Combine (directory, "obj", "designtime") + Path.DirectorySeparatorChar),
				new XElement ("_AndroidIntermediateDesignTimeStampDirectory", Path.Combine (directory, "obj", "designtime", "stamp") + Path.DirectorySeparatorChar),
				new XElement ("_UpdateAndroidResourcesDependsOn", "RecordResourceBuild")),
			setup, resources, update, aapt, tooling, imports,
			new XElement ("Target", new XAttribute ("Name", "RecordResourceBuild"),
				new XElement ("PropertyGroup", new XElement ("ResourceBuild", "true"))),
			new XElement ("Target", new XAttribute ("Name", "_BuildResourceDesigner")),
			new XElement ("Target", new XAttribute ("Name", "Build"),
				new XAttribute ("DependsOnTargets", "UpdateAndroidResources;_InjectAaptDependencies;_ResolveAndroidTooling;_ResolveLibraryProjectImports"),
				new XElement ("WriteLinesToFile",
					new XAttribute ("File", output),
					new XAttribute ("Lines", "ResourceBuild=$(ResourceBuild);JavaValidation=$(JavaValidation);LibraryImports=$(LibraryImports);$(_PrepareUpdateAndroidResgenDependsOnTargets)"),
					new XAttribute ("Overwrite", "true")))))
			.Save (project);

		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:Build", "-v:minimal", "-nr:false");
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		string [] pipeline = File.ReadAllLines (output);
		bool isDesignTimeBuild = string.Equals (designTimeBuild, "true", StringComparison.OrdinalIgnoreCase);
		CollectionAssert.Contains (pipeline, "ResourceBuild=" + (isDesignTimeBuild ? "" : "true"));
		CollectionAssert.Contains (pipeline, "JavaValidation=" + (isDesignTimeBuild ? "" : "true"));
		Assert.AreEqual (!isDesignTimeBuild, pipeline.Any (line => line.Trim () == "_CompileResources"),
			"Only normal builds should compile resources with aapt2.");
		CollectionAssert.Contains (pipeline, "LibraryImports=true");
	}

	static XElement LoadTarget (string file, string name)
	{
		var source = XDocument.Load (file);
		var target = new XElement (source.Descendants ().Single (element =>
			element.Name.LocalName == "Target" && (string?) element.Attribute ("Name") == name));
		foreach (var element in target.DescendantsAndSelf ()) {
			element.Name = element.Name.LocalName;
		}
		return target;
	}
}
