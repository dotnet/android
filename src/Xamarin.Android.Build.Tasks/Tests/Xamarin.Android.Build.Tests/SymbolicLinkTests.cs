using System;
using System.IO;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class SymbolicLinkTests
	{
		string tempDirectory = "";

		[SetUp]
		public void SetUp ()
		{
			tempDirectory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			Directory.CreateDirectory (tempDirectory);
		}

		[TearDown]
		public void TearDown ()
		{
			if (Directory.Exists (tempDirectory))
				Directory.Delete (tempDirectory, recursive: true);
		}

		void RequireDirectorySymlinks ()
		{
			if (!OperatingSystem.IsWindows ())
				return;

			const int ErrorPrivilegeNotHeld = 1314;
			var probe = Path.Combine (tempDirectory, "probe");
			try {
				Directory.CreateSymbolicLink (probe, tempDirectory);
				Directory.Delete (probe);
			} catch (Exception e) when ((e is IOException || e is UnauthorizedAccessException) && (e.HResult & 0xffff) == ErrorPrivilegeNotHeld) {
				Assert.Ignore ("Creating symbolic links requires Windows Developer Mode or elevated permissions.");
			}
		}

		[Test]
		public void CreateDirectoryLink ()
		{
			RequireDirectorySymlinks ();
			var target = Path.Combine (tempDirectory, "target");
			Directory.CreateDirectory (Path.Combine (target, "platforms"));
			var source = Path.Combine (tempDirectory, "SDK Ümläüts", "sdk");

			Assert.IsTrue (SymbolicLink.Create (source, target));
			Assert.AreEqual (target, new DirectoryInfo (source).LinkTarget);
			DirectoryAssert.Exists (Path.Combine (source, "platforms"));
			Assert.IsTrue (SymbolicLink.Create (source, target));
		}

		[Test]
		public void CreateWithRelativeTarget ()
		{
			RequireDirectorySymlinks ();
			var target = Path.Combine ("relative", "target");
			var source = Path.Combine (tempDirectory, "link");
			var expectedTarget = OperatingSystem.IsWindows () ? target : Path.GetFullPath (target);

			Assert.IsTrue (SymbolicLink.Create (source, target));
			Assert.AreEqual (expectedTarget, new DirectoryInfo (source).LinkTarget);
		}

		[Test]
		public void CreateWithMissingTarget ()
		{
			RequireDirectorySymlinks ();
			var target = Path.Combine (tempDirectory, "missing");
			var source = Path.Combine (tempDirectory, "link");

			Assert.IsTrue (SymbolicLink.Create (source, target));
			Assert.AreEqual (target, new DirectoryInfo (source).LinkTarget);
			Assert.IsFalse (Directory.Exists (source));
			Assert.IsFalse (SymbolicLink.Create (source, target));
		}

		[Test]
		public void CreateWithExistingDirectory ()
		{
			var source = Path.Combine (tempDirectory, "existing");
			Directory.CreateDirectory (source);

			Assert.IsTrue (SymbolicLink.Create (source, Path.Combine (tempDirectory, "missing")));
			Assert.IsNull (new DirectoryInfo (source).LinkTarget);
		}

		[Test]
		public void CreateWithExistingFile ()
		{
			var source = Path.Combine (tempDirectory, "existing");
			File.WriteAllText (source, "existing file");

			Assert.IsFalse (SymbolicLink.Create (source, tempDirectory));
			Assert.AreEqual ("existing file", File.ReadAllText (source));
		}

		[Test]
		public void CreateWithEmptyTarget ()
		{
			var source = Path.Combine (tempDirectory, "link");
			if (OperatingSystem.IsWindows ()) {
				Assert.IsFalse (SymbolicLink.Create (source, ""));
			} else {
				Assert.Throws<ArgumentException> (() => SymbolicLink.Create (source, ""));
			}
		}

		[Test]
		public void CreateWithSourceWithoutParent ()
		{
			var source = $"symbolic-link-test-{Guid.NewGuid ():N}";
			try {
				Assert.Throws<ArgumentException> (() => SymbolicLink.Create (source, tempDirectory));
			} finally {
				if (new DirectoryInfo (source).LinkTarget != null)
					Directory.Delete (source);
			}
		}
	}
}
