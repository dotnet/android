#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests.Tasks
{
	[TestFixture]
	public class MergeRemapXmlTests : BaseTest
	{
		[TestCase ("mismatched-root")]
		[TestCase ("unfinished-child")]
		[TestCase ("trailing-root")]
		public void MalformedInputContributesNoLeadingNodes (string kind)
		{
			string prefix = """
				<replacements><replace-type from="example/Peer" to="conflicting/Peer" />
				""";
			string malformed = kind switch {
				"mismatched-root" => prefix + "</broken>",
				"unfinished-child" => prefix + "<replace-method",
				"trailing-root" => prefix + "</replacements><extra />",
				_ => throw new AssertionException ("Unknown fixture kind."),
			};
			var warnings = new List<BuildWarningEventArgs> ();
			XElement root = Merge (warnings, malformed, """
				<replacements><replace-type from="example/Peer" to="generated/Peer" /></replacements>
				""");

			var replacements = root.Elements ("replace-type").ToArray ();
			Assert.AreEqual (1, replacements.Length);
			Assert.AreEqual ("example/Peer", (string?) replacements [0].Attribute ("from"));
			Assert.AreEqual ("generated/Peer", (string?) replacements [0].Attribute ("to"));
			Assert.AreEqual ("XA4318", warnings.Single ().Code);
		}

		[Test]
		public void ValidInputsPreserveNodeOrderAndAttributes ()
		{
			var warnings = new List<BuildWarningEventArgs> ();
			XElement root = Merge (warnings,
				"""<replacements><replace-type from="example/Peer" to="a/b" />"""
				+ """<replace-method source-type="a/b" source-method-name="run" source-method-signature="(Ljava/lang/String;)V" """
				+ """ target-type="a/b" target-method-name="c" target-method-instance-to-static="false" note="&lt;one&gt; &amp; &quot;two&quot;" />"""
				+ """<replace-field source-type="a/b" source-field-name="value" source-field-signature="I" target-type="a/b" target-field-name="d" /></replacements>""",
				"""
				<replacements><reverse-type from="a/b" to="example/Peer" /></replacements>
				""");
			var nodes = root.Elements ().ToArray ();

			CollectionAssert.AreEqual (new [] { "replace-type", "replace-method", "replace-field", "reverse-type" },
				nodes.Select (node => node.Name.LocalName).ToArray ());
			Assert.AreEqual ("(Ljava/lang/String;)V", (string?) nodes [1].Attribute ("source-method-signature"));
			Assert.AreEqual ("false", (string?) nodes [1].Attribute ("target-method-instance-to-static"));
			Assert.AreEqual ("<one> & \"two\"", (string?) nodes [1].Attribute ("note"));
			Assert.AreEqual ("I", (string?) nodes [2].Attribute ("source-field-signature"));
			Assert.IsEmpty (warnings);
		}

		[Test]
		public void InvalidInputDoesNotRemoveEarlierMappings ()
		{
			var warnings = new List<BuildWarningEventArgs> ();
			XElement root = Merge (warnings,
				"""<replacements><replace-type from="before" to="a" /></replacements>""",
				"""<replacements><replace-type from="rejected" to="b" /></broken>""",
				"""<replacements><replace-type from="after" to="c" /></replacements>""");

			CollectionAssert.AreEqual (new [] { "before", "after" },
				root.Elements ("replace-type").Select (node => (string?) node.Attribute ("from")).ToArray ());
			Assert.AreEqual ("XA4318", warnings.Single ().Code);
		}

		[Test]
		public void ValidInputPreservesInheritedNamespacesAndNestedContent ()
		{
			var warnings = new List<BuildWarningEventArgs> ();
			XElement root = Merge (warnings,
				"""<replacements xmlns:jni="urn:remapping"><jni:group label="nested"><jni:replace-type from="a" to="b" />"""
				+ """<!-- retained --><![CDATA[text & more]]></jni:group><replace-type from="c" to="d" /></replacements>""");
			XNamespace ns = "urn:remapping";
			var group = root.Elements (ns + "group").Single ();

			Assert.AreEqual (2, root.Elements ().Count ());
			Assert.AreEqual ("nested", (string?) group.Attribute ("label"));
			Assert.AreEqual ("b", (string?) group.Elements (ns + "replace-type").Single ().Attribute ("to"));
			Assert.AreEqual (" retained ", group.Nodes ().OfType<XComment> ().Single ().Value);
			Assert.AreEqual ("text & more", group.Value);
			Assert.IsEmpty (warnings);
		}

		[Test]
		public void EmptyInputDoesNotSuppressFollowingInputs ()
		{
			var warnings = new List<BuildWarningEventArgs> ();
			XElement root = Merge (warnings, "<replacements />",
				"""<replacements><replace-type from="a" to="b" /></replacements>""");

			Assert.AreEqual (1, root.Elements ().Count ());
			Assert.IsEmpty (warnings);
		}

		XElement Merge (List<BuildWarningEventArgs> warnings, params string [] inputs)
		{
			string directory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (directory);
			var items = new List<ITaskItem> ();
			for (int i = 0; i < inputs.Length; i++) {
				string path = Path.Combine (directory, $"input-{i}.xml");
				File.WriteAllText (path, inputs [i]);
				items.Add (new TaskItem (path));
			}
			string output = Path.Combine (directory, "merged.xml");
			var task = new MergeRemapXml {
				BuildEngine = new MockBuildEngine (TestContext.Out, new List<BuildErrorEventArgs> (), warnings),
				InputRemapXmlFiles = items.ToArray (),
				OutputFile = new TaskItem (output),
			};
			Assert.IsTrue (task.Execute ());
			return XDocument.Load (output).Root ?? throw new AssertionException ("Merged XML has no root.");
		}
	}
}
