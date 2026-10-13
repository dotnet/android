using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Xamarin.Installer.AndroidSDK;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
public class AndroidPropertiesTests
{
	[TestCase ("key=value", "key", "value")]
	[TestCase (" \t\fkey \t\f= \t\fvalue", "key", "value")]
	[TestCase ("key:value", "key", "value")]
	[TestCase ("key \t\f: \t\fvalue", "key", "value")]
	[TestCase ("key value", "key", "value")]
	[TestCase ("key", "key", "")]
	[TestCase ("key \t\f", "key", "")]
	[TestCase ("key=", "key", "")]
	[TestCase ("key:", "key", "")]
	[TestCase ("=value", "", "value")]
	[TestCase (":value", "", "value")]
	[TestCase ("=", "", "")]
	[TestCase ("key==value", "key", "=value")]
	[TestCase ("key=:value", "key", ":value")]
	[TestCase ("key=value \t\f", "key", "value \t\f")]
	[TestCase ("key=#value!:=end", "key", "#value!:=end")]
	[TestCase ("key=\"value\"", "key", "\"value\"")]
	[TestCase ("key='value'", "key", "'value'")]
	[TestCase ("\\#key=value", "#key", "value")]
	[TestCase ("\\!key=value", "!key", "value")]
	[TestCase ("a\\ b\\:c\\=d=value", "a b:c=d", "value")]
	[TestCase ("key=\\ value", "key", " value")]
	[TestCase ("key=\\t\\r\\n\\f\\\\", "key", "\t\r\n\f\\")]
	[TestCase ("\\t\\r\\n\\f\\\\=value", "\t\r\n\f\\", "value")]
	[TestCase ("key=\\b\\z\\0", "key", "bz0")]
	[TestCase ("\\u006bey=\\u00e9\\u4E2D", "key", "\u00e9\u4e2d")]
	[TestCase ("key=\\uD83D\\uDE00", "key", "\ud83d\ude00")]
	[TestCase ("key=\\u000g\\u000Z", "key", "\u0010\u0023")]
	[TestCase ("key=\\uu000", "key", "\ue000")]
	[TestCase ("key=first\\\n \t\fsecond", "key", "firstsecond")]
	[TestCase ("key=first\\\r \t\fsecond", "key", "firstsecond")]
	[TestCase ("key=first\\\r\n \t\fsecond", "key", "firstsecond")]
	[TestCase ("ke\\\n \t\fy=value", "key", "value")]
	[TestCase ("\\\n \t\fkey=value", "key", "value")]
	[TestCase ("key=\\\n \t\fvalue", "key", "value")]
	[TestCase ("key=\\\n#value", "key", "#value")]
	[TestCase ("key=\\\n!value", "key", "!value")]
	[TestCase ("key=\\\\\n", "key", "\\")]
	[TestCase ("key=\\\\\\\nvalue", "key", "\\value")]
	[TestCase ("key=first\\\n \t\f\n", "key", "first")]
	[TestCase ("key=first\\\n", "key", "first")]
	[TestCase ("key=first\\", "key", "first\uffff")]
	[TestCase ("key\\", "key\uffff", "")]
	[TestCase ("\\", "\uffff", "")]
	[TestCase ("\\\n", "", "")]
	[TestCase ("key=\\\n", "key", "")]
	[TestCase ("\u00e9=\u00ff", "\u00e9", "\u00ff")]
	[TestCase ("\u00ef\u00bb\u00bfkey=value", "\u00ef\u00bb\u00bfkey", "value")]
	[TestCase ("a\0b=c\0d", "a\0b", "c\0d")]
	public void ReadProperty (string contents, string key, string value)
	{
		var properties = ReadProperties (contents);
		Assert.AreEqual (1, properties.Count);
		Assert.AreEqual (value, properties [key]);
	}

	[TestCase (1019, "\\u00e9", "\u00e9")]
	[TestCase (1020, "\\u00e9", "\u00e9")]
	[TestCase (1021, "\\u00e9", "\u00e9")]
	[TestCase (1022, "\\u00e9", "\u00e9")]
	[TestCase (1023, "\\u00e9", "\u00e9")]
	[TestCase (1024, "\\u00e9", "\u00e9")]
	[TestCase (1022, "\\\r\n \t\f", "")]
	[TestCase (1023, "\\\r\n \t\f", "")]
	[TestCase (1024, "\\\r\n \t\f", "")]
	public void ReadPropertyAcrossReaderBufferBoundary (int escapeOffset, string escape, string decodedEscape)
	{
		// Latin-1 uses one byte per character; StreamReader's default byte buffer is 1024 bytes.
		const string key = "key=";
		string prefix = new string ('x', escapeOffset - key.Length);
		var properties = ReadProperties (key + prefix + escape + "tail\nother=value");
		Assert.AreEqual (2, properties.Count);
		Assert.AreEqual (prefix + decodedEscape + "tail", properties ["key"]);
		Assert.AreEqual ("value", properties ["other"]);
	}

	[TestCase ("\n")]
	[TestCase ("\r")]
	[TestCase ("\r\n")]
	public void ReadMultipleProperties (string newline)
	{
		string contents = string.Join (newline, [
			"",
			" \t\f# comment\\",
			" \t\f! comment",
			" \t\f",
			"key=first",
			"KEY=uppercase",
			"key=last",
			"other=value",
		]);
		var properties = ReadProperties (contents);
		Assert.AreEqual (3, properties.Count);
		Assert.AreEqual ("last", properties ["key"]);
		Assert.AreEqual ("uppercase", properties ["KEY"]);
		Assert.AreEqual ("value", properties ["other"]);
	}

	[TestCase ("")]
	[TestCase (" \t\f\r\n")]
	[TestCase ("# comment\\")]
	[TestCase ("! comment")]
	public void ReadEmptyProperties (string contents)
	{
		Assert.IsEmpty (ReadProperties (contents));
	}

	[TestCase ("key=\\u")]
	[TestCase ("key=\\u123")]
	[TestCase ("key=\\u00-0")]
	[TestCase ("\\u12\n3=value")]
	public void ReadInvalidUnicodeEscape (string contents)
	{
		var exception = Assert.Throws<InvalidOperationException> (() => ReadProperties (contents));
		Assert.IsNotNull (exception);
		Assert.IsInstanceOf<FormatException> (exception.InnerException);
		Assert.AreEqual ("Invalid Unicode character.", exception.InnerException?.Message);
	}

	[Test]
	public void ReadMissingProperties ()
	{
		Assert.IsEmpty (AndroidUtilities.ReadAndroidProperties (null));
		Assert.IsEmpty (AndroidUtilities.ReadAndroidProperties (""));
		Assert.IsEmpty (AndroidUtilities.ReadAndroidProperties (Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ("N"))));
	}

	static Dictionary<string, string> ReadProperties (string contents)
	{
		string path = Path.Combine (Path.GetTempPath (), "android-properties-" + Guid.NewGuid ().ToString ("N"));
		try {
			File.WriteAllText (path, contents, Encoding.GetEncoding (28591));
			return AndroidUtilities.ReadAndroidProperties (path);
		} finally {
			File.Delete (path);
		}
	}
}
