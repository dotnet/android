using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;
using System.IO.Compression;
using System.Xml.Linq;
using Mono.Cecil;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class FilterAssembliesTests : BaseTest
	{
		const string DotNetPublicPackageBaseAddress = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/flat2";

		string tempDirectory;

		[SetUp]
		public void Setup ()
		{
			tempDirectory = Path.Combine (Root, "temp", TestName, "nuget");
			Directory.CreateDirectory (tempDirectory);
		}

		[TearDown]
		public void TearDown ()
		{
			Directory.Delete (tempDirectory, recursive: true);
		}

		Task<string> DownloadFromNuGet (string url, string filename = "") =>
			Task.Factory.StartNew (() => new DownloadedCache ().GetAsFile (url, filename));

		static string GetPackageUrl (string packageId, string version)
		{
			var lowercaseId = packageId.ToLowerInvariant ();
			var lowercaseVersion = version.ToLowerInvariant ();
			return $"{DotNetPublicPackageBaseAddress}/{lowercaseId}/{lowercaseVersion}/{lowercaseId}.{lowercaseVersion}.nupkg";
		}

		[Test]
		public void PackageUrlIsLowercase ()
		{
			var actual = GetPackageUrl ("Package.ID", "1.0.0-RC1");
			Assert.AreEqual ($"{DotNetPublicPackageBaseAddress}/package.id/1.0.0-rc1/package.id.1.0.0-rc1.nupkg", actual);
		}

		async Task<string []> GetAssembliesFromNuGet (string url, string filename, string path)
		{
			var assemblies = new List<string> ();
			var nuget = await DownloadFromNuGet (url, filename);
			using (var zip = ZipFile.OpenRead (nuget)) {
				foreach (var entry in zip.Entries) {
					if (entry.FullName.StartsWith (path, StringComparison.OrdinalIgnoreCase) &&
						entry.FullName.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
						var temp = Path.Combine (tempDirectory, entry.Name);
						assemblies.Add (temp);
						entry.ExtractToFile (temp, overwrite: true);
					}
				}
			}
			return assemblies.ToArray ();
		}

		string [] Run (params string [] assemblies)
		{
			var task = new FilterAssemblies {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				InputAssemblies = assemblies.Select (a => new TaskItem (a)).ToArray (),
			};
			Assert.IsTrue (task.Execute (), "task.Execute() should have succeeded.");
			return task.OutputAssemblies.Select (a => Path.GetFileName (a.ItemSpec)).ToArray ();
		}

		[Test]
		public async Task CircleImageView ()
		{
			var assemblies = await GetAssembliesFromNuGet (
				GetPackageUrl ("Refractored.Controls.CircleImageView", "1.0.1"),
				"Refractored.Controls.CircleImageView.1.0.1.nupkg",
				"lib/MonoAndroid10/");
			var actual = Run (assemblies);
			var expected = new [] { "Refractored.Controls.CircleImageView.dll" };
			CollectionAssert.AreEqual (expected, actual);
		}

		[Test]
		public async Task XamarinForms ()
		{
			var assemblies = await GetAssembliesFromNuGet (
				GetPackageUrl ("Xamarin.Forms", "3.6.0.220655"),
				"Xamarin.Forms.3.6.0.220655.nupkg",
				"lib/MonoAndroid90/");
			var actual = Run (assemblies);
			var expected = new [] {
				"FormsViewGroup.dll",
				"Xamarin.Forms.Platform.Android.dll",
				"Xamarin.Forms.Platform.dll",
			};
			CollectionAssert.AreEqual (expected, actual);
		}

		[Test]
		public async Task GuavaListenableFuture ()
		{
			var assemblies = await GetAssembliesFromNuGet (
				GetPackageUrl ("Xamarin.Google.Guava.ListenableFuture", "1.0.0"),
				"Xamarin.Google.Guava.ListenableFuture.1.0.0.nupkg",
				"lib/MonoAndroid50/");
			var actual = Run (assemblies);
			var expected = new [] { "Xamarin.Google.Guava.ListenableFuture.dll" };
			CollectionAssert.AreEqual (expected, actual);
		}

		[Test]
		public void NativeDll_Skipped ()
		{
			// Create a minimal valid PE file without CLI metadata (a native DLL)
			var nativeDll = Path.Combine (tempDirectory, "native.dll");
			CreateNativePE (nativeDll);
			var actual = Run (nativeDll);
			CollectionAssert.IsEmpty (actual, "Native DLLs without CLI metadata should be skipped.");
		}

		static void CreateAssembly (string path, bool android)
		{
			Directory.CreateDirectory (Path.GetDirectoryName (path));
			using var assembly = AssemblyDefinition.CreateAssembly (
				new AssemblyNameDefinition ("Library", new Version (1, 0)), "Library", ModuleKind.Dll);
			if (android) {
				assembly.MainModule.Resources.Add (new EmbeddedResource ("library.jar", ManifestResourceAttributes.Private, new byte [] { 1 }));
			}
			assembly.Write (path);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void ConcurrentProjectsPreserveRelativeItems (bool multithreaded)
		{
			string assembly = Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "Xamarin.Android.Build.Tasks.dll");
			var resources = new List<ProjectResource> ();
			foreach (string name in new [] { "first", "second" }) {
				string root = Path.Combine (tempDirectory, name);
				CreateAssembly (Path.Combine (root, "library.dll"), name == "first");
				CreateAssembly (Path.Combine (root, "other.dll"), name == "second");
				var project = new XElement ("Project",
					new XElement ("UsingTask", new XAttribute ("TaskName", "Xamarin.Android.Tasks.FilterAssemblies"), new XAttribute ("AssemblyFile", assembly)),
					new XElement ("ItemGroup",
						new XElement ("Input", new XAttribute ("Include", "library.dll;other.dll;library.dll"),
							new XElement ("Owner", name))),
					new XElement ("Target", new XAttribute ("Name", "Build"),
						new XElement ("FilterAssemblies", new XAttribute ("InputAssemblies", "@(Input)"),
							new XElement ("Output", new XAttribute ("TaskParameter", "OutputAssemblies"), new XAttribute ("ItemName", "_Filtered"))),
						new XElement ("FilterAssemblies", new XAttribute ("InputAssemblies", "@(Input)"),
							new XElement ("Output", new XAttribute ("TaskParameter", "OutputAssemblies"), new XAttribute ("ItemName", "_Cached"))),
						new XElement ("WriteLinesToFile",
							new XAttribute ("File", "$(MSBuildProjectDirectory)/output.txt"),
							new XAttribute ("Overwrite", "true"),
							new XAttribute ("Lines", "@(_Filtered->'%(Identity)|%(FullPath)|%(Owner)');@(_Cached->'%(Identity)|%(FullPath)|%(Owner)')"))));
				resources.Add (new ProjectResource { Path = Path.Combine (name, "filter.proj"), Content = project.ToString () });
			}
			var driver = new XElement ("Project",
				new XElement ("ItemGroup",
					new XElement ("Reader", new XAttribute ("Include", "first/filter.proj;second/filter.proj"))),
				new XElement ("Target", new XAttribute ("Name", "Build"),
					new XElement ("MSBuild", new XAttribute ("Projects", "@(Reader)"), new XAttribute ("BuildInParallel", "true"), new XAttribute ("Targets", "Build"))));
			resources.Add (new ProjectResource { Path = "driver.proj", Content = driver.ToString () });
			new XamarinAndroidApplicationProject ().UpdateProjectFiles (tempDirectory, resources, doNotCleanup: true);
			var dotnet = new DotNetCLI (Path.Combine (tempDirectory, "driver.proj")) { Verbosity = "diagnostic" };
			dotnet.EnvironmentVariables ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
			dotnet.EnvironmentVariables ["MSBUILDDISABLENODEREUSE"] = "1";
			string [] arguments = multithreaded ?
				["--no-restore", "-m:2", "-mt", "-nr:false", "-warnaserror:MSB4286,MSB4287,MSB4288"] :
				["--no-restore", "-m:2", "-nr:false"];
			Assert.IsTrue (dotnet.Build (msbuildArguments: arguments),
				string.Join (Environment.NewLine, dotnet.LastBuildOutput) + File.ReadAllText (dotnet.ProcessLogFile));

			foreach (string name in new [] { "first", "second" }) {
				string file = name == "first" ? "library.dll" : "other.dll";
				string line = $"{file}|{Path.Combine (tempDirectory, name, file)}|{name}";
				CollectionAssert.AreEqual (Enumerable.Repeat (line, name == "first" ? 4 : 2),
					File.ReadAllLines (Path.Combine (tempDirectory, name, "output.txt")));
			}
		}

		/// <summary>
		/// Creates a minimal valid PE file without CLI metadata, simulating a native DLL.
		/// </summary>
		static void CreateNativePE (string path)
		{
			using var fs = File.Create (path);
			using var writer = new BinaryWriter (fs);

			// DOS header: 'MZ' magic + padding to e_lfanew at offset 0x3C
			writer.Write ((ushort) 0x5A4D);         // e_magic = 'MZ'
			writer.Write (new byte [58]);            // pad to offset 0x3C
			writer.Write ((uint) 0x80);              // e_lfanew = 0x80

			// Pad to PE signature at offset 0x80
			writer.Write (new byte [0x80 - 0x40]);

			// PE signature: 'PE\0\0'
			writer.Write ((uint) 0x00004550);

			// COFF header (20 bytes)
			writer.Write ((ushort) 0x14C);           // Machine = IMAGE_FILE_MACHINE_I386
			writer.Write ((ushort) 0);               // NumberOfSections = 0
			writer.Write ((uint) 0);                 // TimeDateStamp
			writer.Write ((uint) 0);                 // PointerToSymbolTable
			writer.Write ((uint) 0);                 // NumberOfSymbols
			writer.Write ((ushort) 0xE0);            // SizeOfOptionalHeader (PE32)
			writer.Write ((ushort) 0x2102);          // Characteristics = DLL | EXECUTABLE_IMAGE | LARGE_ADDRESS_AWARE

			// Optional header (PE32) — minimal, no CLI header directory entry
			writer.Write ((ushort) 0x10B);           // Magic = PE32
			writer.Write ((byte) 0);                 // MajorLinkerVersion
			writer.Write ((byte) 0);                 // MinorLinkerVersion
			writer.Write ((uint) 0);                 // SizeOfCode
			writer.Write ((uint) 0);                 // SizeOfInitializedData
			writer.Write ((uint) 0);                 // SizeOfUninitializedData
			writer.Write ((uint) 0);                 // AddressOfEntryPoint
			writer.Write ((uint) 0);                 // BaseOfCode
			writer.Write ((uint) 0);                 // BaseOfData
			writer.Write ((uint) 0x10000);           // ImageBase
			writer.Write ((uint) 0x1000);            // SectionAlignment
			writer.Write ((uint) 0x200);             // FileAlignment
			writer.Write ((ushort) 4);               // MajorOperatingSystemVersion
			writer.Write ((ushort) 0);               // MinorOperatingSystemVersion
			writer.Write ((ushort) 0);               // MajorImageVersion
			writer.Write ((ushort) 0);               // MinorImageVersion
			writer.Write ((ushort) 4);               // MajorSubsystemVersion
			writer.Write ((ushort) 0);               // MinorSubsystemVersion
			writer.Write ((uint) 0);                 // Win32VersionValue
			writer.Write ((uint) 0x1000);            // SizeOfImage
			writer.Write ((uint) 0x200);             // SizeOfHeaders
			writer.Write ((uint) 0);                 // CheckSum
			writer.Write ((ushort) 3);               // Subsystem = WINDOWS_CUI
			writer.Write ((ushort) 0);               // DllCharacteristics
			writer.Write ((uint) 0x100000);          // SizeOfStackReserve
			writer.Write ((uint) 0x1000);            // SizeOfStackCommit
			writer.Write ((uint) 0x100000);          // SizeOfHeapReserve
			writer.Write ((uint) 0x1000);            // SizeOfHeapCommit
			writer.Write ((uint) 0);                 // LoaderFlags
			writer.Write ((uint) 16);                // NumberOfRvaAndSizes
			// Data directories (16 entries × 8 bytes = 128 bytes), all zeroed — no CLI header
			writer.Write (new byte [128]);
		}
	}
}
