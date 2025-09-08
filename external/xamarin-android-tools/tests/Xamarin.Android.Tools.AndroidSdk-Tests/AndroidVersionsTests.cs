using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	public class AndroidVersionsTests
	{
		[Test]
		public void Constructor_Exceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new AndroidVersions ((IEnumerable<string>) null));
			Assert.Throws<ArgumentNullException> (() => new AndroidVersions ((IEnumerable<AndroidVersion>) null));

			var tempDir = Path.GetTempFileName ();
			File.Delete (tempDir);
			// Directory not found
			Assert.Throws<ArgumentException>(() => new AndroidVersions (new[]{tempDir}));
		}

		[Test]
		public void Constructor_NoDirectories ()
		{
			var versions    = new AndroidVersions (new string [0]);
			Assert.AreEqual (null,  versions.MaxStableVersion);
			Assert.IsNotNull (versions.FrameworkDirectories);
			Assert.AreEqual (0,     versions.FrameworkDirectories.Count);
		}

		[Test]
		public void Constructor_NoVersions ()
		{
			var versions    = new AndroidVersions (new AndroidVersion [0]);
			Assert.AreEqual (null,  versions.MaxStableVersion);
			Assert.IsNotNull (versions.FrameworkDirectories);
			Assert.AreEqual (0,     versions.FrameworkDirectories.Count);
		}

		[Test]
		public void Contructor_UnstableVersions ()
		{
			var versions = new AndroidVersions (
				new [] { new AndroidVersion (apiLevel: 100, osVersion: "100.0", codeName: "Test", id: "Z", stable: false) }
			);
			Assert.IsNull (versions.MaxStableVersion);
			Assert.IsNull (versions.MinStableVersion);
		}

		[Test]
		public void Constructor_FrameworkDirectories ()
		{
			var frameworkDir    = Path.GetTempFileName ();
			File.Delete (frameworkDir);
			Directory.CreateDirectory (frameworkDir);
			try {
				Directory.CreateDirectory (Path.Combine (frameworkDir, "MonoAndroid"));
				Directory.CreateDirectory (Path.Combine (frameworkDir, "MonoAndroid", "v5.1"));
				File.WriteAllLines (Path.Combine (frameworkDir, "MonoAndroid", "v5.1", "AndroidApiInfo.xml"), new []{
					"<AndroidApiInfo>",
					"  <Id>22</Id>",
					"  <Level>22</Level>",
					"  <Name>Marshmallow</Name>",
					"  <Version>v5.1</Version>",
					"  <Stable>True</Stable>",
					"</AndroidApiInfo>",
				});
				Directory.CreateDirectory (Path.Combine (frameworkDir, "MonoAndroid", "v6.0"));
				File.WriteAllLines (Path.Combine (frameworkDir, "MonoAndroid", "v6.0", "AndroidApiInfo.xml"), new []{
					"<AndroidApiInfo>",
					"  <Id>23</Id>",
					"  <Level>23</Level>",
					"  <Name>Marshmallow</Name>",
					"  <Version>v6.0</Version>",
					"  <Stable>True</Stable>",
					"</AndroidApiInfo>",
				});
				Directory.CreateDirectory (Path.Combine (frameworkDir, "MonoAndroid", "v8.0"));
				File.WriteAllLines (Path.Combine (frameworkDir, "MonoAndroid", "v8.0", "AndroidApiInfo.xml"), new []{
					"<AndroidApiInfo>",
					"  <Id>O</Id>",
					"  <Level>26</Level>",
					"  <Name>Oreo</Name>",
					"  <Version>v8.0</Version>",
					"  <Stable>False</Stable>",
					"</AndroidApiInfo>",
				});
				Directory.CreateDirectory (Path.Combine (frameworkDir, "MonoAndroid", "v108.1.99"));
				File.WriteAllLines (Path.Combine (frameworkDir, "MonoAndroid", "v108.1.99", "AndroidApiInfo.xml"), new []{
					"<AndroidApiInfo>",
					"  <Id>Z</Id>",
					"  <Level>127</Level>",
					"  <VersionCodeFull>127.1</VersionCodeFull>",
					"  <Name>Z</Name>",
					"  <Version>v108.1.99</Version>",
					"  <Stable>False</Stable>",
					"</AndroidApiInfo>",
				});
				var versions    = new AndroidVersions (new [] {
					Path.Combine (frameworkDir, "MonoAndroid", "v5.1"),
					Path.Combine (frameworkDir, "MonoAndroid", "v6.0"),
					Path.Combine (frameworkDir, "MonoAndroid", "v108.1.99"),
				});
				Assert.IsNotNull (versions.FrameworkDirectories);
				Assert.AreEqual (1,     versions.FrameworkDirectories.Count);
				Assert.AreEqual (Path.Combine (frameworkDir, "MonoAndroid"), versions.FrameworkDirectories [0]);
				Assert.IsNotNull (versions.MaxStableVersion);
				Assert.AreEqual (23,    versions.MaxStableVersion.ApiLevel);
				Assert.IsNotNull (versions.MinStableVersion);
				Assert.AreEqual (22, versions.MinStableVersion.ApiLevel);
			}
			finally {
				Directory.Delete (frameworkDir, recursive: true);
			}
		}

		[Test]
		public void Constructor_Versions ()
		{
			var versions = new AndroidVersions (new []{
				new AndroidVersion (apiLevel: 1, osVersion: "1.0", codeName: "One",     id: "A", stable: true),
				new AndroidVersion (apiLevel: 2, osVersion: "1.1", codeName: "One.One", id: "B", stable: true),
			});
			Assert.IsNotNull (versions.FrameworkDirectories);
			Assert.AreEqual (0,     versions.FrameworkDirectories.Count);
			Assert.IsNotNull (versions.MaxStableVersion);
			Assert.AreEqual (2,     versions.MaxStableVersion.ApiLevel);
			Assert.IsNotNull (versions.MinStableVersion);
			Assert.AreEqual (1, versions.MinStableVersion.ApiLevel);
		}

		static AndroidVersions CreateTestVersions ()
		{
			return new AndroidVersions (new []{
				new AndroidVersion (apiLevel: 1,    osVersion: "1.0",   id: "A",    stable: true),
				new AndroidVersion (apiLevel: 2,    osVersion: "1.1",   id: "B",    stable: false),
				new AndroidVersion (apiLevel: 3,    osVersion: "1.2",   id: "C",    stable: true),
				// Hides/shadows a Known Version
				new AndroidVersion (apiLevel: 14,   osVersion: "4.0",   id: "II",   stable: false),
				// Demonstrates new "minor" release support
				new AndroidVersion (versionCodeFull: new Version (36, 1),   osVersion: "16.1",  id: "CANARY",   stable: true),
				new AndroidVersion (versionCodeFull: new Version (36, 0),   osVersion: "16.0",  id: "Baklava",  stable: true),
				new AndroidVersion (versionCodeFull: new Version (37, 1),   osVersion: "17.1",  id: "E",        stable: false),
				new AndroidVersion (versionCodeFull: new Version (37, 0),   osVersion: "17.0",  id: "D",        stable: true),
			});
		}

		[Test]
		public void GetApiLevelFromFrameworkVersion ()
		{
			var versions    = CreateTestVersions ();

			Assert.AreEqual (null,  versions.GetApiLevelFromFrameworkVersion (null));
			Assert.AreEqual (1,     versions.GetApiLevelFromFrameworkVersion ("v1.0"));
			Assert.AreEqual (1,     versions.GetApiLevelFromFrameworkVersion ("1.0"));
			Assert.AreEqual (2,     versions.GetApiLevelFromFrameworkVersion ("v1.1"));
			Assert.AreEqual (2,     versions.GetApiLevelFromFrameworkVersion ("1.1"));
			Assert.AreEqual (3,     versions.GetApiLevelFromFrameworkVersion ("v1.2"));
			Assert.AreEqual (3,     versions.GetApiLevelFromFrameworkVersion ("1.2"));
			Assert.AreEqual (null,  versions.GetApiLevelFromFrameworkVersion ("v1.3"));
			Assert.AreEqual (null,  versions.GetApiLevelFromFrameworkVersion ("1.3"));
			Assert.AreEqual (14,    versions.GetApiLevelFromFrameworkVersion ("v4.0"));
			Assert.AreEqual (14,    versions.GetApiLevelFromFrameworkVersion ("4.0"));
			Assert.AreEqual (36,    versions.GetApiLevelFromFrameworkVersion ("16.0"));
			Assert.AreEqual (36,    versions.GetApiLevelFromFrameworkVersion ("16.1"));
			Assert.AreEqual (37,    versions.GetApiLevelFromFrameworkVersion ("17.0"));
			Assert.AreEqual (37,    versions.GetApiLevelFromFrameworkVersion ("17.1"));

			// via KnownVersions
			Assert.AreEqual (4,     versions.GetApiLevelFromFrameworkVersion ("v1.6"));
			Assert.AreEqual (4,     versions.GetApiLevelFromFrameworkVersion ("1.6"));
		}

		[Test]
		public void GetApiLevelFromId ()
		{
			var versions    = CreateTestVersions ();

			Assert.AreEqual (null,  versions.GetApiLevelFromId (null));
			Assert.AreEqual (1,     versions.GetApiLevelFromId ("A"));
			Assert.AreEqual (1,     versions.GetApiLevelFromId ("1"));
			Assert.AreEqual (2,     versions.GetApiLevelFromId ("B"));
			Assert.AreEqual (2,     versions.GetApiLevelFromId ("2"));
			Assert.AreEqual (3,     versions.GetApiLevelFromId ("C"));
			Assert.AreEqual (3,     versions.GetApiLevelFromId ("3"));
			Assert.AreEqual (14,    versions.GetApiLevelFromId ("14"));
			Assert.AreEqual (14,    versions.GetApiLevelFromId ("II"));
			Assert.AreEqual (36,    versions.GetApiLevelFromId ("36"));
			Assert.AreEqual (36,    versions.GetApiLevelFromId ("36.1"));
			Assert.AreEqual (36,    versions.GetApiLevelFromId ("CANARY"));
			Assert.AreEqual (37,    versions.GetApiLevelFromId ("37"));
			Assert.AreEqual (37,    versions.GetApiLevelFromId ("37.1"));
			Assert.AreEqual (37,    versions.GetApiLevelFromId ("D"));

			Assert.AreEqual (null,  versions.GetApiLevelFromId ("Z"));

			// via KnownVersions
			Assert.AreEqual (11,    versions.GetApiLevelFromId ("H"));
		}

		[Test]
		public void GetIdFromApiLevel ()
		{
			var versions    = CreateTestVersions ();

			Assert.AreEqual (null,  versions.GetIdFromApiLevel (null));
			Assert.AreEqual ("A",   versions.GetIdFromApiLevel (1));
			Assert.AreEqual ("A",   versions.GetIdFromApiLevel ("1"));
			Assert.AreEqual ("A",   versions.GetIdFromApiLevel ("A"));
			Assert.AreEqual ("B",   versions.GetIdFromApiLevel (2));
			Assert.AreEqual ("B",   versions.GetIdFromApiLevel ("2"));
			Assert.AreEqual ("B",   versions.GetIdFromApiLevel ("B"));
			Assert.AreEqual ("C",   versions.GetIdFromApiLevel (3));
			Assert.AreEqual ("C",   versions.GetIdFromApiLevel ("3"));
			Assert.AreEqual ("C",   versions.GetIdFromApiLevel ("C"));
			Assert.AreEqual ("II",  versions.GetIdFromApiLevel ("14"));
			Assert.AreEqual ("II",  versions.GetIdFromApiLevel ("II"));

			Assert.AreEqual ("Baklava", versions.GetIdFromApiLevel (36));
			Assert.AreEqual ("Baklava", versions.GetIdFromApiLevel ("36"));
			Assert.AreEqual ("Baklava", versions.GetIdFromApiLevel ("36.0"));
			Assert.AreEqual ("Baklava", versions.GetIdFromApiLevel ("Baklava"));
			Assert.AreEqual ("CANARY",  versions.GetIdFromApiLevel ("36.1"));
			Assert.AreEqual ("CANARY",  versions.GetIdFromApiLevel ("CANARY"));

			Assert.AreEqual ("D",       versions.GetIdFromApiLevel (37));
			Assert.AreEqual ("D",       versions.GetIdFromApiLevel ("37"));
			Assert.AreEqual ("D",       versions.GetIdFromApiLevel ("37.0"));
			Assert.AreEqual ("D",       versions.GetIdFromApiLevel ("D"));
			Assert.AreEqual ("E",       versions.GetIdFromApiLevel ("37.1"));
			Assert.AreEqual ("E",       versions.GetIdFromApiLevel ("E"));

			// "GIGO"
			Assert.AreEqual ("-1",  versions.GetIdFromApiLevel (-1));
			Assert.AreEqual ("-1",  versions.GetIdFromApiLevel ("-1"));
			Assert.AreEqual ("Z",   versions.GetIdFromApiLevel ("Z"));

			// via KnownVersions
			Assert.AreEqual ("11",  versions.GetIdFromApiLevel (11));
			Assert.AreEqual ("11",  versions.GetIdFromApiLevel ("11"));
			Assert.AreEqual ("11",  versions.GetIdFromApiLevel ("H"));
		}

		[Test]
		public void GetIdFromFrameworkVersion ()
		{
			var versions    = CreateTestVersions ();

			Assert.AreEqual (null,  versions.GetIdFromFrameworkVersion (null));
			Assert.AreEqual ("A",   versions.GetIdFromFrameworkVersion ("v1.0"));
			Assert.AreEqual ("A",   versions.GetIdFromFrameworkVersion ("1.0"));
			Assert.AreEqual ("B",   versions.GetIdFromFrameworkVersion ("v1.1"));
			Assert.AreEqual ("B",   versions.GetIdFromFrameworkVersion ("1.1"));
			Assert.AreEqual ("C",   versions.GetIdFromFrameworkVersion ("v1.2"));
			Assert.AreEqual ("C",   versions.GetIdFromFrameworkVersion ("1.2"));
			Assert.AreEqual ("II",  versions.GetIdFromFrameworkVersion ("v4.0"));
			Assert.AreEqual ("II",  versions.GetIdFromFrameworkVersion ("4.0"));
			Assert.AreEqual ("Baklava", versions.GetIdFromFrameworkVersion ("16.0"));
			Assert.AreEqual ("CANARY",  versions.GetIdFromFrameworkVersion ("16.1"));
			Assert.AreEqual ("D",       versions.GetIdFromFrameworkVersion ("17.0"));
			Assert.AreEqual ("E",       versions.GetIdFromFrameworkVersion ("17.1"));

			// Unknown values
			Assert.AreEqual (null,  versions.GetIdFromFrameworkVersion ("v0.99"));
			Assert.AreEqual (null,  versions.GetIdFromFrameworkVersion ("0.99"));

			// via KnownVersions
			Assert.AreEqual ("10",  versions.GetIdFromFrameworkVersion ("v2.3"));
			Assert.AreEqual ("10",  versions.GetIdFromFrameworkVersion ("2.3"));
		}

		[Test]
		public void GetFrameworkVersionFromApiLevel ()
		{
			var versions    = CreateTestVersions ();

			Assert.AreEqual (null,      versions.GetFrameworkVersionFromApiLevel (0));
			Assert.AreEqual ("v1.0",    versions.GetFrameworkVersionFromApiLevel (1));
			Assert.AreEqual ("v1.1",    versions.GetFrameworkVersionFromApiLevel (2));
			Assert.AreEqual ("v1.2",    versions.GetFrameworkVersionFromApiLevel (3));
			Assert.AreEqual ("v4.0",    versions.GetFrameworkVersionFromApiLevel (14));
			Assert.AreEqual ("v16.0",   versions.GetFrameworkVersionFromApiLevel (36));
			Assert.AreEqual ("v17.0",   versions.GetFrameworkVersionFromApiLevel (37));

			// via KnownVersions
			Assert.AreEqual ("v2.3",    versions.GetFrameworkVersionFromApiLevel (10));
		}

		[Test]
		public void GetFrameworkVersionFromId ()
		{
			var versions    = CreateTestVersions ();

			Assert.AreEqual (null,      versions.GetFrameworkVersionFromId (null));
			Assert.AreEqual ("v1.0",    versions.GetFrameworkVersionFromId ("1"));
			Assert.AreEqual ("v1.0",    versions.GetFrameworkVersionFromId ("A"));
			Assert.AreEqual ("v1.1",    versions.GetFrameworkVersionFromId ("2"));
			Assert.AreEqual ("v1.1",    versions.GetFrameworkVersionFromId ("B"));
			Assert.AreEqual ("v1.2",    versions.GetFrameworkVersionFromId ("3"));
			Assert.AreEqual ("v1.2",    versions.GetFrameworkVersionFromId ("C"));
			Assert.AreEqual ("v4.0",    versions.GetFrameworkVersionFromId ("14"));
			Assert.AreEqual ("v4.0",    versions.GetFrameworkVersionFromId ("II"));
			Assert.AreEqual ("v16.0",   versions.GetFrameworkVersionFromId ("36"));
			Assert.AreEqual ("v16.0",   versions.GetFrameworkVersionFromId ("Baklava"));
			Assert.AreEqual ("v16.1",   versions.GetFrameworkVersionFromId ("36.1"));
			Assert.AreEqual ("v16.1",   versions.GetFrameworkVersionFromId ("CANARY"));
			Assert.AreEqual ("v17.0",   versions.GetFrameworkVersionFromId ("37"));
			Assert.AreEqual ("v17.0",   versions.GetFrameworkVersionFromId ("D"));
			Assert.AreEqual ("v17.1",   versions.GetFrameworkVersionFromId ("37.1"));
			Assert.AreEqual ("v17.1",   versions.GetFrameworkVersionFromId ("E"));

			// via KnownVersions
			Assert.AreEqual ("v3.0",    versions.GetFrameworkVersionFromId ("11"));
			Assert.AreEqual ("v3.0",    versions.GetFrameworkVersionFromId ("H"));
		}
	}
}
