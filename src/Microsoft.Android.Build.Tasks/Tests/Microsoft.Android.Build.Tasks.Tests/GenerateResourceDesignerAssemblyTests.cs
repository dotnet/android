using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Xml.Linq;
using Microsoft.Android.Tasks;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable (ParallelScope.Children)]
public class GenerateResourceDesignerAssemblyTests : BaseTest
{
	const string DesignerNamespace = "_Microsoft.Android.Resource.Designer";
	const string Resources = """
		int string app_name 0x7f010001
		int anim fade_in 0x7f020001
		int attr custom_attr 0x7f030001
		int bool enabled 0x7f040001
		int dimen spacing 0x7f050001
		int customtype custom_value 0x7f060001
		int id negative 0xffffffff
		int string name.with.dot 0x7f010002
		int[] styleable TestView { 0x01010001, 0x7f030001, 0xffffffff }
		int styleable TestView_text 0
		int styleable TestView_enabled 1
		int[] styleable AnotherView { 0x7f030001 }
		""";
	string DirectoryPath => Path.Combine (Root, "temp", TestName);
	string OutputPath => Path.Combine (DirectoryPath, "_Microsoft.Android.Resource.Designer.dll");
	string RtxtPath => Path.Combine (DirectoryPath, "R.txt");

	[SetUp]
	public void SetUp ()
	{
		Directory.CreateDirectory (DirectoryPath);
		File.WriteAllText (RtxtPath, Resources);
	}

	GenerateResourceDesignerAssembly CreateTask (bool deterministic = true, bool isApplication = true) => new () {
		BuildEngine = new MockBuildEngine (TestContext.Out),
		RTxtFile = new TaskItem (RtxtPath),
		OutputFile = new TaskItem (OutputPath),
		CaseMapFile = Path.Combine (DirectoryPath, "case_map.txt"),
		ProjectDir = DirectoryPath,
		ResourceDirectory = Path.Combine (DirectoryPath, "res"),
		Deterministic = deterministic,
		IsApplication = isApplication,
		AssemblyName = "TestApp",
	};

	[TestCase (true, true)]
	[TestCase (true, false)]
	[TestCase (false, true)]
	[TestCase (false, false)]
	public void DeterministicImage (bool deterministic, bool isApplication)
	{
		var start = DateTimeOffset.UtcNow.ToUnixTimeSeconds ();
		Assert.IsTrue (CreateTask (deterministic, isApplication).Execute ());
		var first = File.ReadAllBytes (OutputPath);
		var firstMvid = ReadMvid ();
		File.Delete (OutputPath);
		Assert.IsTrue (CreateTask (deterministic, isApplication).Execute ());
		if (deterministic) {
			CollectionAssert.AreEqual (first, File.ReadAllBytes (OutputPath));
			Assert.AreEqual (firstMvid, ReadMvid ());
		} else {
			Assert.AreNotEqual (firstMvid, ReadMvid ());
		}
		using var stream = File.OpenRead (OutputPath);
		using var pe = new PEReader (stream);
		if (deterministic)
			Assert.AreEqual (0, pe.PEHeaders.CoffHeader.TimeDateStamp);
		else
			Assert.That ((long) pe.PEHeaders.CoffHeader.TimeDateStamp, Is.InRange (start, DateTimeOffset.UtcNow.ToUnixTimeSeconds ()));
		Assert.IsFalse (pe.PEHeaders.CorHeader?.Flags.HasFlag (CorFlags.StrongNameSigned));
		Assert.That (pe.PEHeaders.CorHeader?.StrongNameSignatureDirectory.Size, Is.EqualTo (128));
	}

	Guid ReadMvid ()
	{
		using var stream = File.OpenRead (OutputPath);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		return reader.GetGuid (reader.GetModuleDefinition ().Mvid);
	}

	[Test]
	public void ApplicationPropertiesAndConstants ()
	{
		Assert.IsTrue (CreateTask ().Execute ());
		var context = new AssemblyLoadContext (TestName, isCollectible: true);
		try {
			var assembly = context.LoadFromAssemblyPath (OutputPath);
			Assert.AreEqual (new Version (1, 0, 0, 0), assembly.GetName ().Version);
			Assert.AreEqual (8, assembly.GetName ().GetPublicKeyToken ()?.Length);
			var propertyType = assembly.GetType ($"{DesignerNamespace}.Resource+String", throwOnError: true)
				?? throw new InvalidOperationException ();
			var constantType = assembly.GetType ($"{DesignerNamespace}.ResourceConstant+String", throwOnError: true)
				?? throw new InvalidOperationException ();
			Assert.AreEqual (0x7f010001, propertyType.GetProperty ("app_name")?.GetValue (null));
			Assert.AreEqual (0x7f010001, constantType.GetField ("app_name")?.GetRawConstantValue ());
			Assert.AreEqual (0x7f010002, propertyType.GetProperty ("name_with_dot")?.GetValue (null));
			Assert.IsNotNull (Activator.CreateInstance (propertyType));
			foreach (var name in new [] { "Animation", "Attribute", "Boolean", "Dimension", "Customtype" })
				Assert.IsNotNull (assembly.GetType ($"{DesignerNamespace}.Resource+{name}"));
			Assert.AreEqual (-1, assembly.GetType ($"{DesignerNamespace}.Resource+Id")?.GetProperty ("negative")?.GetValue (null));

			var styleable = assembly.GetType ($"{DesignerNamespace}.Resource+Styleable") ?? throw new InvalidOperationException ();
			var property = styleable.GetProperty ("TestView") ?? throw new InvalidOperationException ();
			var first = (int []) (property.GetValue (null) ?? throw new InvalidOperationException ());
			var second = (int []) (property.GetValue (null) ?? throw new InvalidOperationException ());
			CollectionAssert.AreEqual (new [] { 0x01010001, 0x7f030001, -1 }, first);
			Assert.AreNotSame (first, second, "Each array getter must allocate a new array.");
			var constants = assembly.GetType ($"{DesignerNamespace}.ResourceConstant+Styleable") ?? throw new InvalidOperationException ();
			var field = constants.GetField ("TestView") ?? throw new InvalidOperationException ();
			CollectionAssert.AreEqual (first, (int []) (field.GetValue (null) ?? throw new InvalidOperationException ()));
			Assert.AreSame (field.GetValue (null), field.GetValue (null), "Constant arrays must be initialized only once.");
			CollectionAssert.AreEqual (new [] { 0x7f030001 }, (int []) (constants.GetField ("AnotherView")?.GetValue (null) ?? throw new InvalidOperationException ()));
			Assert.AreEqual (1, styleable.GetProperty ("TestView_enabled")?.GetValue (null));
			Assert.IsFalse (constants.Attributes.HasFlag (TypeAttributes.BeforeFieldInit));
			var resource = assembly.GetType ($"{DesignerNamespace}.Resource") ?? throw new InvalidOperationException ();
			Assert.AreEqual (System.ComponentModel.EditorBrowsableState.Never,
				resource.GetCustomAttribute<System.ComponentModel.EditorBrowsableAttribute> ()?.State);
			Assert.AreEqual (nameof (GenerateResourceDesignerAssembly),
				resource.GetCustomAttribute<System.CodeDom.Compiler.GeneratedCodeAttribute> ()?.Tool);
			Assert.AreEqual ("TestApp", assembly.GetCustomAttribute<System.Runtime.CompilerServices.InternalsVisibleToAttribute> ()?.AssemblyName);
			Assert.AreEqual (".NETStandard,Version=v2.1", assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute> ()?.FrameworkName);
		} finally {
			context.Unload ();
		}
	}

	[Test]
	public void LibraryIsReferenceAssembly ()
	{
		Assert.IsTrue (CreateTask (isApplication: false).Execute ());
		using var stream = File.OpenRead (OutputPath);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		var types = reader.TypeDefinitions.Select (handle => reader.GetString (reader.GetTypeDefinition (handle).Name)).ToArray ();
		Assert.Contains ("Resource", types);
		Assert.IsFalse (types.Contains ("ResourceConstant"));
		var attributes = reader.GetAssemblyDefinition ().GetCustomAttributes ().Select (handle => {
			var constructor = reader.GetMemberReference ((MemberReferenceHandle) reader.GetCustomAttribute (handle).Constructor);
			return reader.GetString (reader.GetTypeReference ((TypeReferenceHandle) constructor.Parent).Name);
		}).ToArray ();
		Assert.Contains ("ReferenceAssemblyAttribute", attributes);
		Assert.IsFalse (attributes.Contains ("InternalsVisibleToAttribute"));
	}

	[Test]
	public void RemappingAndUnchangedOutput ()
	{
		File.WriteAllText (Path.Combine (DirectoryPath, "case_map.txt"),
			$"string{Path.DirectorySeparatorChar}app_name;string{Path.DirectorySeparatorChar}AppName\n");
		Assert.IsTrue (CreateTask ().Execute ());
		var original = File.ReadAllBytes (OutputPath);
		var originalMvid = ReadMvid ();
		var lastWrite = File.GetLastWriteTimeUtc (OutputPath);
		// Force emission rather than the task's timestamp fast path.
		File.SetLastWriteTimeUtc (RtxtPath, lastWrite.AddSeconds (1));
		Assert.IsTrue (CreateTask ().Execute ());
		CollectionAssert.AreEqual (original, File.ReadAllBytes (OutputPath));
		Assert.AreEqual (lastWrite, File.GetLastWriteTimeUtc (OutputPath));
		using var stream = File.OpenRead (OutputPath);
		using var pe = new PEReader (stream);
		var reader = pe.GetMetadataReader ();
		Assert.IsTrue (reader.PropertyDefinitions.Any (handle => reader.GetString (reader.GetPropertyDefinition (handle).Name) == "AppName"));
		File.AppendAllText (RtxtPath, "\nint string added 0x7f010003\n");
		File.SetLastWriteTimeUtc (RtxtPath, lastWrite.AddSeconds (1));
		Assert.IsTrue (CreateTask ().Execute ());
		CollectionAssert.AreNotEqual (original, File.ReadAllBytes (OutputPath));
		Assert.AreNotEqual (originalMvid, ReadMvid ());
	}

	[Test]
	public void TaskCanBeReused ()
	{
		var task = CreateTask ();
		Assert.IsTrue (task.Execute ());
		var original = File.ReadAllBytes (OutputPath);
		File.Delete (OutputPath);
		Assert.IsTrue (task.Execute ());
		CollectionAssert.AreEqual (original, File.ReadAllBytes (OutputPath));
	}

	[Test]
	public void MissingRtxtProducesEmptyDesigner ()
	{
		File.Delete (RtxtPath);
		Assert.IsTrue (CreateTask ().Execute ());
		using var stream = File.OpenRead (OutputPath);
		using var pe = new PEReader (stream);
		Assert.AreEqual (3, pe.GetMetadataReader ().TypeDefinitions.Count);
	}

	[Test]
	public void RealResourceSet ()
	{
		using (var input = typeof (GenerateResourceDesignerAssemblyTests).Assembly.GetManifestResourceStream ("ResourceDesigner.R.txt")
			?? throw new InvalidOperationException ("The resource designer test data is missing."))
		using (var output = File.Create (RtxtPath))
			input.CopyTo (output);
		var task = CreateTask ();
		task.BuildEngine = new MockBuildEngine (TextWriter.Null);
		Assert.IsTrue (task.Execute ());
		var parsed = new Xamarin.Android.Tasks.RtxtParser ().Parse (RtxtPath, task.Log, new System.Collections.Generic.Dictionary<string, string> ());
		var context = new AssemblyLoadContext (TestName, isCollectible: true);
		try {
			var assembly = context.LoadFromAssemblyPath (OutputPath);
			int count = 0;
			foreach (var resource in parsed) {
				if (resource.ResourceTypeName == null || resource.Identifier == null)
					continue;
				string typeName = resource.ResourceTypeName switch {
					"anim" => "Animation",
					"attr" => "Attribute",
					"bool" => "Boolean",
					"dimen" => "Dimension",
					_ => char.ToUpperInvariant (resource.ResourceTypeName [0]) + resource.ResourceTypeName.Substring (1),
				};
				var properties = assembly.GetType ($"{DesignerNamespace}.Resource+{typeName}") ?? throw new InvalidOperationException ();
				var constants = assembly.GetType ($"{DesignerNamespace}.ResourceConstant+{typeName}") ?? throw new InvalidOperationException ();
				var propertyValue = properties.GetProperty (resource.Identifier)?.GetValue (null);
				var fieldValue = constants.GetField (resource.Identifier)?.GetValue (null);
				if (resource.Type == Xamarin.Android.Tasks.RType.Array) {
					CollectionAssert.AreEqual (resource.Ids, (int []) (propertyValue ?? throw new InvalidOperationException ()));
					CollectionAssert.AreEqual (resource.Ids, (int []) (fieldValue ?? throw new InvalidOperationException ()));
				} else {
					Assert.AreEqual (resource.Id, propertyValue, resource.Key);
					Assert.AreEqual (resource.Id, fieldValue, resource.Key);
				}
				count++;
			}
			Assert.That (count, Is.GreaterThan (6000));
		} finally {
			context.Unload ();
		}
	}

	[TestCase ("Xamarin.Android.Common.targets")]
	[TestCase ("MSBuild/Xamarin/Android/Xamarin.Android.Resource.Designer.targets")]
	public void MetadataTaskRegistration (string relativePath)
	{
		var repository = new DirectoryInfo (AppContext.BaseDirectory);
		while (repository != null && !File.Exists (Path.Combine (repository.FullName, "Configuration.props")))
			repository = repository.Parent;
		if (repository == null)
			throw new InvalidOperationException ("Could not locate the repository.");
		var source = XDocument.Load (Path.Combine (repository.FullName, "src", "Xamarin.Android.Build.Tasks", relativePath));
		var registrations = source.Descendants ().Where (element => element.Name.LocalName == "UsingTask" &&
			((string?) element.Attribute ("TaskName"))?.EndsWith (".GenerateResourceDesignerAssembly", StringComparison.Ordinal) == true)
			.Select (element => new XElement (element)).ToArray ();
		Assert.AreEqual (2, registrations.Length);
		foreach (var registration in registrations) {
			registration.Name = "UsingTask";
			Assert.IsNull (registration.Attribute ("TaskFactory"));
			Assert.IsNull (registration.Attribute ("Runtime"));
		}
		var projectPath = Path.Combine (DirectoryPath, "registration.proj");
		new XDocument (new XElement ("Project",
			new XElement ("PropertyGroup",
				new XElement ("_AndroidUseMetadataResourceDesigner", "true"),
				new XElement ("_MicrosoftAndroidBuildTasksAssembly", typeof (GenerateResourceDesignerAssembly).Assembly.Location)),
			registrations,
			new XElement ("Target", new XAttribute ("Name", "Build"),
				new XElement ("GenerateResourceDesignerAssembly",
					new XAttribute ("RTxtFile", RtxtPath),
					new XAttribute ("OutputFile", OutputPath),
					new XAttribute ("CaseMapFile", Path.Combine (DirectoryPath, "case_map.txt")),
					new XAttribute ("ResourceDirectory", DirectoryPath),
					new XAttribute ("ProjectDir", DirectoryPath),
					new XAttribute ("TargetFrameworkVersion", "v11.0"),
					new XAttribute ("TargetFrameworkIdentifier", ".NETCoreApp"),
					new XAttribute ("Resources", RtxtPath),
					new XAttribute ("IsApplication", "true"),
					new XAttribute ("DesignTimeBuild", "false"),
					new XAttribute ("Deterministic", "true")))))
			.Save (projectPath);
		var start = new ProcessStartInfo ("dotnet") {
			WorkingDirectory = DirectoryPath,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		start.Environment ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
		foreach (var argument in new [] { "msbuild", projectPath, "-t:Build", "-nologo", "-v:minimal", "-nr:false" })
			start.ArgumentList.Add (argument);
		using var process = Process.Start (start) ?? throw new InvalidOperationException ("Could not start MSBuild.");
		var stdout = process.StandardOutput.ReadToEndAsync ();
		var stderr = process.StandardError.ReadToEndAsync ();
		bool completed = process.WaitForExit (120000);
		if (!completed) {
			process.Kill (entireProcessTree: true);
			process.WaitForExit ();
		}
		var output = stdout.GetAwaiter ().GetResult () + stderr.GetAwaiter ().GetResult ();
		Assert.IsTrue (completed, output);
		Assert.AreEqual (0, process.ExitCode, output);
		Assert.IsTrue (File.Exists (OutputPath));
	}

	[Test]
	public void TaskAssemblyDoesNotReferenceCecil ()
	{
		Assert.IsFalse (typeof (GenerateResourceDesignerAssembly).Assembly.GetReferencedAssemblies ()
			.Any (assembly => assembly.Name?.Contains ("Cecil", StringComparison.OrdinalIgnoreCase) == true));
	}
}
