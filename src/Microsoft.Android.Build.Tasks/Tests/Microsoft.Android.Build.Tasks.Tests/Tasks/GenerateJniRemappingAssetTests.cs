#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;

using Microsoft.Android.Runtime;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class GenerateJniRemappingAssetTests : BaseTest
{
	readonly List<BuildErrorEventArgs> errors = new ();
	string directory = "";

	[SetUp]
	public void Setup ()
	{
		errors.Clear ();
		directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
	}

	byte [] Generate (string? xml)
	{
		var task = CreateTask (xml);
		Assert.IsTrue (task.Execute (), string.Join ("; ", errors.Select (e => e.Message)));
		return File.ReadAllBytes (task.OutputFile);
	}

	GenerateJniRemappingAsset CreateTask (string? xml)
	{
		string? input = null;
		if (xml is not null) {
			input = Path.Combine (directory, "remap.xml");
			File.WriteAllText (input, xml);
		}
		return new GenerateJniRemappingAsset {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			RemappingXmlFilePath = input,
			OutputFile = Path.Combine (directory, "jni-remap.bin"),
		};
	}

	[Test]
	public void EmptyInputProducesValidEmptyAsset ()
	{
		var bytes = Generate (null);
		Assert.AreEqual (JniRemappingAsset.HeaderSize, bytes.Length);
		Assert.IsTrue (new JniRemappingAsset (bytes).IsEmpty);
		CollectionAssert.AreEqual (bytes, Generate ("<replacements />"));
	}

	[Test]
	public void RoundTripPreservesLookupOrderAndOptionalSignatures ()
	{
		const string xml = """
			<replacements>
			  <replace-type from="zz/Last" to="x/Last" />
			  <replace-type from="aa/First" to="x/First" />
			  <replace-type from="型/名前" to="x/Unicode" />
			  <reverse-type from="x/Unicode" to="型/名前" />
			  <replace-method source-type="a/B" source-method-name="m"
			      target-type="x/Y" target-method-name="wildcard" target-method-instance-to-static="false" />
			  <replace-method source-type="a/B" source-method-name="m" source-method-signature="(I)"
			      target-type="x/Y" target-method-name="parameters" target-method-instance-to-static="false" />
			  <replace-method source-type="a/B" source-method-name="m" source-method-signature="(I)V"
			      target-type="x/Y" target-method-name="exact" target-method-signature="(J)V" target-method-instance-to-static="false" />
			  <replace-method source-type="a/B" source-method-name="forward"
			      target-type="x/Y" target-method-name="staticTarget" target-method-instance-to-static="true" />
			  <replace-field source-type="a/B" source-field-name="value"
			      target-type="x/Y" target-field-name="wildcardField" />
			  <replace-field source-type="a/B" source-field-name="value" source-field-signature="I"
			      target-type="x/Y" target-field-name="integerField" target-field-signature="J" />
			  <replace-field source-type="a/B" source-field-name="value" source-field-signature="Ljava/lang/String;"
			      target-type="x/Y" target-field-name="stringField" />
			</replacements>
			""";
		var bytes = Generate (xml);
		var reversed = XDocument.Parse (xml);
		var root = reversed.Root ?? throw new AssertionException ("XML has no root.");
		root.ReplaceNodes (root.Elements ().Reverse ().ToArray ());
		CollectionAssert.AreEqual (bytes, Generate (reversed.ToString ()), "XML ordering must not affect serialized bytes.");

		var asset = new JniRemappingAsset (bytes);
		foreach (var (source, target) in new [] { ("aa/First", "x/First"), ("zz/Last", "x/Last"), ("型/名前", "x/Unicode") }) {
			Assert.AreEqual (target, asset.ReadString (asset.FindReplacementType (source)
				?? throw new AssertionException ($"Missing replacement for {source}.")));
		}
		Assert.IsNull (asset.FindReplacementType ("0/Before"));
		Assert.IsNull (asset.FindReplacementType ("\uffff/After"));
		Assert.AreEqual ("型/名前", asset.ReadString (asset.FindReverseType ("x/Unicode")
			?? throw new AssertionException ("Reverse remap was lost.")));

		var exact = asset.FindMethod ("a/B", "m", "(I)V") ?? throw new AssertionException ("Exact method was lost.");
		Assert.AreEqual ("exact", asset.ReadString (exact.TargetName));
		Assert.AreEqual ("(J)V", asset.ReadString (exact.TargetSignature));
		Assert.AreEqual ("(I)V", asset.ReadString (exact.MatchedSignature));
		foreach (var (signature, target) in new [] { ("(I)I", "parameters"), ("(J)V", "wildcard") }) {
			var method = asset.FindMethod ("a/B", "m", signature) ?? throw new AssertionException ("Fallback method was lost.");
			Assert.AreEqual (target, asset.ReadString (method.TargetName));
			Assert.IsTrue (method.TargetSignature.IsMissing, "Absent target signatures must use the caller's descriptor.");
			Assert.AreEqual (0, method.MatchedSignature.Length, "A partial descriptor must never replace the caller's full descriptor.");
		}
		var forward = asset.FindMethod ("a/B", "forward", "()V") ?? throw new AssertionException ("Forwarding method was lost.");
		Assert.IsTrue (forward.IsStatic);
		Assert.IsTrue (forward.TargetSignature.IsMissing);
		foreach (var (signature, target, targetSignature) in new [] {
				("I", "integerField", "J"), ("Ljava/lang/String;", "stringField", null), ("J", "wildcardField", null),
			}) {
			var field = asset.FindField ("a/B", "value", signature) ?? throw new AssertionException ("Field remap was lost.");
			Assert.AreEqual (target, asset.ReadString (field.TargetName));
			Assert.AreEqual (targetSignature, field.TargetSignature.IsMissing ? null : asset.ReadString (field.TargetSignature));
		}
	}

	[Test]
	public void DuplicateLookupKeysKeepFirstXmlReplacement ()
	{
		var asset = new JniRemappingAsset (Generate ("""
			<replacements>
			  <replace-type from="a/B" to="first/Type" />
			  <replace-type from="a/B" to="later/Type" />
			  <replace-method source-type="a/B" source-method-name="m"
			      target-type="x/Y" target-method-name="firstMethod" target-method-instance-to-static="false" />
			  <replace-method source-type="a/B" source-method-name="m"
			      target-type="x/Y" target-method-name="laterMethod" target-method-instance-to-static="false" />
			  <replace-field source-type="a/B" source-field-name="f"
			      target-type="x/Y" target-field-name="firstField" />
			  <replace-field source-type="a/B" source-field-name="f"
			      target-type="x/Y" target-field-name="laterField" />
			</replacements>
			"""));
		Assert.AreEqual ("first/Type", asset.ReadString (asset.FindReplacementType ("a/B")
			?? throw new AssertionException ("Type remap was lost.")));
		Assert.AreEqual ("firstMethod", asset.ReadString ((asset.FindMethod ("a/B", "m", "()V")
			?? throw new AssertionException ("Method remap was lost.")).TargetName));
		Assert.AreEqual ("firstField", asset.ReadString ((asset.FindField ("a/B", "f", "I")
			?? throw new AssertionException ("Field remap was lost.")).TargetName));
	}

	[Test]
	public void ParserRejectsCorruptRangesAndUtf8BeforeLookup ()
	{
		var bytes = Generate ("""
			<replacements>
			  <replace-type from="a/B" to="x/Y" />
			  <replace-method source-type="a/B" source-method-name="m"
			      target-type="x/Y" target-method-name="n" target-method-instance-to-static="false" />
			</replacements>
			""");
		void Reject (Action<byte []> corrupt)
		{
			var copy = (byte [])bytes.Clone ();
			corrupt (copy);
			Assert.Throws<InvalidDataException> (() => new JniRemappingAsset (copy));
		}
		Assert.Throws<InvalidDataException> (() => new JniRemappingAsset (bytes.AsSpan (0, JniRemappingAsset.HeaderSize - 1)));
		Reject (data => JniRemappingAsset.WriteUInt32 (data, 4, 2));
		Reject (data => JniRemappingAsset.WriteUInt32 (data, 20, uint.MaxValue));
		Reject (data => JniRemappingAsset.WriteUInt32 (data, JniRemappingAsset.HeaderSize, 0));
		Reject (data => JniRemappingAsset.WriteUInt32 (data, JniRemappingAsset.HeaderSize + 4, uint.MaxValue));
		Reject (data => data [data.Length - 1] = 1);
		Reject (data => data [(int)JniRemappingAsset.ReadUInt32 (data, 48)] = 0xff);
		Reject (data => JniRemappingAsset.WriteUInt32 (data, (int)JniRemappingAsset.ReadUInt32 (data, 32) + 48, 2));
	}

	[Test]
	public void ValidatedPrivateBytesKeepUtf8PointersStable ()
	{
		var bytes = Generate ("""<replacements><replace-type from="a/B" to="x/Y" /></replacements>""");
		var asset = new JniRemappingAsset (bytes);
		var target = asset.FindReplacementType ("a/B") ?? throw new AssertionException ("Type remap was lost.");
		var handle = asset.Pin ();
		try {
			IntPtr pointer = IntPtr.Add (handle.AddrOfPinnedObject (), checked ((int)target.Offset));
			Array.Fill (bytes, (byte)0xff);
			GC.Collect ();
			GC.WaitForPendingFinalizers ();
			GC.Collect ();
			Assert.AreEqual ("x/Y", asset.ReadString (target));
			Assert.AreEqual ("x/Y", Marshal.PtrToStringUTF8 (pointer));
			Assert.AreEqual (pointer, IntPtr.Add (handle.AddrOfPinnedObject (), checked ((int)target.Offset)));
		} finally {
			handle.Free ();
		}
	}

	[TestCase ("<not-replacements />", "XA1045")]
	[TestCase ("""<replacements><replace-type from="a/B" /></replacements>""", "XA1047")]
	[TestCase ("""<replacements><replace-method source-type="a/B" source-method-name="m" target-type="x/Y" target-method-name="n" target-method-instance-to-static="invalid" /></replacements>""", "XA1046")]
	[TestCase ("<replacements>", "XA4331")]
	public void InvalidXmlFailsWithCodedDiagnostic (string xml, string code)
	{
		var task = CreateTask (xml);
		Assert.IsFalse (task.Execute ());
		Assert.IsTrue (errors.Any (error => error.Code == code));
		FileAssert.DoesNotExist (task.OutputFile);
	}

	[Test]
	public void MissingInputDoesNotProduceAnEmptyAsset ()
	{
		var task = CreateTask (null);
		task.RemappingXmlFilePath = Path.Combine (directory, "missing.xml");
		Assert.IsFalse (task.Execute ());
		Assert.IsTrue (errors.Any (error => error.Code == "XA4331"));
		FileAssert.DoesNotExist (task.OutputFile);
	}
}
