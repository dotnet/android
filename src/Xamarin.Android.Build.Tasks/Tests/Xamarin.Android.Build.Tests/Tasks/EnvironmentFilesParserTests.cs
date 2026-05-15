using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Self)]
	public class EnvironmentFilesParserTests : BaseTest
	{
		string? tempDirectory;

		[SetUp]
		public void Setup ()
		{
			tempDirectory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			Directory.CreateDirectory (tempDirectory);
		}

		[TearDown]
		public void TearDown ()
		{
			if (tempDirectory != null && Directory.Exists (tempDirectory))
				Directory.Delete (tempDirectory, recursive: true);
		}

		ITaskItem CreateEnvFile (string content)
		{
			var path = Path.Combine (tempDirectory ?? Path.GetTempPath (), Path.GetRandomFileName () + ".env");
			File.WriteAllText (path, content);
			return new TaskItem (path);
		}

		[Test]
		public void DetectsMonoLogLevel ()
		{
			var envFile = CreateEnvFile ("MONO_LOG_LEVEL=debug");
			var parser = new EnvironmentFilesParser ();
			var engine = new MockBuildEngine (TestContext.Out);
			var log = new TaskLoggingHelper (engine, "Test");

			parser.Parse (new [] { envFile }, SequencePointsMode.None, log);

			Assert.IsTrue (parser.HaveLogLevel, "HaveLogLevel should be true");
		}

		[Test]
		public void DetectsMonoGCParams ()
		{
			var envFile = CreateEnvFile ("MONO_GC_PARAMS=nursery-size=64m");
			var parser = new EnvironmentFilesParser ();
			var engine = new MockBuildEngine (TestContext.Out);
			var log = new TaskLoggingHelper (engine, "Test");

			parser.Parse (new [] { envFile }, SequencePointsMode.None, log);

			Assert.IsTrue (parser.HaveMonoGCParams, "HaveMonoGCParams should be true");
		}

		[Test]
		public void MonoGCParams_OldBridgeWarning ()
		{
			var envFile = CreateEnvFile ("MONO_GC_PARAMS=bridge-implementation=old");
			var warnings = new List<BuildWarningEventArgs> ();
			var engine = new MockBuildEngine (TestContext.Out, warnings: warnings);
			var log = new TaskLoggingHelper (engine, "Test");
			var parser = new EnvironmentFilesParser ();

			parser.Parse (new [] { envFile }, SequencePointsMode.None, log);

			Assert.IsTrue (parser.HaveMonoGCParams, "HaveMonoGCParams should be true");
			Assert.AreEqual (1, warnings.Count, "Expected one warning");
			Assert.AreEqual ("XA2000", warnings [0].Code);
		}

		[Test]
		public void DetectsMonoDebug ()
		{
			var envFile = CreateEnvFile ("MONO_DEBUG=soft-breakpoints");
			var parser = new EnvironmentFilesParser ();
			var engine = new MockBuildEngine (TestContext.Out);
			var log = new TaskLoggingHelper (engine, "Test");

			parser.Parse (new [] { envFile }, SequencePointsMode.None, log);

			Assert.IsTrue (parser.HaveMonoDebug, "HaveMonoDebug should be true");
		}

		[Test]
		public void MonoDebug_AppendsSequencePoints ()
		{
			var envFile = CreateEnvFile ("MONO_DEBUG=soft-breakpoints");
			var parser = new EnvironmentFilesParser ();
			var engine = new MockBuildEngine (TestContext.Out);
			var log = new TaskLoggingHelper (engine, "Test");

			parser.Parse (new [] { envFile }, SequencePointsMode.Normal, log);

			Assert.IsTrue (parser.HaveMonoDebug, "HaveMonoDebug should be true");
			var monoDebugLine = parser.EnvironmentVariableLines.FirstOrDefault (l => l.StartsWith ("MONO_DEBUG="));
			Assert.IsNotNull (monoDebugLine, "Should have a MONO_DEBUG line");
			StringAssert.Contains ("gen-compact-seq-points", monoDebugLine);
		}

		[Test]
		public void MonoDebug_DoesNotDuplicateSequencePoints ()
		{
			var envFile = CreateEnvFile ("MONO_DEBUG=soft-breakpoints,gen-compact-seq-points");
			var parser = new EnvironmentFilesParser ();
			var engine = new MockBuildEngine (TestContext.Out);
			var log = new TaskLoggingHelper (engine, "Test");

			parser.Parse (new [] { envFile }, SequencePointsMode.Normal, log);

			Assert.IsTrue (parser.HaveMonoDebug, "HaveMonoDebug should be true");
			var monoDebugLine = parser.EnvironmentVariableLines.FirstOrDefault (l => l.StartsWith ("MONO_DEBUG="));
			Assert.IsNotNull (monoDebugLine, "Should have a MONO_DEBUG line");
			// Should not duplicate the gen-compact-seq-points entry
			Assert.AreEqual ("MONO_DEBUG=soft-breakpoints,gen-compact-seq-points", monoDebugLine);
		}

		[Test]
		public void DetectsHttpMessageHandler ()
		{
			var envFile = CreateEnvFile ("XA_HTTP_CLIENT_HANDLER_TYPE=Xamarin.Android.Net.AndroidClientHandler");
			var parser = new EnvironmentFilesParser ();
			var engine = new MockBuildEngine (TestContext.Out);
			var log = new TaskLoggingHelper (engine, "Test");

			parser.Parse (new [] { envFile }, SequencePointsMode.None, log);

			Assert.IsTrue (parser.HaveHttpMessageHandler, "HaveHttpMessageHandler should be true");
		}

		[Test]
		public void AssemblyPreload_SetsFlag_AndExcludesLine ()
		{
			var envFile = CreateEnvFile ("mono.enable_assembly_preload=1");
			var parser = new EnvironmentFilesParser ();
			var engine = new MockBuildEngine (TestContext.Out);
			var log = new TaskLoggingHelper (engine, "Test");

			parser.Parse (new [] { envFile }, SequencePointsMode.None, log);

			Assert.IsFalse (parser.EnvironmentVariableLines.Any (l => l.Contains ("mono.enable_assembly_preload")),
				"mono.enable_assembly_preload should be excluded from EnvironmentVariableLines");
		}

		[Test]
		public void BrokenExceptionTransitions_SetsFlag_AndExcludesLine ()
		{
			var envFile = CreateEnvFile ("XA_BROKEN_EXCEPTION_TRANSITIONS=true");
			var parser = new EnvironmentFilesParser ();
			var engine = new MockBuildEngine (TestContext.Out);
			var log = new TaskLoggingHelper (engine, "Test");

			parser.Parse (new [] { envFile }, SequencePointsMode.None, log);

			Assert.IsTrue (parser.BrokenExceptionTransitions, "BrokenExceptionTransitions should be true");
			Assert.IsFalse (parser.EnvironmentVariableLines.Any (l => l.Contains ("XA_BROKEN_EXCEPTION_TRANSITIONS")),
				"XA_BROKEN_EXCEPTION_TRANSITIONS should be excluded from EnvironmentVariableLines");
		}

		[Test]
		public void AreBrokenExceptionTransitionsEnabled_ReturnsTrue ()
		{
			var envFile = CreateEnvFile ("XA_BROKEN_EXCEPTION_TRANSITIONS=true");
			var parser = new EnvironmentFilesParser ();

			Assert.IsTrue (parser.AreBrokenExceptionTransitionsEnabled (new [] { envFile }));
		}

		[Test]
		public void AreBrokenExceptionTransitionsEnabled_ReturnsFalse ()
		{
			var envFile = CreateEnvFile ("MONO_LOG_LEVEL=debug");
			var parser = new EnvironmentFilesParser ();

			Assert.IsFalse (parser.AreBrokenExceptionTransitionsEnabled (new [] { envFile }));
		}

		[Test]
		public void Parse_NullEnvironments_DoesNotThrow ()
		{
			var parser = new EnvironmentFilesParser ();
			var engine = new MockBuildEngine (TestContext.Out);
			var log = new TaskLoggingHelper (engine, "Test");

			Assert.DoesNotThrow (() => parser.Parse (null, SequencePointsMode.None, log));
		}

		[Test]
		public void Parse_MultipleFiles_AccumulatesFlags ()
		{
			var envFile1 = CreateEnvFile ("MONO_LOG_LEVEL=debug");
			var envFile2 = CreateEnvFile ("MONO_GC_PARAMS=nursery-size=64m");
			var parser = new EnvironmentFilesParser ();
			var engine = new MockBuildEngine (TestContext.Out);
			var log = new TaskLoggingHelper (engine, "Test");

			parser.Parse (new [] { envFile1, envFile2 }, SequencePointsMode.None, log);

			Assert.IsTrue (parser.HaveLogLevel, "HaveLogLevel should be true");
			Assert.IsTrue (parser.HaveMonoGCParams, "HaveMonoGCParams should be true");
			Assert.AreEqual (2, parser.EnvironmentVariableLines.Count, "Should have 2 environment variable lines");
		}
	}
}
