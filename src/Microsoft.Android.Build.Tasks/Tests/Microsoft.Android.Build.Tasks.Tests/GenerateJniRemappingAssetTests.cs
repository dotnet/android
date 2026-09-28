#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Android.Runtime;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable (ParallelScope.Children)]
public class GenerateJniRemappingAssetTests : BaseTest
{
	byte [] Produce (string xml, string name = "default")
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string input = Path.Combine (directory, $"{name}.xml");
		string output = Path.Combine (directory, $"{name}.bin");
		File.WriteAllText (input, xml);
		var errors = new List<BuildErrorEventArgs> ();
		var task = new GenerateJniRemappingAsset {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			RemappingXmlFilePath = input,
			OutputFile = output,
		};

		Assert.IsTrue (task.Execute (), string.Join ("; ", errors.Select (error => error.Message)));
		return File.ReadAllBytes (output);
	}

	[Test]
	public void EmptyMapIsAValidVersionedAsset ()
	{
		byte [] bytes = Produce ("<replacements />");
		var asset = new JniRemappingAsset (bytes);

		Assert.AreEqual (JniRemappingAsset.HeaderSize, bytes.Length);
		Assert.AreEqual (JniRemappingAsset.Magic, JniRemappingAsset.ReadUInt32 (bytes, 0));
		Assert.AreEqual (JniRemappingAsset.Version, JniRemappingAsset.ReadUInt32 (bytes, 4));
		Assert.IsTrue (asset.IsEmpty);
		Assert.IsNull (asset.FindReplacementType ("a/B"));
		Assert.IsNull (asset.FindReverseType ("a/B"));
		Assert.IsNull (asset.FindMethod ("a/B", "run", "()V"));
		Assert.IsNull (asset.FindField ("a/B", "value", "I"));
	}

	[Test]
	public void ResolvesTypesOverloadedMethodsFieldsAndDescriptors ()
	{
		byte [] bytes = Produce ("""
			<replacements>
			  <replace-type from="z/Z" to="q/Q" />
			  <replace-type from="a/B" to="x/Y" />
			  <reverse-type from="x/Y" to="a/B" />
			  <replace-method source-type="a/B" source-method-name="run"
			      target-type="x/Y" target-method-name="wildcard" target-method-instance-to-static="false" />
			  <replace-method source-type="a/B" source-method-name="run" source-method-signature="(I)"
			      target-type="x/Y" target-method-name="parameters" target-method-instance-to-static="false" />
			  <replace-method source-type="a/B" source-method-name="run" source-method-signature="(I)V"
			      target-type="x/Y" target-method-name="exact" target-method-signature="(J)V"
			      target-method-instance-to-static="false" />
			  <replace-method source-type="a/B" source-method-name="run" source-method-signature="(Ljava/lang/String;)V"
			      target-type="x/Y" target-method-name="moved"
			      target-method-signature="(La/B;Ljava/lang/String;)V" target-method-instance-to-static="true" />
			  <replace-method source-type="a/B" source-method-name="&lt;init&gt;"
			      target-type="x/Y" target-method-name="factory" target-method-instance-to-static="true" />
			  <replace-field source-type="a/B" source-field-name="value"
			      target-type="x/Y" target-field-name="anyValue" />
			  <replace-field source-type="a/B" source-field-name="value" source-field-signature="I"
			      target-type="x/Y" target-field-name="exactValue" target-field-signature="J" />
			</replacements>
			""");
		var asset = new JniRemappingAsset (bytes);

		Assert.AreEqual ("x/Y", Read (asset, asset.FindReplacementType ("a/B")));
		Assert.AreEqual ("q/Q", Read (asset, asset.FindReplacementType ("z/Z")));
		Assert.AreEqual ("a/B", Read (asset, asset.FindReverseType ("x/Y")));
		Assert.IsNull (asset.FindReplacementType ("missing/Type"));

		var exact = asset.FindMethod ("a/B", "run", "(I)V");
		Assert.IsTrue (exact.HasValue);
		Assert.AreEqual ("exact", asset.ReadString (exact.GetValueOrDefault ().TargetName));
		Assert.AreEqual ("(J)V", asset.ReadString (exact.GetValueOrDefault ().TargetSignature));
		Assert.AreEqual ("(I)V", asset.ReadString (exact.GetValueOrDefault ().MatchedSignature));

		var parameters = asset.FindMethod (Encoding.UTF8.GetBytes ("a/B"), "run", "(I)J");
		Assert.IsTrue (parameters.HasValue);
		Assert.AreEqual ("parameters", asset.ReadString (parameters.GetValueOrDefault ().TargetName));
		Assert.IsTrue (parameters.GetValueOrDefault ().TargetSignature.IsMissing);
		Assert.AreEqual (0, parameters.GetValueOrDefault ().MatchedSignature.Length);

		var wildcard = asset.FindMethod ("a/B", "run", "(Z)V");
		Assert.IsTrue (wildcard.HasValue);
		Assert.AreEqual ("wildcard", asset.ReadString (wildcard.GetValueOrDefault ().TargetName));
		Assert.IsNull (asset.FindMethod ("a/B", "absent", "()V"));

		var moved = asset.FindMethod ("a/B", "run", "(Ljava/lang/String;)V");
		Assert.IsTrue (moved.HasValue);
		Assert.IsTrue (moved.GetValueOrDefault ().IsStatic);
		Assert.AreEqual ("(La/B;Ljava/lang/String;)V", asset.ReadString (moved.GetValueOrDefault ().TargetSignature));
		var factory = asset.FindMethod ("a/B", "<init>", "()V");
		Assert.IsTrue (factory.HasValue);
		Assert.IsTrue (factory.GetValueOrDefault ().IsStatic);
		Assert.IsTrue (factory.GetValueOrDefault ().TargetSignature.IsMissing);

		var field = asset.FindField ("a/B", "value", "I");
		Assert.IsTrue (field.HasValue);
		Assert.AreEqual ("exactValue", asset.ReadString (field.GetValueOrDefault ().TargetName));
		Assert.AreEqual ("J", asset.ReadString (field.GetValueOrDefault ().TargetSignature));
		var fallback = asset.FindField ("a/B", "value", "Z");
		Assert.IsTrue (fallback.HasValue);
		Assert.AreEqual ("anyValue", asset.ReadString (fallback.GetValueOrDefault ().TargetName));
		Assert.IsTrue (fallback.GetValueOrDefault ().TargetSignature.IsMissing);
		Assert.IsNull (asset.FindField ("a/B", "absent", "I"));
	}

	[Test]
	public void Utf8NamesAndCallerMutationAreSafe ()
	{
		byte [] bytes = Produce ("<replacements>" +
			"<replace-type from=\"test/\u0179rodlo\" to=\"test/\u00c9cho\" />" +
			"<replace-type from=\"test/A\" to=\"test/Alpha\" />" +
			"</replacements>");
		var asset = new JniRemappingAsset (bytes);
		Array.Fill (bytes, (byte)0);

		Assert.AreEqual ("test/\u00c9cho", Read (asset, asset.FindReplacementType ("test/\u0179rodlo")));
		Assert.AreEqual ("test/Alpha", Read (asset, asset.FindReplacementType ("test/A")));
		Assert.IsNull (asset.FindReplacementType ("test/\u0179rodla"));
	}

	[Test]
	public void Utf8MethodAndFieldKeysUseTheSameBinaryOrdering ()
	{
		byte [] bytes = Produce ("<replacements>" +
			"<replace-method source-type=\"test/\u0179rodlo\" source-method-name=\"r\u00e9sum\u00e9\" source-method-signature=\"()V\"" +
			" target-type=\"test/\u00c9cho\" target-method-name=\"a\" target-method-instance-to-static=\"false\" />" +
			"<replace-field source-type=\"test/\u0179rodlo\" source-field-name=\"v\u00e4lue\" source-field-signature=\"I\"" +
			" target-type=\"test/\u00c9cho\" target-field-name=\"b\" target-field-signature=\"J\" />" +
			"</replacements>");
		var asset = new JniRemappingAsset (bytes);
		var method = asset.FindMethod (Encoding.UTF8.GetBytes ("test/\u0179rodlo"), "r\u00e9sum\u00e9", "()V");
		var field = asset.FindField ("test/\u0179rodlo", "v\u00e4lue", "I");

		Assert.IsTrue (method.HasValue);
		Assert.AreEqual ("a", asset.ReadString (method.GetValueOrDefault ().TargetName));
		Assert.IsTrue (field.HasValue);
		Assert.AreEqual ("b", asset.ReadString (field.GetValueOrDefault ().TargetName));
		Assert.IsNull (asset.FindMethod ("test/\u0179rodlo", "resume", "()V"));
		Assert.IsNull (asset.FindField ("test/\u0179rodlo", "value", "I"));
	}

	[TestCase ("truncated-header")]
	[TestCase ("magic")]
	[TestCase ("version")]
	[TestCase ("file-size")]
	[TestCase ("reserved-flags")]
	[TestCase ("table-offset")]
	[TestCase ("table-count")]
	[TestCase ("pool-offset")]
	[TestCase ("string-offset")]
	[TestCase ("string-length")]
	[TestCase ("missing-terminator")]
	[TestCase ("embedded-terminator")]
	[TestCase ("invalid-utf8")]
	[TestCase ("method-flags")]
	[TestCase ("optional-string-length")]
	[TestCase ("empty-optional-string")]
	public void RejectsMalformedAsset (string corruption)
	{
		byte [] bytes = Produce ("""
			<replacements>
			  <replace-type from="a/B" to="x/Y" />
			  <replace-method source-type="a/B" source-method-name="run" source-method-signature="(I)V"
			      target-type="x/Y" target-method-name="renamed" target-method-instance-to-static="false" />
			</replacements>
			""");
		int methodOffset = (int)JniRemappingAsset.ReadUInt32 (bytes, 32);
		uint sourceOffset = JniRemappingAsset.ReadUInt32 (bytes, JniRemappingAsset.HeaderSize);
		uint sourceLength = JniRemappingAsset.ReadUInt32 (bytes, JniRemappingAsset.HeaderSize + 4);
		switch (corruption) {
			case "truncated-header": bytes = new byte [JniRemappingAsset.HeaderSize - 1]; break;
			case "magic": JniRemappingAsset.WriteUInt32 (bytes, 0, 0); break;
			case "version": JniRemappingAsset.WriteUInt32 (bytes, 4, 2); break;
			case "file-size": JniRemappingAsset.WriteUInt32 (bytes, 12, 0); break;
			case "reserved-flags": JniRemappingAsset.WriteUInt32 (bytes, 56, 1); break;
			case "table-offset": JniRemappingAsset.WriteUInt32 (bytes, 16, uint.MaxValue); break;
			case "table-count": JniRemappingAsset.WriteUInt32 (bytes, 20, uint.MaxValue); break;
			case "pool-offset": JniRemappingAsset.WriteUInt32 (bytes, 48, 0); break;
			case "string-offset": JniRemappingAsset.WriteUInt32 (bytes, JniRemappingAsset.HeaderSize, uint.MaxValue); break;
			case "string-length": JniRemappingAsset.WriteUInt32 (bytes, JniRemappingAsset.HeaderSize + 4, uint.MaxValue); break;
			case "missing-terminator": bytes [(int)(sourceOffset + sourceLength)] = (byte)'!'; break;
			case "embedded-terminator": bytes [(int)sourceOffset] = 0; break;
			case "invalid-utf8": bytes [(int)sourceOffset] = 0xff; break;
			case "method-flags": JniRemappingAsset.WriteUInt32 (bytes, methodOffset + 48, 2); break;
			case "optional-string-length": JniRemappingAsset.WriteUInt32 (bytes, methodOffset + 44, 1); break;
			case "empty-optional-string": JniRemappingAsset.WriteUInt32 (bytes, methodOffset + 40, 0); break;
			default: throw new AssertionException ($"Unrecognized corruption: {corruption}");
		}

		Assert.Throws<InvalidDataException> (() => new JniRemappingAsset (bytes));
	}

	[Test]
	public void RejectsDuplicateKeysAndKeepsPreviousOutput ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string input = Path.Combine (directory, "duplicate.xml");
		string output = Path.Combine (directory, "existing.bin");
		File.WriteAllText (input, """
			<replacements>
			  <replace-method source-type="a/B" source-method-name="run" source-method-signature="()V"
			      target-type="a/B" target-method-name="a" target-method-instance-to-static="false" />
			  <replace-method source-type="a/B" source-method-name="run" source-method-signature="()V"
			      target-type="a/B" target-method-name="b" target-method-instance-to-static="false" />
			</replacements>
			""");
		File.WriteAllText (output, "previous output");
		var errors = new List<BuildErrorEventArgs> ();
		var task = new GenerateJniRemappingAsset {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			RemappingXmlFilePath = input,
			OutputFile = output,
		};

		Assert.IsFalse (task.Execute ());
		Assert.AreEqual ("XA4331", errors.Single ().Code);
		StringAssert.Contains ("Duplicate JNI remapping method", errors [0].Message);
		Assert.AreEqual ("previous output", File.ReadAllText (output));
	}

	[Test]
	public void MissingInputIsNotAnEmptyAsset ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		var errors = new List<BuildErrorEventArgs> ();
		var task = new GenerateJniRemappingAsset {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			RemappingXmlFilePath = Path.Combine (directory, "missing.xml"),
			OutputFile = Path.Combine (directory, "missing.bin"),
		};

		Assert.IsFalse (task.Execute ());
		Assert.AreEqual ("XA4331", errors.Single ().Code);
		FileAssert.DoesNotExist (task.OutputFile);
	}

	[Test]
	public void InvalidXmlRootAndEmptyTargetSignatureCannotProduceAnAsset ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string input = Path.Combine (directory, "remap.xml");
		string output = Path.Combine (directory, "asset.bin");
		var errors = new List<BuildErrorEventArgs> ();
		var task = new GenerateJniRemappingAsset {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			RemappingXmlFilePath = input,
			OutputFile = output,
		};

		File.WriteAllText (input, "<unrelated />");
		Assert.IsFalse (task.Execute ());
		Assert.AreEqual ("XA1045", errors.Single ().Code);
		FileAssert.DoesNotExist (output);

		errors.Clear ();
		task = new GenerateJniRemappingAsset {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			RemappingXmlFilePath = input,
			OutputFile = output,
		};
		File.WriteAllText (input, """
			<replacements>
			  <replace-method source-type="a/B" source-method-name="run" source-method-signature="()V"
			      target-type="a/B" target-method-name="renamed" target-method-signature=""
			      target-method-instance-to-static="false" />
			</replacements>
			""");
		Assert.IsFalse (task.Execute ());
		Assert.AreEqual ("XA4331", errors.Single ().Code);
		FileAssert.DoesNotExist (output);
	}

	[Test]
	public void PerRidInputsRemainIndependent ()
	{
		var first = new JniRemappingAsset (Produce (
			"""<replacements><replace-type from="a/Retained" to="x/Arm" /></replacements>""", "arm64-v8a"));
		var second = new JniRemappingAsset (Produce (
			"""<replacements><replace-type from="b/Retained" to="x/X64" /></replacements>""", "x86_64"));

		Assert.AreEqual ("x/Arm", Read (first, first.FindReplacementType ("a/Retained")));
		Assert.IsNull (first.FindReplacementType ("b/Retained"));
		Assert.AreEqual ("x/X64", Read (second, second.FindReplacementType ("b/Retained")));
		Assert.IsNull (second.FindReplacementType ("a/Retained"));
	}

	[Test]
	public void LargeInputRemainsSortedAndSearchable ()
	{
		const int count = 4096;
		var xml = new StringBuilder (count * 400);
		xml.Append ("<replacements>");
		for (int i = count - 1; i >= 0; i--) {
			string id = i.ToString ("D5", CultureInfo.InvariantCulture);
			xml.Append (CultureInfo.InvariantCulture, $"<replace-type from=\"app/T{id}\" to=\"r/T{id}\" />");
			xml.Append (CultureInfo.InvariantCulture, $"<reverse-type from=\"r/T{id}\" to=\"app/T{id}\" />");
			xml.Append (CultureInfo.InvariantCulture, $"<replace-method source-type=\"r/T{id}\" source-method-name=\"call\" source-method-signature=\"(I)V\" target-type=\"r/T{id}\" target-method-name=\"a\" target-method-signature=\"(J)V\" target-method-instance-to-static=\"false\" />");
			xml.Append (CultureInfo.InvariantCulture, $"<replace-field source-type=\"r/T{id}\" source-field-name=\"field\" source-field-signature=\"I\" target-type=\"r/T{id}\" target-field-name=\"b\" target-field-signature=\"J\" />");
		}
		xml.Append ("</replacements>");
		var asset = new JniRemappingAsset (Produce (xml.ToString ()));

		for (int i = 0; i < count; i += 127) {
			string id = i.ToString ("D5", CultureInfo.InvariantCulture);
			Assert.AreEqual ($"r/T{id}", Read (asset, asset.FindReplacementType ($"app/T{id}")));
			Assert.AreEqual ($"app/T{id}", Read (asset, asset.FindReverseType ($"r/T{id}")));
			var method = asset.FindMethod ($"r/T{id}", "call", "(I)V");
			Assert.IsTrue (method.HasValue);
			Assert.AreEqual ("a", asset.ReadString (method.GetValueOrDefault ().TargetName));
			var field = asset.FindField ($"r/T{id}", "field", "I");
			Assert.IsTrue (field.HasValue);
			Assert.AreEqual ("b", asset.ReadString (field.GetValueOrDefault ().TargetName));
		}
	}

	static string? Read (JniRemappingAsset asset, JniRemappingAsset.StringRef? value)
		=> value.HasValue ? asset.ReadString (value.GetValueOrDefault ()) : null;
}
