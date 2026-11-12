using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class ClassParseTargetsTests : BaseTest
{
	[Test]
	public void PreservesLiteralPathsAndResponseArguments ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		string special = Path.Combine (directory, "binding $PATH %PATH% %25 apostrophe's");
		Directory.CreateDirectory (special);
		string host = Path.Combine (special, OperatingSystem.IsWindows () ? "dotnet.cmd" : "dotnet");
		File.WriteAllText (host, OperatingSystem.IsWindows ()
			? "@echo off\r\nif not \"%~1\"==\"%~dp0class-parse.dll\" exit /b 8\r\nset \"response=%~2\"\r\ntype \"%response:~1%\" > \"%~dp0captured.rsp\"\r\n"
			: "#!/bin/sh\n[ \"$1\" = \"$(dirname \"$0\")/class-parse.dll\" ] || exit 8\ncp \"${2#@}\" \"$(dirname \"$0\")/captured.rsp\"\n");
		if (!OperatingSystem.IsWindows ()) {
			File.SetUnixFileMode (host, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}
		string targets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src", "Xamarin.Android.Build.Tasks",
			"MSBuild", "Xamarin", "Android", "Xamarin.Android.Bindings.ClassParse.targets");
		var source = XDocument.Load (targets);
		foreach (var element in source.Descendants ()) {
			element.Name = element.Name.LocalName;
		}
		var target = new XElement (source.Descendants ().Single (element =>
			element.Name.LocalName == "Target" && (string?) element.Attribute ("Name") == "_ExportJarToXml"));
		// Isolate class-parse execution from the unrelated managed XML adjuster.
		target.Element ("BindingsGenerator")?.Remove ();
		string output = Path.Combine (special, "api.xml");
		string sourceJar = Path.Combine (special, "source.jar");
		string referenceJar = Path.Combine (special, "reference.jar");
		string documentation = Path.Combine (special, "docs");
		string project = Path.Combine (directory, "class-parse.proj");
		new XDocument (new XElement ("Project",
			new XElement ("PropertyGroup",
				new XElement ("NetCoreRoot", ""),
				new XElement ("DOTNET_HOST_PATH", host.Replace ("%", "%25")),
				new XElement ("_BindingsToolsLocation", special.Replace ("%", "%25")),
				new XElement ("ClassParseToolExe", "class-parse.dll"),
				new XElement ("ApiOutputFile", output.Replace ("%", "%25")),
				new XElement ("JavaDocPaths", documentation.Replace ("%", "%25"))),
			new XElement ("ItemGroup",
				new XElement ("InputJar", new XAttribute ("Include", sourceJar.Replace ("%", "%25"))),
				new XElement ("ReferenceJar", new XAttribute ("Include", referenceJar.Replace ("%", "%25")))),
			source.Root?.Elements ().Where (element => element.Name.LocalName == "Target" &&
				(string?) element.Attribute ("Name") == "_ExportJarToXmlInputs"),
			new XElement ("Target", new XAttribute ("Name", "_ExtractJavadocsFromJavaSourceJars")),
			target)).Save (project);
		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:_ExportJarToXml", "-v:normal", "-nr:false");
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		string [] response = File.ReadAllLines (Path.Combine (special, "captured.rsp"));
		CollectionAssert.Contains (response, $"--o=\"{output}.class-parse\"");
		CollectionAssert.Contains (response, $"--reference-output=\"{output}.reference.class-parse\"");
		CollectionAssert.Contains (response, $"--reference=\"{referenceJar}\"");
		CollectionAssert.Contains (response, $"--docspath=\"{documentation}\"");
		CollectionAssert.Contains (response, $"\"{sourceJar}\"");
	}
}
