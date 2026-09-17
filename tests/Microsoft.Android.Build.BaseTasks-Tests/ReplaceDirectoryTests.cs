using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.BaseTasks.Tests.Utilities;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tools.BootstrapTasks;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestFixture]
	public class ReplaceDirectoryTests
	{
		string tempDirectory;
		List<BuildErrorEventArgs> errors;
		List<BuildWarningEventArgs> warnings;
		List<BuildMessageEventArgs> messages;

		[SetUp]
		public void SetUp ()
		{
			tempDirectory = Path.Combine (Path.GetTempPath (), $"{nameof (ReplaceDirectoryTests)}-{Guid.NewGuid ():N}");
			Directory.CreateDirectory (tempDirectory);
			errors = new List<BuildErrorEventArgs> ();
			warnings = new List<BuildWarningEventArgs> ();
			messages = new List<BuildMessageEventArgs> ();
		}

		[TearDown]
		public void TearDown ()
		{
			if (Directory.Exists (tempDirectory))
				Directory.Delete (tempDirectory, recursive: true);
		}

		[Test]
		public void ReplacesNonEmptyDestinationAndRetriesBackupCleanup ()
		{
			var source = Path.Combine (tempDirectory, "source");
			var destination = Path.Combine (tempDirectory, "platforms", "android-36.1");
			Directory.CreateDirectory (source);
			File.WriteAllText (Path.Combine (source, "source.properties"), "Pkg.Revision=1");
			File.WriteAllText (Path.Combine (source, "android.jar"), "new");
			Directory.CreateDirectory (Path.Combine (destination, "data", "res"));
			File.WriteAllText (Path.Combine (destination, "data", "res", "old.xml"), "old");

			var task = new RetryDeleteReplaceDirectory {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors, warnings, messages),
				SourceDirectory = source,
				DestinationDirectory = destination,
				RequiredFile = "source.properties",
				FailuresBeforeSuccess = 2,
				RetryDelayMilliseconds = 0,
			};

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			Assert.AreEqual (3, task.DeleteAttempts, "Backup cleanup should be retried.");
			Assert.IsFalse (Directory.Exists (source), "Source directory should be moved.");
			FileAssert.Exists (Path.Combine (destination, "android.jar"));
			FileAssert.DoesNotExist (Path.Combine (destination, "data", "res", "old.xml"));
			Assert.IsTrue (task.DeletedDirectories.All (path => path.StartsWith (destination + ".old-", StringComparison.Ordinal)), "The live destination should be renamed, not recursively deleted.");
			Assert.IsTrue (messages.Any (message => message.Message.Contains ("Retrying.", StringComparison.Ordinal)), "Expected retry diagnostics.");
			Assert.IsEmpty (warnings);
			Assert.IsEmpty (errors);
		}

		[Test]
		public void ReportsBackupCleanupFailureWithoutDiscardingInstalledDirectory ()
		{
			var source = Path.Combine (tempDirectory, "source");
			var destination = Path.Combine (tempDirectory, "destination");
			Directory.CreateDirectory (source);
			File.WriteAllText (Path.Combine (source, "source.properties"), "Pkg.Revision=1");
			Directory.CreateDirectory (Path.Combine (destination, "data", "res"));
			File.WriteAllText (Path.Combine (destination, "data", "res", "old.xml"), "old");

			var task = new RetryDeleteReplaceDirectory {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors, warnings, messages),
				SourceDirectory = source,
				DestinationDirectory = destination,
				RequiredFile = "source.properties",
				FailuresBeforeSuccess = int.MaxValue,
				RetryCount = 1,
				RetryDelayMilliseconds = 0,
			};

			Assert.IsTrue (task.Execute (), "A committed installation should not fail because backup cleanup failed.");
			FileAssert.Exists (Path.Combine (destination, "source.properties"));
			Assert.IsTrue (warnings.Any (warning =>
				warning.Message.Contains ("could not remove old directory", StringComparison.Ordinal) &&
				warning.Message.Contains ("Directory not empty.", StringComparison.Ordinal) &&
				warning.Message.Contains ("Remaining entries:", StringComparison.Ordinal)), "Expected actionable cleanup diagnostics.");
			Assert.IsEmpty (errors);
		}

		[Test]
		public void MissingRequiredFilePreservesDestination ()
		{
			var source = Path.Combine (tempDirectory, "source");
			var destination = Path.Combine (tempDirectory, "destination");
			Directory.CreateDirectory (source);
			Directory.CreateDirectory (destination);
			File.WriteAllText (Path.Combine (destination, "original.txt"), "original");

			var task = new ReplaceDirectory {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors, warnings, messages),
				SourceDirectory = source,
				DestinationDirectory = destination,
				RequiredFile = "source.properties",
			};

			Assert.IsFalse (task.Execute (), "Task should fail.");
			FileAssert.Exists (Path.Combine (destination, "original.txt"));
			Assert.IsTrue (Directory.Exists (source), "Invalid source should not be moved.");
			Assert.IsTrue (errors.Any (error => error.Message.Contains ("source.properties", StringComparison.Ordinal)), "Expected missing-file diagnostics.");
		}

		[Test]
		public void MoveFailureRestoresDestination ()
		{
			var source = Path.Combine (tempDirectory, "source");
			var destination = Path.Combine (tempDirectory, "destination");
			Directory.CreateDirectory (source);
			File.WriteAllText (Path.Combine (source, "source.properties"), "Pkg.Revision=1");
			Directory.CreateDirectory (destination);
			File.WriteAllText (Path.Combine (destination, "original.txt"), "original");

			var task = new FailInstallReplaceDirectory (source) {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors, warnings, messages),
				SourceDirectory = source,
				DestinationDirectory = destination,
				RequiredFile = "source.properties",
				RetryCount = 0,
			};

			Assert.IsFalse (task.Execute (), "Task should fail.");
			FileAssert.Exists (Path.Combine (destination, "original.txt"));
			Assert.IsTrue (Directory.Exists (source), "Source should remain available after rollback.");
			Assert.IsTrue (messages.Any (message => message.Message.Contains ("Restored previous directory", StringComparison.Ordinal)), "Expected rollback diagnostics.");
			Assert.IsNotEmpty (errors);
		}

		sealed class RetryDeleteReplaceDirectory : ReplaceDirectory
		{
			public int DeleteAttempts { get; private set; }

			public int FailuresBeforeSuccess { get; set; }

			public List<string> DeletedDirectories { get; } = new List<string> ();

			protected override void DeleteDirectory (string directory)
			{
				DeleteAttempts++;
				DeletedDirectories.Add (directory);
				if (DeleteAttempts <= FailuresBeforeSuccess)
					throw new IOException ("Directory not empty.");
				base.DeleteDirectory (directory);
			}
		}

		sealed class FailInstallReplaceDirectory : ReplaceDirectory
		{
			readonly string sourceDirectory;

			public FailInstallReplaceDirectory (string sourceDirectory)
			{
				this.sourceDirectory = new DirectoryInfo (sourceDirectory).FullName;
			}

			protected override void MoveDirectory (string source, string destination)
			{
				if (string.Equals (source, sourceDirectory, StringComparison.Ordinal))
					throw new IOException ("Install move failed.");
				base.MoveDirectory (source, destination);
			}
		}
	}
}
