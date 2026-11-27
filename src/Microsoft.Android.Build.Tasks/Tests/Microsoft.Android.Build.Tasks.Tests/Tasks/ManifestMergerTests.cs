using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class ManifestMergerTests
{
	[TestCase (true)]
	[TestCase (false)]
	public void SaveMergedManifest (bool removeNodes)
	{
		string template = Path.GetTempFileName ();
		try {
			File.WriteAllText (template, """
				<manifest xmlns:android="http://schemas.android.com/apk/res/android" xmlns:tools="http://schemas.android.com/tools" package="${applicationId}">
				  <uses-permission android:name="example.DUPLICATE" />
				  <uses-permission android:name="example.DUPLICATE" />
				  <uses-permission android:name="example.REMOVED" tools:node="remove" />
				  <application android:icon="@drawable/MyIcon" android:label="${label}" />
				</manifest>
				""");
			var manifest = new ManifestDocument (template) {
				PackageName = "com.example",
				Placeholders = ["label=My App", "invalid"],
			};
			var warnings = new List<string> ();
			using var stream = new MemoryStream ();
			manifest.Save ((code, message) => warnings.Add (code), stream, removeNodes);

			Assert.IsTrue (stream.CanRead);
			stream.Position = 0;
			var document = XDocument.Load (stream);
			var root = document.Root;
			Assert.IsNotNull (root);
			Assert.AreEqual ("com.example", (string?)root.Attribute ("package"));
			Assert.That (document.Nodes ().OfType<XComment> ().Single ().Value, Does.Contain (Path.GetFileName (template)));
			XNamespace android = "http://schemas.android.com/apk/res/android";
			var application = root.Element ("application");
			Assert.IsNotNull (application);
			Assert.AreEqual ("@drawable/myicon", (string?)application.Attribute (android + "icon"));
			Assert.AreEqual ("My App", (string?)application.Attribute (android + "label"));
			var permissions = root.Elements ("uses-permission").Select (element => (string?)element.Attribute (android + "name")).ToArray ();
			CollectionAssert.AreEqual (removeNodes ? ["example.DUPLICATE"] : new [] { "example.DUPLICATE", "example.REMOVED" }, permissions);
			CollectionAssert.AreEqual (new [] { "XA1010" }, warnings);
		} finally {
			File.Delete (template);
		}
	}

	[Test]
	public void PreserveExistingComment ()
	{
		string template = Path.GetTempFileName ();
		try {
			File.WriteAllText (template, "<!-- existing -->\n<manifest />");
			var manifest = new ManifestDocument (template);
			using var stream = new MemoryStream ();
			manifest.Save ((code, message) => throw new InvalidOperationException (message), stream);
			stream.Position = 0;
			var document = XDocument.Load (stream);
			Assert.AreEqual (" existing ", document.Nodes ().OfType<XComment> ().Single ().Value);
		} finally {
			File.Delete (template);
		}
	}
}
