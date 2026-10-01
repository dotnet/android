using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Android.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class ZipTaskRegistrationTests : BaseTest
{
	const string CommonTargets = "Xamarin.Android.Common.targets";
	const string JavacTargets = "MSBuild/Xamarin/Android/Xamarin.Android.Javac.targets";
	const string AssetsTargets = "MSBuild/Xamarin/Android/Xamarin.Android.Assets.targets";
	static readonly XNamespace MSBuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

	string TestDirectory => Path.Combine (Root, "temp", TestName);

	[SetUp]
	public void Setup ()
	{
		Directory.CreateDirectory (TestDirectory);
	}

	[TestCase ("BuildArchive", CommonTargets, 2)]
	[TestCase ("CreateJavaArchive", JavacTargets, 1)]
	[TestCase ("FixupAssetPackArchive", AssetsTargets, 1)]
	public void ShippedRegistrationAndInvocationsUseModernNetTasks (string taskName, string targetsFile, int invocationCount)
	{
		var document = LoadTargets (targetsFile);
		var registration = GetRegistration (document, taskName);
		Assert.AreEqual ("$(_MicrosoftAndroidBuildTasksAssembly)", (string)registration.Attribute ("AssemblyFile"));
		Assert.IsNull (registration.Attribute ("Runtime"), "Shipped registrations should not select a task host.");
		Assert.IsNull (registration.Attribute ("TaskFactory"), "Shipped registrations should not select a task factory.");

		var invocations = document.Descendants (MSBuildNamespace + taskName).ToArray ();
		Assert.AreEqual (invocationCount, invocations.Length);
		foreach (var invocation in invocations) {
			Assert.AreEqual ("NET", (string)invocation.Attribute ("MSBuildRuntime"), taskName);
			Assert.IsNull (invocation.Attribute ("UseLibZipSharp"));
			Assert.IsNull (invocation.Attribute ("ZipFlushFilesLimit"));
			Assert.IsNull (invocation.Attribute ("ZipFlushSizeLimit"));
		}
	}

	[Test]
	public async Task CreateJavaArchiveRunsThroughShippedInvocation ()
	{
		var classes = Path.Combine (TestDirectory, "classes");
		Directory.CreateDirectory (classes);
		var contents = new byte [] { 0xca, 0xfe, 0xba, 0xbe };
		File.WriteAllBytes (Path.Combine (classes, "Main.class"), contents);
		var output = Path.Combine (TestDirectory, "classes.jar");
		var properties = new XElement (MSBuildNamespace + "PropertyGroup",
			new XElement (MSBuildNamespace + "_AndroidIntermediateBindingJavaClassDirectory", classes),
			new XElement (MSBuildNamespace + "_AndroidIntermediateBindingClassesZip", output));
		var items = new XElement (MSBuildNamespace + "ItemGroup",
			new XElement (MSBuildNamespace + "_JavaBindingSource", new XAttribute ("Include", "Main.java")));

		await RunShippedInvocation ("CreateJavaArchive", JavacTargets, properties, items);

		using var archive = ZipFile.OpenRead (output);
		var entry = archive.GetEntry ("Main.class") ?? throw new InvalidOperationException ("The registered task did not create Main.class.");
		Assert.AreEqual (ZipCompressionMethod.Stored, entry.CompressionMethod);
		using var stream = entry.Open ();
		using var result = new MemoryStream ();
		stream.CopyTo (result);
		CollectionAssert.AreEqual (contents, result.ToArray ());
	}

	[Test]
	public async Task FixupAssetPackArchiveRunsThroughShippedInvocation ()
	{
		var output = Path.Combine (TestDirectory, "assetpack.zip");
		using (var archive = ZipFile.Open (output, ZipArchiveMode.Create)) {
			WriteEntry (archive, "AndroidManifest.xml", "manifest");
			WriteEntry (archive, "resources.pb", "unused resources");
			WriteEntry (archive, @"assets\contents.dat", "contents");
		}
		var items = new XElement (MSBuildNamespace + "ItemGroup",
			new XElement (MSBuildNamespace + "_AssetPacks", new XAttribute ("Include", "assetpack"),
				new XElement (MSBuildNamespace + "AssetPackOutput", output)));

		await RunShippedInvocation ("FixupAssetPackArchive", AssetsTargets, new XElement (MSBuildNamespace + "PropertyGroup"), items);

		using var result = ZipFile.OpenRead (output);
		CollectionAssert.AreEquivalent (new [] { "manifest/AndroidManifest.xml", "assets/contents.dat" },
			result.Entries.Select (entry => entry.FullName));
		foreach (var entry in result.Entries)
			Assert.AreEqual (ZipCompressionMethod.Stored, entry.CompressionMethod);
	}

	[Test]
	public async Task BuildArchiveRunsThroughShippedInvocation ()
	{
		var file = Path.Combine (TestDirectory, "contents.dat");
		File.WriteAllText (file, "contents");
		var output = Path.Combine (TestDirectory, "app.apk");
		var properties = new XElement (MSBuildNamespace + "PropertyGroup",
			new XElement (MSBuildNamespace + "AndroidPackageFormat", "apk"),
			new XElement (MSBuildNamespace + "_PackagedResources", ""),
			new XElement (MSBuildNamespace + "_ApkOutputPath", output),
			new XElement (MSBuildNamespace + "AndroidStoreUncompressedFileExtensions", ".dat"));
		var items = new XElement (MSBuildNamespace + "ItemGroup",
			new XElement (MSBuildNamespace + "FilesToAddToArchive", new XAttribute ("Include", file),
				new XElement (MSBuildNamespace + "ArchivePath", "assets/contents.dat")));

		await RunShippedInvocation ("BuildArchive", CommonTargets, properties, items);

		using var archive = ZipFile.OpenRead (output);
		var entry = archive.GetEntry ("assets/contents.dat") ?? throw new InvalidOperationException ("The registered task did not package contents.dat.");
		Assert.AreEqual (ZipCompressionMethod.Stored, entry.CompressionMethod);
		using var reader = new StreamReader (entry.Open ());
		Assert.AreEqual ("contents", reader.ReadToEnd ());
	}

	async Task RunShippedInvocation (string taskName, string targetsFile, XElement properties, XElement items)
	{
		var source = LoadTargets (targetsFile);
		var registration = new XElement (GetRegistration (source, taskName));
		var invocation = new XElement (source.Descendants (MSBuildNamespace + taskName).First ());
		properties.AddFirst (new XElement (MSBuildNamespace + "_MicrosoftAndroidBuildTasksAssembly", typeof (BuildArchive).Assembly.Location));
		var project = new XDocument (
			new XElement (MSBuildNamespace + "Project",
				properties,
				registration,
				items,
				new XElement (MSBuildNamespace + "Target", new XAttribute ("Name", "Run"), invocation)));
		var projectFile = Path.Combine (TestDirectory, "task.proj");
		project.Save (projectFile);

		var scratch = Path.Combine (TestDirectory, "scratch");
		Directory.CreateDirectory (scratch);
		var host = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH");
		var startInfo = new ProcessStartInfo {
			FileName = string.IsNullOrEmpty (host) ? "dotnet" : host,
			WorkingDirectory = TestDirectory,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		startInfo.ArgumentList.Add ("msbuild");
		startInfo.ArgumentList.Add (projectFile);
		startInfo.ArgumentList.Add ("-t:Run");
		startInfo.ArgumentList.Add ("-nologo");
		startInfo.ArgumentList.Add ("-nodeReuse:false");
		startInfo.ArgumentList.Add ("-verbosity:minimal");
		startInfo.Environment ["TMPDIR"] = scratch;
		startInfo.Environment ["TMP"] = scratch;
		startInfo.Environment ["TEMP"] = scratch;

		using var process = Process.Start (startInfo) ?? throw new InvalidOperationException ("Could not start MSBuild.");
		var output = process.StandardOutput.ReadToEndAsync ();
		var errors = process.StandardError.ReadToEndAsync ();
		using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (60));
		try {
			await process.WaitForExitAsync (timeout.Token);
		} catch (OperationCanceledException) {
			if (!process.HasExited)
				process.Kill (entireProcessTree: true);
			await process.WaitForExitAsync ();
			throw new TimeoutException ($"MSBuild timed out while invoking {taskName}.");
		}
		Assert.AreEqual (0, process.ExitCode, $"{taskName} failed through its shipped registration/invocation:\n{await output}\n{await errors}");
	}

	static XDocument LoadTargets (string relativePath)
	{
		var directory = new DirectoryInfo (TestContext.CurrentContext.WorkDirectory);
		while (directory != null) {
			var sourceDirectory = Path.Combine (directory.FullName, "src", "Xamarin.Android.Build.Tasks");
			if (File.Exists (Path.Combine (sourceDirectory, CommonTargets)))
				return XDocument.Load (Path.Combine (sourceDirectory, relativePath.Replace ('/', Path.DirectorySeparatorChar)));
			directory = directory.Parent;
		}
		throw new DirectoryNotFoundException ("Could not locate the shipped Android task targets in the checkout.");
	}

	static XElement GetRegistration (XDocument document, string taskName) =>
		document.Descendants (MSBuildNamespace + "UsingTask").Single (element =>
			(string)element.Attribute ("TaskName") == $"Microsoft.Android.Tasks.{taskName}");

	static void WriteEntry (ZipArchive archive, string name, string contents)
	{
		using var writer = new StreamWriter (archive.CreateEntry (name, CompressionLevel.NoCompression).Open (), new UTF8Encoding (false));
		writer.Write (contents);
	}
}
