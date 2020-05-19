using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class EnumMappingsTests
	{
		[Test]
		public void BasicEnumificationTest ()
		{
			// This should create a new enum and remove the field
			var csv = "10,Org.XmlPull.V1.XmlPullParserNode,Cdsect,I:org/xmlpull/v1/XmlPullParser.CDSECT,5";
			var mappings = new EnumMappings (string.Empty, string.Empty, "30", false);
			var sr = new StringReader (csv);

			var removes = new List<KeyValuePair<string, string>> ();
			var enums = mappings.ParseFieldMappings (sr, new string [0], 30, removes);

			Assert.AreEqual ("[I:org/xmlpull/v1/XmlPullParser.CDSECT, ]", removes.Single ().ToString ());

			Assert.AreEqual ("Org.XmlPull.V1.XmlPullParserNode", enums.Single ().Key);
			Assert.AreEqual (false, enums.Single ().Value.BitField);
			Assert.AreEqual (true, enums.Single ().Value.FieldsRemoved);
			Assert.AreEqual ("[Cdsect, I:org/xmlpull/v1/XmlPullParser.CDSECT]", enums.First ().Value.JniNames.Single ().ToString ());
			Assert.AreEqual ("[Cdsect, 5]", enums.First ().Value.Members.Single ().ToString ());
		}

		[Test]
		public void RemoveFieldOnlyTest ()
		{
			// This should only remove the field
			var csv = "10,,,I:org/xmlpull/v1/XmlPullParser.CDSECT,5";
			var mappings = new EnumMappings (string.Empty, string.Empty, "30", false);
			var sr = new StringReader (csv);

			var removes = new List<KeyValuePair<string, string>> ();
			var enums = mappings.ParseFieldMappings (sr, new string [0], 30, removes);

			Assert.AreEqual ("[I:org/xmlpull/v1/XmlPullParser.CDSECT, ]", removes.Single ().ToString ());

			Assert.AreEqual (0, enums.Count);
		}

		[Test]
		public void AddConstantOnlyTest ()
		{
			// This should only add an enum
			var csv = "10,Org.XmlPull.V1.XmlPullParserNode,Cdsect,,5";
			var mappings = new EnumMappings (string.Empty, string.Empty, "30", false);
			var sr = new StringReader (csv);

			var removes = new List<KeyValuePair<string, string>> ();
			var enums = mappings.ParseFieldMappings (sr, new string [0], 30, removes);

			Assert.AreEqual (0, removes.Count);

			Assert.AreEqual ("Org.XmlPull.V1.XmlPullParserNode", enums.Single ().Key);
			Assert.AreEqual (false, enums.Single ().Value.BitField);
			Assert.AreEqual (true, enums.Single ().Value.FieldsRemoved);
			Assert.AreEqual ("[Cdsect, ]", enums.First ().Value.JniNames.Single ().ToString ());
			Assert.AreEqual ("[Cdsect, 5]", enums.First ().Value.Members.Single ().ToString ());
		}

		[Test]
		public void FlagsEnumerationTest ()
		{
			// This should create a new enum with [Flags] because of the ",Flags" field
			var csv = "10,Org.XmlPull.V1.XmlPullParserNode,Cdsect,I:org/xmlpull/v1/XmlPullParser.CDSECT,5,Flags";
			var mappings = new EnumMappings (string.Empty, string.Empty, "30", false);
			var sr = new StringReader (csv);

			var removes = new List<KeyValuePair<string, string>> ();
			var enums = mappings.ParseFieldMappings (sr, new string [0], 30, removes);

			Assert.AreEqual (true, enums.Single ().Value.BitField);
		}

		[Test]
		public void ExternalFlagsEnumerationTest ()
		{
			// This should create a new enum with [Flags] because of the "enumFlags" parameter
			var csv = "10,Org.XmlPull.V1.XmlPullParserNode,Cdsect,I:org/xmlpull/v1/XmlPullParser.CDSECT,5";
			var mappings = new EnumMappings (string.Empty, string.Empty, "30", false);
			var sr = new StringReader (csv);

			var removes = new List<KeyValuePair<string, string>> ();
			var enums = mappings.ParseFieldMappings (sr, new [] { "Org.XmlPull.V1.XmlPullParserNode" }, 30, removes);

			Assert.AreEqual (true, enums.Single ().Value.BitField);
		}

		[Test]
		public void ApiVersionExcludedTest ()
		{
			// This should be completely ignored because it's API=10 and we're looking for API=5
			var csv = "10,Org.XmlPull.V1.XmlPullParserNode,Cdsect,I:org/xmlpull/v1/XmlPullParser.CDSECT,5,Flags";
			var mappings = new EnumMappings (string.Empty, string.Empty, "5", false);
			var sr = new StringReader (csv);

			var removes = new List<KeyValuePair<string, string>> ();
			var enums = mappings.ParseFieldMappings (sr, new string [0], 5, removes);

			Assert.AreEqual (0, removes.Count);
			Assert.AreEqual (0, enums.Count);
		}

		[Test]
		public void TransientEnumificationTest ()
		{
			// This should create a new enum and remove the field
			var csv = $"- ENTER TRANSIENT MODE -{Environment.NewLine}10,Org.XmlPull.V1.XmlPullParserNode,Cdsect,I:org/xmlpull/v1/XmlPullParser.CDSECT,5";
			var mappings = new EnumMappings (string.Empty, string.Empty, "30", false);
			var sr = new StringReader (csv);

			var removes = new List<KeyValuePair<string, string>> ();
			var enums = mappings.ParseFieldMappings (sr, new string [0], 30, removes);

			Assert.AreEqual ("[I:org/xmlpull/v1/XmlPullParser.CDSECT, Org.XmlPull.V1.XmlPullParserNode]", removes.Single ().ToString ());

			Assert.AreEqual ("Org.XmlPull.V1.XmlPullParserNode", enums.Single ().Key);
			Assert.AreEqual (false, enums.Single ().Value.BitField);
			Assert.AreEqual (false, enums.Single ().Value.FieldsRemoved);
			Assert.AreEqual ("[Cdsect, I:org/xmlpull/v1/XmlPullParser.CDSECT]", enums.First ().Value.JniNames.Single ().ToString ());
			Assert.AreEqual ("[Cdsect, 5]", enums.First ().Value.Members.Single ().ToString ());
		}

		[Test]
		public void TransientRemoveFieldOnlyTest ()
		{
			// Transient has no effect here
			var csv = $"- ENTER TRANSIENT MODE -{Environment.NewLine}10,,,I:org/xmlpull/v1/XmlPullParser.CDSECT,5";
			var mappings = new EnumMappings (string.Empty, string.Empty, "30", false);
			var sr = new StringReader (csv);

			var removes = new List<KeyValuePair<string, string>> ();
			var enums = mappings.ParseFieldMappings (sr, new string [0], 30, removes);

			Assert.AreEqual ("[I:org/xmlpull/v1/XmlPullParser.CDSECT, ]", removes.Single ().ToString ());

			Assert.AreEqual (0, enums.Count);
		}

		[Test]
		public void TransientAddConstantOnlyTest ()
		{
			// This should only add an enum
			var csv = $"- ENTER TRANSIENT MODE -{Environment.NewLine}10,Org.XmlPull.V1.XmlPullParserNode,Cdsect,,5";
			var mappings = new EnumMappings (string.Empty, string.Empty, "30", false);
			var sr = new StringReader (csv);

			var removes = new List<KeyValuePair<string, string>> ();
			var enums = mappings.ParseFieldMappings (sr, new string [0], 30, removes);

			Assert.AreEqual (0, removes.Count);

			Assert.AreEqual ("Org.XmlPull.V1.XmlPullParserNode", enums.Single ().Key);
			Assert.AreEqual (false, enums.Single ().Value.BitField);
			Assert.AreEqual (false, enums.Single ().Value.FieldsRemoved);
			Assert.AreEqual ("[Cdsect, ]", enums.First ().Value.JniNames.Single ().ToString ());
			Assert.AreEqual ("[Cdsect, 5]", enums.First ().Value.Members.Single ().ToString ());
		}
		[Test]
		public void BasicEnumificationV2Test ()
		{
			// This should create a new enum and remove the field
			var csv = "E,10,I:org/xmlpull/v1/XmlPullParser.CDSECT,5,Org.XmlPull.V1.XmlPullParserNode,Cdsect";
			var mappings = new EnumMappings (string.Empty, string.Empty, "30", false);
			var sr = new StringReader (csv);

			var removes = new List<KeyValuePair<string, string>> ();
			var enums = mappings.ParseFieldMappings (sr, new string [0], 30, removes);

			Assert.AreEqual ("[I:org/xmlpull/v1/XmlPullParser.CDSECT, ]", removes.Single ().ToString ());

			Assert.AreEqual ("Org.XmlPull.V1.XmlPullParserNode", enums.Single ().Key);
			Assert.AreEqual (false, enums.Single ().Value.BitField);
			Assert.AreEqual (true, enums.Single ().Value.FieldsRemoved);
			Assert.AreEqual ("[Cdsect, I:org/xmlpull/v1/XmlPullParser.CDSECT]", enums.First ().Value.JniNames.Single ().ToString ());
			Assert.AreEqual ("[Cdsect, 5]", enums.First ().Value.Members.Single ().ToString ());
		}

		[Test]
		public void RemoveFieldOnlyV2Test ()
		{
			// This should only remove the field
			var csv = "R,10,I:org/xmlpull/v1/XmlPullParser.CDSECT,5";
			var mappings = new EnumMappings (string.Empty, string.Empty, "30", false);
			var sr = new StringReader (csv);

			var removes = new List<KeyValuePair<string, string>> ();
			var enums = mappings.ParseFieldMappings (sr, new string [0], 30, removes);

			Assert.AreEqual ("[I:org/xmlpull/v1/XmlPullParser.CDSECT, ]", removes.Single ().ToString ());

			Assert.AreEqual (0, enums.Count);
		}

		[Test]
		public void AddConstantOnlyV2Test ()
		{
			// This should only add an enum
			var csv = "A,10,,5,Org.XmlPull.V1.XmlPullParserNode,Cdsect";
			var mappings = new EnumMappings (string.Empty, string.Empty, "30", false);
			var sr = new StringReader (csv);

			var removes = new List<KeyValuePair<string, string>> ();
			var enums = mappings.ParseFieldMappings (sr, new string [0], 30, removes);

			Assert.AreEqual (0, removes.Count);

			Assert.AreEqual ("Org.XmlPull.V1.XmlPullParserNode", enums.Single ().Key);
			Assert.AreEqual (false, enums.Single ().Value.BitField);
			Assert.AreEqual (true, enums.Single ().Value.FieldsRemoved);
			Assert.AreEqual ("[Cdsect, ]", enums.First ().Value.JniNames.Single ().ToString ());
			Assert.AreEqual ("[Cdsect, 5]", enums.First ().Value.Members.Single ().ToString ());
		}

		[Test]
		public void FlagsEnumerationV2Test ()
		{
			// This should create a new enum with [Flags] because of the ",Flags" field
			var csv = "E,10,I:org/xmlpull/v1/XmlPullParser.CDSECT,5,Org.XmlPull.V1.XmlPullParserNode,Cdsect,remove,Flags";
			var mappings = new EnumMappings (string.Empty, string.Empty, "30", false);
			var sr = new StringReader (csv);

			var removes = new List<KeyValuePair<string, string>> ();
			var enums = mappings.ParseFieldMappings (sr, new string [0], 30, removes);

			Assert.AreEqual (true, enums.Single ().Value.BitField);
		}

		[Test]
		public void ExternalFlagsEnumerationV2Test ()
		{
			// This should create a new enum with [Flags] because of the "enumFlags" parameter
			var csv = "E,10,I:org/xmlpull/v1/XmlPullParser.CDSECT,5,Org.XmlPull.V1.XmlPullParserNode,Cdsect";
			var mappings = new EnumMappings (string.Empty, string.Empty, "30", false);
			var sr = new StringReader (csv);

			var removes = new List<KeyValuePair<string, string>> ();
			var enums = mappings.ParseFieldMappings (sr, new [] { "Org.XmlPull.V1.XmlPullParserNode" }, 30, removes);

			Assert.AreEqual (true, enums.Single ().Value.BitField);
		}

		[Test]
		public void ApiVersionExcludedV2Test ()
		{
			// This should be completely ignored because it's API=10 and we're looking for API=5
			var csv = "E,10,I:org/xmlpull/v1/XmlPullParser.CDSECT,5,Org.XmlPull.V1.XmlPullParserNode,Cdsect";
			var mappings = new EnumMappings (string.Empty, string.Empty, "5", false);
			var sr = new StringReader (csv);

			var removes = new List<KeyValuePair<string, string>> ();
			var enums = mappings.ParseFieldMappings (sr, new string [0], 5, removes);

			Assert.AreEqual (0, removes.Count);
			Assert.AreEqual (0, enums.Count);
		}

		[Test]
		public void TransientEnumificationV2Test ()
		{
			// This should create a new enum and remove the field
			var csv = "E,10,I:org/xmlpull/v1/XmlPullParser.CDSECT,5,Org.XmlPull.V1.XmlPullParserNode,Cdsect,remove";
			var mappings = new EnumMappings (string.Empty, string.Empty, "30", false);
			var sr = new StringReader (csv);

			var removes = new List<KeyValuePair<string, string>> ();
			var enums = mappings.ParseFieldMappings (sr, new string [0], 30, removes);

			Assert.AreEqual ("[I:org/xmlpull/v1/XmlPullParser.CDSECT, Org.XmlPull.V1.XmlPullParserNode]", removes.Single ().ToString ());

			Assert.AreEqual ("Org.XmlPull.V1.XmlPullParserNode", enums.Single ().Key);
			Assert.AreEqual (false, enums.Single ().Value.BitField);
			Assert.AreEqual (false, enums.Single ().Value.FieldsRemoved);
			Assert.AreEqual ("[Cdsect, I:org/xmlpull/v1/XmlPullParser.CDSECT]", enums.First ().Value.JniNames.Single ().ToString ());
			Assert.AreEqual ("[Cdsect, 5]", enums.First ().Value.Members.Single ().ToString ());
		}
	}
}
