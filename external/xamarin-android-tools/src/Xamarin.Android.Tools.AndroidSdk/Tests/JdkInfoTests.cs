using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	public class JdkInfoTests
	{
		[Test]
		public void Constructor_NullPath ()
		{
			Assert.Throws<ArgumentNullException>(() => new JdkInfo (null));
		}

		[Test]
		public void Constructor_InvalidPath ()
		{
			var dir = Path.GetTempFileName ();
			File.Delete (dir);
			Directory.CreateDirectory (dir);
			Assert.Throws<ArgumentException>(() => new JdkInfo (dir));
			Directory.Delete (dir);
		}

		string  FauxJdkDir;

		[OneTimeSetUp]
		public void CreateFauxJdk ()
		{
			var dir = Path.GetTempFileName();
			File.Delete (dir);

			CreateFauxJdk (dir, "1.2.3.4");

			FauxJdkDir = dir;
		}

		internal static void CreateFauxJdk (string dir, string version)
		{
			Directory.CreateDirectory (dir);

			using (var release = new StreamWriter (Path.Combine (dir, "release"))) {
				release.WriteLine ($"JAVA_VERSION=\"{version}\"");
				release.WriteLine ($"BUILD_NUMBER=42");
				release.WriteLine ($"JUST_A_KEY");
			}

			var bin = Path.Combine (dir, "bin");
			var inc = Path.Combine (dir, "include");
			var jli = Path.Combine (dir, "jli");
			var jre = Path.Combine (dir, "jre");

			Directory.CreateDirectory (bin);
			Directory.CreateDirectory (inc);
			Directory.CreateDirectory (jli);
			Directory.CreateDirectory (jre);


			string java =
				$"echo Property settings:{Environment.NewLine}" +
				$"echo \"    java.home = {dir}\"{Environment.NewLine}" +
				$"echo \"    java.vendor = Xamarin.Android Unit Tests\"{Environment.NewLine}" +
				$"echo \"    java.version = 100.100.100\"{Environment.NewLine}" +
				$"echo \"    xamarin.multi-line = line the first\"{Environment.NewLine}" +
				$"echo \"        line the second\"{Environment.NewLine}" +
				$"echo \"        .\"{Environment.NewLine}";

			CreateShellScript (Path.Combine (bin, "jar"), "");
			CreateShellScript (Path.Combine (bin, "java"), java);
			CreateShellScript (Path.Combine (bin, "javac"), "");
			CreateShellScript (Path.Combine (dir, "jli", "libjli.dylib"), "");
			CreateShellScript (Path.Combine (jre, "libjvm.so"), "");
			CreateShellScript (Path.Combine (jre, "jvm.dll"), "");
		}

		[OneTimeTearDown]
		public void DeleteFauxJdk ()
		{
			Directory.Delete (FauxJdkDir, recursive: true);
		}

		static void CreateShellScript (string path, string contents)
		{
			if (OS.IsWindows)
				path += ".cmd";
			using (var script = new StreamWriter (path)) {
				if (!OS.IsWindows) {
					script.WriteLine ("#!/bin/sh");
				}
				script.WriteLine (contents);
			}
			if (OS.IsWindows)
				return;
			var chmod = new ProcessStartInfo {
				FileName                    = "chmod",
				Arguments                   = $"+x \"{path}\"",
				UseShellExecute             = false,
				RedirectStandardInput       = false,
				RedirectStandardOutput      = true,
				RedirectStandardError       = true,
				CreateNoWindow              = true,
				WindowStyle                 = ProcessWindowStyle.Hidden,
			};
			var p = Process.Start (chmod);
			p.WaitForExit ();
		}

		[Test]
		public void PathPropertyValidation ()
		{
			var jdk     = new JdkInfo (FauxJdkDir);

			Assert.AreEqual (jdk.HomePath, FauxJdkDir);
			Assert.IsTrue (File.Exists (jdk.JarPath));
			Assert.IsTrue (File.Exists (jdk.JavaPath));
			Assert.IsTrue (File.Exists (jdk.JavacPath));
			Assert.IsTrue (File.Exists (jdk.JdkJvmPath));
			Assert.IsTrue (Directory.Exists (jdk.IncludePath [0]));
		}

		[Test]
		public void VersionPrefersRelease ()
		{
			var jdk     = new JdkInfo (FauxJdkDir);
			// Note: `release` has JAVA_VERSION=1.2.3.4, while `java` prints java.version=100.100.100.
			// We prefer the value within `releas`.
			Assert.AreEqual (jdk.Version, new Version ("1.2.3.4"));
		}

		[Test]
		public void ReleaseProperties ()
		{
			var jdk     = new JdkInfo (FauxJdkDir);

			Assert.AreEqual (3,         jdk.ReleaseProperties.Count);
			Assert.AreEqual ("1.2.3.4", jdk.ReleaseProperties ["JAVA_VERSION"]);
			Assert.AreEqual ("42",      jdk.ReleaseProperties ["BUILD_NUMBER"]);
			Assert.AreEqual ("",        jdk.ReleaseProperties ["JUST_A_KEY"]);
		}

		[Test]
		public void JavaSettingsProperties ()
		{
			var jdk     = new JdkInfo (FauxJdkDir);

			Assert.AreEqual (4, jdk.JavaSettingsPropertyKeys.Count ());

			Assert.IsFalse(jdk.GetJavaSettingsPropertyValue ("does-not-exist", out var _));
			Assert.IsFalse(jdk.GetJavaSettingsPropertyValues ("does-not-exist", out var _));

			Assert.IsTrue (jdk.GetJavaSettingsPropertyValue ("java.home", out var home));
			Assert.AreEqual (FauxJdkDir, home);

			Assert.IsTrue (jdk.GetJavaSettingsPropertyValue ("java.version", out var version));
			Assert.AreEqual ("100.100.100", version);

			Assert.IsTrue (jdk.GetJavaSettingsPropertyValue ("java.vendor", out var vendor));
			Assert.AreEqual ("Xamarin.Android Unit Tests", vendor);
			Assert.AreEqual (vendor, jdk.Vendor);

			Assert.Throws<InvalidOperationException>(() => jdk.GetJavaSettingsPropertyValue ("xamarin.multi-line", out var _));
			Assert.IsTrue (jdk.GetJavaSettingsPropertyValues ("xamarin.multi-line", out var lines));
			Assert.AreEqual (3,                     lines.Count ());
			Assert.AreEqual ("line the first",      lines.ElementAt (0));
			Assert.AreEqual ("line the second",     lines.ElementAt (1));
			Assert.AreEqual (".",                   lines.ElementAt (2));
		}
	}
}
