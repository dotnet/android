using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Build.Tests
{
	[TestClass]
	public class SymbolicLinkTests
	{
		string tempDirectory = "";

		[TestInitialize]
		public void SetUp ()
		{
			tempDirectory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			Directory.CreateDirectory (tempDirectory);
		}

		[TestCleanup]
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
				Assert.Inconclusive ("Creating symbolic links requires Windows Developer Mode or elevated permissions.");
			}
		}

		[TestMethod]
		public void CreateDirectoryLink ()
		{
			RequireDirectorySymlinks ();
			var target = Path.Combine (tempDirectory, "target");
			Directory.CreateDirectory (Path.Combine (target, "platforms"));
			var source = Path.Combine (tempDirectory, "SDK Ümläüts", "sdk");

			Assert.IsTrue (SymbolicLink.Create (source, target));
			Assert.AreEqual (target, new DirectoryInfo (source).LinkTarget);
			Assert.IsTrue (Directory.Exists (Path.Combine (source, "platforms")));
			Assert.IsTrue (SymbolicLink.Create (source, target));
		}

		[TestMethod]
		public void CreateWithRelativeTarget ()
		{
			RequireDirectorySymlinks ();
			var target = Path.Combine ("relative", "target");
			var source = Path.Combine (tempDirectory, "link");
			var expectedTarget = OperatingSystem.IsWindows () ? target : Path.GetFullPath (target);

			Assert.IsTrue (SymbolicLink.Create (source, target));
			Assert.AreEqual (expectedTarget, new DirectoryInfo (source).LinkTarget);
		}

		[TestMethod]
		public void CreateWithMissingTarget ()
		{
			RequireDirectorySymlinks ();
			var target = Path.Combine (tempDirectory, "missing");
			var source = Path.Combine (tempDirectory, "link");

			Assert.IsTrue (SymbolicLink.Create (source, target));
			Assert.AreEqual (target, new DirectoryInfo (source).LinkTarget);
			bool danglingLinkExists = OperatingSystem.IsWindows ();
			Assert.AreEqual (danglingLinkExists, Directory.Exists (source));
			Assert.AreEqual (danglingLinkExists, SymbolicLink.Create (source, target));
		}

		[TestMethod]
		public void CreateWithExistingDirectory ()
		{
			var source = Path.Combine (tempDirectory, "existing");
			Directory.CreateDirectory (source);

			Assert.IsTrue (SymbolicLink.Create (source, Path.Combine (tempDirectory, "missing")));
			Assert.IsNull (new DirectoryInfo (source).LinkTarget);
		}

		[TestMethod]
		public void CreateWithExistingFile ()
		{
			var source = Path.Combine (tempDirectory, "existing");
			File.WriteAllText (source, "existing file");

			Assert.IsFalse (SymbolicLink.Create (source, tempDirectory));
			Assert.AreEqual ("existing file", File.ReadAllText (source));
		}

		[TestMethod]
		public void CreateWithEmptyTarget ()
		{
			var source = Path.Combine (tempDirectory, "link");
			if (OperatingSystem.IsWindows ()) {
				Assert.IsFalse (SymbolicLink.Create (source, ""));
			} else {
				Assert.ThrowsExactly<ArgumentException> (() => SymbolicLink.Create (source, ""));
			}
		}

		[TestMethod]
		public void CreateWithSourceWithoutParent ()
		{
			var source = $"symbolic-link-test-{Guid.NewGuid ():N}";
			try {
				Assert.ThrowsExactly<ArgumentException> (() => SymbolicLink.Create (source, tempDirectory));
			} finally {
				if (new DirectoryInfo (source).LinkTarget != null)
					Directory.Delete (source);
			}
		}
	}
}
