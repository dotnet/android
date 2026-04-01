using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests
{
	public static class AssertionExtensions
	{
		[DebuggerHidden]
		public static void AssertTargetIsSkipped (this BuildOutput output, string target, int? occurrence = null, bool defaultIfNotUsed = false)
		{
			if (occurrence != null)
				Assert.IsTrue (output.IsTargetSkipped (target, defaultIfNotUsed), $"The target {target} should have been skipped. ({occurrence})");
			else
				Assert.IsTrue (output.IsTargetSkipped (target, defaultIfNotUsed), $"The target {target} should have been skipped.");
		}

		[DebuggerHidden]
		public static void AssertTargetIsNotSkipped (this BuildOutput output, string target, int? occurrence = null, bool defaultIfNotUsed = false)
		{
			if (occurrence != null)
				Assert.IsFalse (output.IsTargetSkipped (target, defaultIfNotUsed), $"The target {target} should have *not* been skipped. ({occurrence})");
			else
				Assert.IsFalse (output.IsTargetSkipped (target, defaultIfNotUsed), $"The target {target} should have *not* been skipped.");
		}

		[DebuggerHidden]
		public static void AssertTargetIsSkipped (this DotNetCLI dotnet, string target, int? occurrence = null, bool defaultIfNotUsed = false)
		{
			if (occurrence != null)
				Assert.IsTrue (dotnet.IsTargetSkipped (target, defaultIfNotUsed), $"The target {target} should have been skipped. ({occurrence})");
			else
				Assert.IsTrue (dotnet.IsTargetSkipped (target, defaultIfNotUsed), $"The target {target} should have been skipped.");
		}

		[DebuggerHidden]
		public static void AssertTargetIsNotSkipped (this DotNetCLI dotnet, string target, int? occurrence = null, bool defaultIfNotUsed = false)
		{
			if (occurrence != null)
				Assert.IsFalse (dotnet.IsTargetSkipped (target, defaultIfNotUsed), $"The target {target} should have *not* been skipped. ({occurrence})");
			else
				Assert.IsFalse (dotnet.IsTargetSkipped (target, defaultIfNotUsed), $"The target {target} should have *not* been skipped.");
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
			Assert.IsTrue (zip.ContainsEntry (archivePath), $"{zipPath} should contain {archivePath}:\n{string.Join (",\n", zip.Select (e => e.FullName))}");
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
			zip.AssertContainsEntry (zipPath, archivePath);
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
			AssertHasSomeWarnings (builder.LastBuildOutput, 0, builder.BuildLogFile);
		}

		[DebuggerHidden]
		public static void AssertHasSomeWarnings (this ProjectBuilder builder, uint numOfExpectedWarnings)
		{
			AssertHasSomeWarnings (builder.LastBuildOutput, numOfExpectedWarnings, builder.BuildLogFile);
		}

		[DebuggerHidden]
		public static void AssertHasNoWarnings (this DotNetCLI dotnet)
		{
			AssertHasSomeWarnings (dotnet.LastBuildOutput, 0, dotnet.BuildLogFile);
		}

		[DebuggerHidden]
		public static void AssertHasSomeWarnings (this DotNetCLI dotnet, uint numOfExpectedWarnings)
		{
			AssertHasSomeWarnings (dotnet.LastBuildOutput, numOfExpectedWarnings, dotnet.BuildLogFile);
		}

		static void AssertHasSomeWarnings (IEnumerable<string> lastBuildOutput, uint numOfExpectedWarnings, string logFile)
		{
			Assert.IsTrue (StringAssertEx.ContainsText (lastBuildOutput, $" {numOfExpectedWarnings} Warning(s)"), $"{logFile} should have {numOfExpectedWarnings} MSBuild warnings.");
		}
	}
}
