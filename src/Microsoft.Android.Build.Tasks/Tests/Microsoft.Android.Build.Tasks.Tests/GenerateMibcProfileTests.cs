using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Microsoft.Android.Tasks;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class GenerateMibcProfileTests : BaseTest {

		static string InputAssembly => typeof (GenerateMibcProfile).Assembly.Location;

		GenerateMibcProfile CreateTask (string outputFile, string? mainAssembly = null) =>
			new GenerateMibcProfile {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				MainAssembly = mainAssembly ?? InputAssembly,
				OutputFile = outputFile,
			};

		[Test]
		public void Execute_WritesZippedManagedPE ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var outputFile = Path.Combine (path, "test.mibc");

			var task = CreateTask (outputFile);
			Assert.IsTrue (task.Execute (), "Task should succeed.");

			FileAssert.Exists (outputFile);

			// A MIBC file is a zip archive with a single "<file name>.dll" entry.
			using var archive = ZipFile.OpenRead (outputFile);
			Assert.AreEqual (1, archive.Entries.Count, "Expected a single entry.");
			var entry = archive.Entries [0];
			Assert.AreEqual ("test.mibc.dll", entry.Name);
			Assert.AreEqual (new DateTime (1980, 1, 1), entry.LastWriteTime.DateTime);

			var buffer = new MemoryStream ();
			using (var entryStream = entry.Open ())
				entryStream.CopyTo (buffer);
			buffer.Position = 0;

			using var peReader = new PEReader (buffer);
			Assert.IsTrue (peReader.HasMetadata, "The entry should be a managed PE.");
			var reader = peReader.GetMetadataReader ();

			// crossgen2 looks up a global method named "AssemblyDictionary".
			var globalMethods = reader.GetTypeDefinition (reader.TypeDefinitions.First ())
				.GetMethods ()
				.Select (h => reader.GetString (reader.GetMethodDefinition (h).Name))
				.ToArray ();
			CollectionAssert.Contains (globalMethods, "AssemblyDictionary");

			// One group method per assembly, named after the assembly it describes.
			var assemblyName = Path.GetFileNameWithoutExtension (InputAssembly);
			CollectionAssert.Contains (globalMethods, $"Assemblies_{assemblyName}_1");
		}

		[Test]
		public void Execute_IsDeterministic ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var outputFile = Path.Combine (path, "test.mibc");

			Assert.IsTrue (CreateTask (outputFile).Execute (), "First run should succeed.");
			var bytes = File.ReadAllBytes (outputFile);

			Assert.IsTrue (CreateTask (outputFile).Execute (), "Second run should succeed.");
			CollectionAssert.AreEqual (bytes, File.ReadAllBytes (outputFile), "Output should be byte-identical.");
		}

		[Test]
		public void Execute_MissingAssembly_WritesNothing ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var outputFile = Path.Combine (path, "test.mibc");

			var task = CreateTask (outputFile, mainAssembly: Path.Combine (path, "DoesNotExist.dll"));
			Assert.IsTrue (task.Execute (), "Task should succeed.");

			FileAssert.DoesNotExist (outputFile);
		}

		[Test]
		public void Execute_UsesProjectDirectory ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			File.Copy (InputAssembly, Path.Combine (path, "input.dll"));
			var task = CreateTask (Path.Combine ("profiles", "test.mibc"), "input.dll");
			task.TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (path, new Dictionary<string, string> ());

			Assert.IsTrue (task.Execute ());
			FileAssert.Exists (Path.Combine (path, "profiles", "test.mibc"));
		}

		[Test]
		public void Execute_InvalidAssembly_ReportsError ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			string input = Path.Combine (path, "invalid.dll");
			File.WriteAllBytes (input, [0, 1, 2]);
			var errors = new List<BuildErrorEventArgs> ();
			var task = CreateTask (Path.Combine (path, "test.mibc"), input);
			task.BuildEngine = new MockBuildEngine (TestContext.Out, errors: errors);

			Assert.IsFalse (task.Execute ());
			Assert.AreEqual (1, errors.Count);
			Assert.AreEqual ("XAGMP7000", errors [0].Code);
		}

		[Test]
		public void Writer_PreservesMethodCoverage ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			string output = Path.Combine (path, "profile.dll");
			int count = MibcProfileWriter.Write ([typeof (GenerateMibcProfileTests).Assembly.Location], output, TaskEnvironment.Fallback);
			Assert.Greater (count, 0);

			using var stream = File.OpenRead (output);
			using var pe = new PEReader (stream);
			var reader = pe.GetMetadataReader ();
			string [] names = reader.MemberReferences.Select (handle => reader.GetString (reader.GetMemberReference (handle).Name)).ToArray ();
			CollectionAssert.Contains (names, nameof (MethodCoverage.Included));
			CollectionAssert.Contains (names, nameof (MethodCoverage.NativeStub));
			CollectionAssert.DoesNotContain (names, nameof (MethodCoverage.GenericMethod));
			CollectionAssert.DoesNotContain (names, nameof (MethodCoverage.AbstractMethod));
			CollectionAssert.DoesNotContain (names, nameof (MethodCoverage.RuntimeMethod));
			CollectionAssert.DoesNotContain (names, nameof (GenericCoverage<int>.GenericTypeMethod));
		}

		[Test]
		public void Targets_RegisterModernTaskForBothHosts ()
		{
			var targets = XDocument.Load (Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
				"src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.CoreCLR.targets"));
			var registrations = targets.Root.Elements ("UsingTask").ToArray ();
			Assert.AreEqual (2, registrations.Length);
			Assert.That (registrations.Select (r => (string?)r.Attribute ("TaskName")), Is.All.EqualTo ("Microsoft.Android.Tasks.GenerateMibcProfile"));
			Assert.That (registrations.Select (r => (string?)r.Attribute ("AssemblyFile")), Is.All.EqualTo ("$(_MicrosoftAndroidBuildTasksAssembly)"));
			Assert.That (registrations.Select (r => (string?)r.Attribute ("Runtime")), Is.All.EqualTo ("NET"));
			Assert.AreEqual ("TaskHostFactory", (string?)registrations.Single (r => (string?)r.Attribute ("Condition") == " '$(MSBuildRuntimeType)' == 'Full' ").Attribute ("TaskFactory"));
			Assert.IsNull (registrations.Single (r => (string?)r.Attribute ("Condition") == " '$(MSBuildRuntimeType)' == 'Core' ").Attribute ("TaskFactory"));
		}

		[TestCase (false)]
		[TestCase (true)]
		public void Targets_ExecuteAndSkipUnchangedProfile (bool multithreaded)
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			File.Copy (InputAssembly, Path.Combine (path, "input.dll"));
			string targetsPath = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
				"src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.CoreCLR.targets");
			var targets = XDocument.Load (targetsPath);
			string project = Path.Combine (path, "generate.proj");
			new XDocument (new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("_MicrosoftAndroidBuildTasksAssembly", InputAssembly),
					new XElement ("IntermediateOutputPath", "profiles/"),
					new XElement ("TargetName", "input"),
					new XElement ("PublishReadyToRun", "true"),
					new XElement ("_AndroidReadyToRunMainAssembly", "true")),
				new XElement ("ItemGroup", new XElement ("IntermediateAssembly", new XAttribute ("Include", "input.dll"))),
				targets.Root.Elements ("UsingTask"),
				targets.Root.Elements ("Target").Where (t => ((string?)t.Attribute ("Name"))?.StartsWith ("_AndroidGenerateMibcProfile", StringComparison.Ordinal) == true)))
				.Save (project);
			string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
			var arguments = new List<string> {
				"msbuild", project, "-t:_AndroidGenerateMibcProfile", "-m", "-nr:false", "-v:normal",
			};
			if (multithreaded) {
				arguments.Add ("-mt");
				arguments.Add ("-warnaserror:MSB4286,MSB4287,MSB4288");
			}
			var result = NativeAotBootstrapTestTools.Run (dotnet, arguments.ToArray ());
			Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
			string output = Path.Combine (path, "profiles", "input.mibc");
			FileAssert.Exists (output);
			byte [] bytes = File.ReadAllBytes (output);
			DateTime timestamp = DateTime.UtcNow.AddMinutes (1);
			File.SetLastWriteTimeUtc (output, timestamp);
			timestamp = File.GetLastWriteTimeUtc (output);

			result = NativeAotBootstrapTestTools.Run (dotnet, arguments.ToArray ());
			Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
			StringAssert.Contains ("Skipping target \"_AndroidGenerateMibcProfile\"", result.Output);
			Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (output));
			CollectionAssert.AreEqual (bytes, File.ReadAllBytes (output));
		}

		abstract class MethodCoverage
		{
			public void Included () { }
			public void GenericMethod<T> () { }
			public abstract void AbstractMethod ();
			[System.Runtime.CompilerServices.MethodImpl (System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
			public extern void RuntimeMethod ();
			[System.Runtime.InteropServices.DllImport ("unused")]
			public static extern void NativeStub ();
		}

		class GenericCoverage<T>
		{
			public void GenericTypeMethod () { }
		}
	}
}
