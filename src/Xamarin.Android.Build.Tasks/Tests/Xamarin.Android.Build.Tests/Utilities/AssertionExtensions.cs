using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests
{
	public static class AssertionExtensions
	{
		[DebuggerHidden]
		public static void AssertTargetIsSkipped (this BuildOutput output, string target, int? occurrence = null)
		{
			if (occurrence != null)
				Assert.IsTrue (output.IsTargetSkipped (target), $"The target {target} should have been skipped. ({occurrence})");
			else
				Assert.IsTrue (output.IsTargetSkipped (target), $"The target {target} should have been skipped.");
		}

		[DebuggerHidden]
		public static void AssertTargetIsNotSkipped (this BuildOutput output, string target, int? occurrence = null)
		{
			if (occurrence != null)
				Assert.IsFalse (output.IsTargetSkipped (target), $"The target {target} should have *not* been skipped. ({occurrence})");
			else
				Assert.IsFalse (output.IsTargetSkipped (target), $"The target {target} should have *not* been skipped.");
		}

		[DebuggerHidden]
		public static void AssertTargetIsSkipped (this DotNetCLI dotnet, string target, int? occurrence = null)
		{
			if (occurrence != null)
				Assert.IsTrue (dotnet.IsTargetSkipped (target), $"The target {target} should have been skipped. ({occurrence})");
			else
				Assert.IsTrue (dotnet.IsTargetSkipped (target), $"The target {target} should have been skipped.");
		}

		[DebuggerHidden]
		public static void AssertTargetIsNotSkipped (this DotNetCLI dotnet, string target, int? occurrence = null)
		{
			if (occurrence != null)
				Assert.IsFalse (dotnet.IsTargetSkipped (target), $"The target {target} should have *not* been skipped. ({occurrence})");
			else
				Assert.IsFalse (dotnet.IsTargetSkipped (target), $"The target {target} should have *not* been skipped.");
		}

		[DebuggerHidden]
		public static void AssertTargetIsPartiallyBuilt (this BuildOutput output, string target, int? occurrence = null)
		{
			if (occurrence != null)
				Assert.IsTrue (output.IsTargetPartiallyBuilt (target), $"The target {target} should have been partially built. ({occurrence})");
			else
				Assert.IsTrue (output.IsTargetPartiallyBuilt (target), $"The target {target} should have been partially built.");
		}

		[DebuggerHidden]
		public static void AssertEntryEquals (this ZipArchive zip, string zipPath, string archivePath, string expected)
		{
			zip.AssertContainsEntry (zipPath, archivePath);

			var entry = zip.ReadEntry (archivePath);
			using var stream = new MemoryStream ();
			entry.Extract (stream);
			stream.Position = 0;
			using var reader = new StreamReader (stream);
			Assert.AreEqual (expected, reader.ReadToEnd ().Trim ());
		}

		[DebuggerHidden]
		public static void AssertContainsEntry (this ZipArchive zip, string zipPath, string archivePath)
		{
			Assert.IsTrue (zip.ContainsEntry (archivePath), $"{zipPath} should contain {archivePath}");
		}

		[DebuggerHidden]
		public static void AssertContainsEntry (this ArchiveAssemblyHelper helper, string archivePath)
		{
			Assert.IsTrue (helper.Exists (archivePath), $"{helper.ArchivePath} should contain {archivePath}");
		}

		[DebuggerHidden]
		public static void AssertDoesNotContainEntry (this ZipArchive zip, string zipPath, string archivePath)
		{
			Assert.IsFalse (zip.ContainsEntry (archivePath), $"{zipPath} should *not* contain {archivePath}");
		}

		[DebuggerHidden]
		public static void AssertDoesNotContainEntry (this ArchiveAssemblyHelper helper, string archivePath)
		{
			Assert.IsFalse (helper.Exists (archivePath), $"{helper.ArchivePath} should *not* contain {archivePath}");
		}

		[DebuggerHidden]
		public static void AssertContainsEntry (this ZipArchive zip, string zipPath, string archivePath, bool shouldContainEntry)
		{
			if (shouldContainEntry) {
				zip.AssertContainsEntry (zipPath, archivePath);
			} else {
				zip.AssertDoesNotContainEntry (zipPath, archivePath);
			}
		}

		[DebuggerHidden]
		public static void AssertContainsEntry (this ArchiveAssemblyHelper helper, string archivePath, bool shouldContainEntry)
		{
			if (shouldContainEntry) {
				helper.AssertContainsEntry (archivePath);
			} else {
				helper.AssertDoesNotContainEntry (archivePath);
			}
		}

		[DebuggerHidden]
		public static void AssertEntryContents (this ZipArchive zip, string zipPath, string archivePath, string contents)
		{
			var entry = zip.ReadEntry (archivePath);
			Assert.IsNotNull (entry, $"{zipPath} should contain {archivePath}");
			using (var stream = new MemoryStream ())
			using (var reader = new StreamReader (stream)) {
				entry.Extract (stream);
				stream.Position = 0;
				var actual = reader.ReadToEnd ();
				Assert.AreEqual (contents, actual, $"{archivePath} should contain {contents}");
			}
		}

		[DebuggerHidden]
		public static void AssertHasNoWarnings (this ProjectBuilder builder)
		{
			Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, " 0 Warning(s)"), $"{builder.BuildLogFile} should have no MSBuild warnings.");
		}

		[DebuggerHidden]
		public static void AssertHasNoWarnings (this DotNetCLI dotnet)
		{
			Assert.IsTrue (StringAssertEx.ContainsText (dotnet.LastBuildOutput, " 0 Warning(s)"), $"{dotnet.BuildLogFile} should have no MSBuild warnings.");
		}
	}
}
