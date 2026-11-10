using System;
using Java.Interop.Tools.Generator.Enumification;
using NUnit.Framework;

namespace Java.Interop.Tools.Generator_Tests
{
	public class ConstantEntryTests
	{
		[Test]
		public void ParseEnumMapV1 ()
		{
			var csv = "10,Org.XmlPull.V1.XmlPullParserNode,Cdsect,I:org/xmlpull/v1/XmlPullParser.CDSECT,5";
			var entry = ConstantEntry.FromString (csv);

			Assert.AreEqual (ConstantAction.Enumify, entry.Action);
			Assert.AreEqual (10, entry.ApiLevel.ApiLevel);
			Assert.AreEqual ("I:org/xmlpull/v1/XmlPullParser.CDSECT", entry.JavaSignature);
			Assert.AreEqual ("5", entry.Value);
			Assert.AreEqual ("Org.XmlPull.V1.XmlPullParserNode", entry.EnumFullType);
			Assert.AreEqual ("Cdsect", entry.EnumMember);
			Assert.AreEqual (FieldAction.Keep, entry.FieldAction);
			Assert.False (entry.IsFlags);
		}

		[Test]
		public void ParseTransientEnumMapV1 ()
		{
			var csv = "10,Org.XmlPull.V1.XmlPullParserNode,Cdsect,I:org/xmlpull/v1/XmlPullParser.CDSECT,5";
			var entry = ConstantEntry.FromString (csv, true);

			Assert.AreEqual (ConstantAction.Enumify, entry.Action);
			Assert.AreEqual (10, entry.ApiLevel.ApiLevel);
			Assert.AreEqual ("I:org/xmlpull/v1/XmlPullParser.CDSECT", entry.JavaSignature);
			Assert.AreEqual ("5", entry.Value);
			Assert.AreEqual ("Org.XmlPull.V1.XmlPullParserNode", entry.EnumFullType);
			Assert.AreEqual ("Cdsect", entry.EnumMember);
			Assert.AreEqual (FieldAction.Remove, entry.FieldAction);
			Assert.False (entry.IsFlags);
		}

		[Test]
		public void ParseAddEnumMapV1 ()
		{
			var csv = "10,Org.XmlPull.V1.XmlPullParserNode,Cdsect,,5";
			var entry = ConstantEntry.FromString (csv);

			Assert.AreEqual (ConstantAction.Add, entry.Action);
			Assert.AreEqual (10, entry.ApiLevel.ApiLevel);
			Assert.AreEqual (string.Empty, entry.JavaSignature);
			Assert.AreEqual ("5", entry.Value);
			Assert.AreEqual ("Org.XmlPull.V1.XmlPullParserNode", entry.EnumFullType);
			Assert.AreEqual ("Cdsect", entry.EnumMember);
			Assert.AreEqual (FieldAction.None, entry.FieldAction);
			Assert.False (entry.IsFlags);
		}

		[Test]
		public void ParseRemoveEnumMapV1 ()
		{
			var csv = "10,,,I:org/xmlpull/v1/XmlPullParser.CDSECT,5";
			var entry = ConstantEntry.FromString (csv);

			Assert.AreEqual (ConstantAction.Remove, entry.Action);
			Assert.AreEqual (10, entry.ApiLevel.ApiLevel);
			Assert.AreEqual ("I:org/xmlpull/v1/XmlPullParser.CDSECT", entry.JavaSignature);
			Assert.AreEqual ("5", entry.Value);
			Assert.AreEqual (string.Empty, entry.EnumFullType);
			Assert.AreEqual (string.Empty, entry.EnumMember);
			Assert.AreEqual (FieldAction.Remove, entry.FieldAction);
			Assert.False (entry.IsFlags);
		}

		[Test]
		public void ParseEnumMapV2 ()
		{
			var csv = "E,10,I:org/xmlpull/v1/XmlPullParser.CDSECT,5,Org.XmlPull.V1.XmlPullParserNode,Cdsect,keep";
			var entry = ConstantEntry.FromString (csv);

			Assert.AreEqual (ConstantAction.Enumify, entry.Action);
			Assert.AreEqual (10, entry.ApiLevel.ApiLevel);
			Assert.AreEqual ("I:org/xmlpull/v1/XmlPullParser.CDSECT", entry.JavaSignature);
			Assert.AreEqual ("5", entry.Value);
			Assert.AreEqual ("Org.XmlPull.V1.XmlPullParserNode", entry.EnumFullType);
			Assert.AreEqual ("Cdsect", entry.EnumMember);
			Assert.AreEqual (FieldAction.Keep, entry.FieldAction);
			Assert.False (entry.IsFlags);
			Assert.IsNull (entry.DeprecatedSince);
		}

		[Test]
		public void ParseAddEnumMapV2 ()
		{
			var csv = "A,10,,5,Org.XmlPull.V1.XmlPullParserNode,Cdsect";
			var entry = ConstantEntry.FromString (csv);

			Assert.AreEqual (ConstantAction.Add, entry.Action);
			Assert.AreEqual (10, entry.ApiLevel.ApiLevel);
			Assert.AreEqual (string.Empty, entry.JavaSignature);
			Assert.AreEqual ("5", entry.Value);
			Assert.AreEqual ("Org.XmlPull.V1.XmlPullParserNode", entry.EnumFullType);
			Assert.AreEqual ("Cdsect", entry.EnumMember);
			Assert.AreEqual (FieldAction.None, entry.FieldAction);
			Assert.IsNull (entry.DeprecatedSince);
			Assert.False (entry.IsFlags);
		}

		[Test]
		public void ParseRemoveEnumMapV2 ()
		{
			var csv = "R,10,I:org/xmlpull/v1/XmlPullParser.CDSECT,5,,,remove,,33";
			var entry = ConstantEntry.FromString (csv);

			Assert.AreEqual (ConstantAction.Remove, entry.Action);
			Assert.AreEqual (10, entry.ApiLevel.ApiLevel);
			Assert.AreEqual ("I:org/xmlpull/v1/XmlPullParser.CDSECT", entry.JavaSignature);
			Assert.AreEqual ("5", entry.Value);
			Assert.AreEqual (string.Empty, entry.EnumFullType);
			Assert.AreEqual (string.Empty, entry.EnumMember);
			Assert.AreEqual (FieldAction.Remove, entry.FieldAction);
			Assert.False (entry.IsFlags);
			Assert.AreEqual (33, entry.DeprecatedSince?.ApiLevel ?? 0);
		}

		[TestCase ("flags", true)]
		[TestCase ("FLAGS", true)]
		[TestCase ("Flags", true)]
		[TestCase (" flags ", true)]
		[TestCase ("", false)]
		[TestCase ("flag", false)]
		[TestCase ("flags2", false)]
		[TestCase ("\uFF46lags", false)]
		public void ParseIsFlags (string value, bool expected)
		{
			var v1 = ConstantEntry.FromString ($"10,Org.XmlPull.V1.XmlPullParserNode,Cdsect,I:org/xmlpull/v1/XmlPullParser.CDSECT,5,{value}");
			var v2 = ConstantEntry.FromString ($"E,10,I:org/xmlpull/v1/XmlPullParser.CDSECT,5,Org.XmlPull.V1.XmlPullParserNode,Cdsect,keep,{value}");

			Assert.AreEqual (expected, v1.IsFlags);
			Assert.AreEqual (expected, v2.IsFlags);
		}

		[TestCase ("keep", FieldAction.Keep)]
		[TestCase ("KEEP", FieldAction.Keep)]
		[TestCase ("\u212Aeep", FieldAction.Keep)]
		[TestCase ("Remove", FieldAction.Remove)]
		[TestCase ("", FieldAction.None)]
		public void ParseFieldAction (string value, FieldAction expected)
		{
			var entry = ConstantEntry.FromString ($"E,10,I:org/xmlpull/v1/XmlPullParser.CDSECT,5,Org.XmlPull.V1.XmlPullParserNode,Cdsect,{value}");

			Assert.AreEqual (expected, entry.FieldAction);
		}
	}
}
