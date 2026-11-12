using System.Linq;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests
{
	public partial class FastDeployTests
	{
		[Test]
		public void FastDeployRejectsFailedOverrideCommand (
			[Values (
				"'-c' 'find ",
				"rm -f files/.__override__/stale.dll",
				"rm -f files/.__override__/arm64-v8a/Foo.dll",
				"mkdir -p files/.__override__",
				"cp -p")] string command,
			[Values ("", "error: closed")] string output)
		{
			using var device = new FakeAdbDevice ();
			device.FailCommand (command, output);

			Assert.IsFalse (device.RunFastDeploy (), command);
			Assert.AreEqual ("XA0129", device.Errors.Single ().Code);
			Assert.IsFalse (device.HasManifest);
			Assert.IsFalse (device.Commands.Any (c => c.Contains ("printf %s")));
			Assert.That (device.Commands.Last (), Does.Contain (command), "Stop at the failed command.");
		}

		[Test]
		public void FastDeployRejectsFailedOverrideRemoval ()
		{
			using var device = new FakeAdbDevice ();
			device.FailCommand ("rm -rf files/.__override__", "");

			Assert.IsFalse (device.RunFastDeploy (embedded: true));
			Assert.AreEqual ("XA0129", device.Errors.Single ().Code);
		}

		[TestCase ("cp: write failed", "XA0129")]
		[TestCase ("run-as: package not debuggable", "XA0134")]
		public void FastDeployRecognizesLegacyShellErrors (string output, string code)
		{
			using var device = new FakeAdbDevice ();
			device.FailCommand ("cp -p", output, exitCode: 0);

			Assert.IsFalse (device.RunFastDeploy ());
			Assert.AreEqual (code, device.Errors.Single ().Code);
			Assert.IsFalse (device.HasManifest);
		}

		[TestCase (false, "")]
		[TestCase (true, "error: closed")]
		public void FastDeployRejectsFailedPackageQuery (bool readlink, string output)
		{
			using var device = new FakeAdbDevice ();
			if (readlink) {
				device.WriteResult ("package", 1, "Permission denied");
			}
			string command = readlink ? "readlink -f ." : "pwd";
			device.FailCommand (command, output);

			Assert.IsFalse (device.RunFastDeploy ());
			Assert.AreEqual ("XA0129", device.Errors.Single ().Code);
			Assert.That (device.Commands.Last (), Does.Contain (command));
		}

		[TestCase (false, "")]
		[TestCase (true, "error: closed")]
		public void FastDeployFallsBackToCopyAfterSymlinkFailure (bool incremental, string output)
		{
			using var device = new FakeAdbDevice ();
			if (incremental) {
				Assert.IsTrue (device.RunFastDeploy (symlink: true));
				device.AddFile ("Bar.dll");
			}
			device.FailCommand ("ln -sf", output);

			Assert.IsTrue (device.RunFastDeploy (symlink: true));
			Assert.IsEmpty (device.Errors);
			Assert.IsTrue (device.HasManifest);
			Assert.That (device.Commands, Has.Some.Contain ("cp -p"));
			var failedCommand = device.Commands.Last (c => c.Contains ("ln -sf"));
			Assert.AreEqual (!incremental, failedCommand.Contains ("for f in"));
		}

		[Test]
		public void FastDeployRejectsFailedFallbackCleanup ()
		{
			using var device = new FakeAdbDevice ();
			device.FailCommand ("ln -sf", "error: closed");
			device.FailCommand ("rm -rf files/.__override__", "", second: true);

			Assert.IsFalse (device.RunFastDeploy (symlink: true));
			Assert.AreEqual ("XA0129", device.Errors.Single ().Code);
			Assert.IsFalse (device.HasManifest);
			Assert.That (device.Commands.Last (), Does.Contain ("rm -rf files/.__override__"));
		}

		[TestCase (0, "")]
		[TestCase (1, "find: 'files/.__override__': No such file or directory")]
		public void FastDeployAllowsMissingOrEmptyOverrideDirectory (int exitCode, string output)
		{
			using var device = new FakeAdbDevice ();
			device.WriteResult ("override", exitCode, output);

			Assert.IsTrue (device.RunFastDeploy ());
			Assert.IsEmpty (device.Errors);
			Assert.IsTrue (device.HasManifest);
			Assert.That (device.Commands, Has.Some.Contain ("cp -p"));
		}

		[Test]
		public void FastDeployFallsBackToReadlink ()
		{
			using var device = new FakeAdbDevice ();
			device.WriteResult ("package", 1, "Permission denied");

			Assert.IsTrue (device.RunFastDeploy ());
			Assert.IsEmpty (device.Errors);
			Assert.That (device.Commands, Has.Some.Contain ("readlink -f ."));
		}

		[TestCase ("run-as: unknown package: com.example.app")]
		[TestCase ("run-as: package not debuggable: com.example.app")]
		public void FastDeployInstallsAfterRecoverablePackageQuery (string output)
		{
			using var device = new FakeAdbDevice ();
			device.WriteResult ("package", 1, output);
			device.FailCommand ("rm -rf files/.__override__", output);

			Assert.IsTrue (device.RunFastDeploy (embedded: true, install: true));
			Assert.IsEmpty (device.Errors);
			Assert.That (device.Commands, Has.Some.Contain (" install "));
			Assert.IsFalse (device.Commands.Any (c => c.Contains ("rm -rf files/.__override__")));
		}

		[TestCase ("rm")]
		[TestCase ("ln")]
		public void FastDeployStopsGlobOnIntermediateFailure (string command)
		{
			using var device = new FakeAdbDevice ();
			Assert.IsTrue (device.RunFastDeploy (symlink: true));

			Assert.AreNotEqual (0, device.RunGlobScript (command),
				"A later loop iteration must not mask a failed removal or symlink.");
		}
	}
}
