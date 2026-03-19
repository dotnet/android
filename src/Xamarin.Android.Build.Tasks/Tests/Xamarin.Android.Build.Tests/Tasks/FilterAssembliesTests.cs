using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;
using Xamarin.Tools.Zip;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class FilterAssembliesTests : BaseTest
	{
		string tempDirectory;

		[SetUp]
		public void Setup ()
		{
			tempDirectory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			Directory.CreateDirectory (tempDirectory);
		}

		[TearDown]
		public void TearDown ()
		{
			Directory.Delete (tempDirectory, recursive: true);
		}

		Task<string> DownloadFromNuGet (string url, string filename = "") =>
			Task.Factory.StartNew (() => new DownloadedCache ().GetAsFile (url, filename));

		async Task<string []> GetAssembliesFromNuGet (string url, string filename, string path)
		{
			var assemblies = new List<string> ();
			var nuget = await DownloadFromNuGet (url, filename);
			using (var zip = ZipArchive.Open (nuget, FileMode.Open)) {
				foreach (var entry in zip) {
					if (entry.FullName.StartsWith (path, StringComparison.OrdinalIgnoreCase) &&
						entry.FullName.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
						var temp = Path.Combine (tempDirectory, Path.GetFileName (entry.NativeFullName));
						assemblies.Add (temp);
						using (var fileStream = File.Create (temp)) {
							entry.Extract (fileStream);
						}
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
				"https://www.nuget.org/api/v2/package/Refractored.Controls.CircleImageView/1.0.1",
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
				"https://www.nuget.org/api/v2/package/Xamarin.Forms/3.6.0.220655",
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
				"https://www.nuget.org/api/v2/package/Xamarin.Google.Guava.ListenableFuture/1.0.0",
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
