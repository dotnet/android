using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.Installer.AndroidSDK;

namespace Xamarin.Android.Tools.Tests;

[TestClass]
public class AndroidPropertiesTests
{
	[TestMethod]
	[DataRow ("key=value", "key", "value")]
	[DataRow (" \t\fkey \t\f= \t\fvalue", "key", "value")]
	[DataRow ("key:value", "key", "value")]
	[DataRow ("key \t\f: \t\fvalue", "key", "value")]
	[DataRow ("key value", "key", "value")]
	[DataRow ("key", "key", "")]
	[DataRow ("key \t\f", "key", "")]
	[DataRow ("key=", "key", "")]
	[DataRow ("key:", "key", "")]
	[DataRow ("=value", "", "value")]
	[DataRow (":value", "", "value")]
	[DataRow ("=", "", "")]
	[DataRow ("key==value", "key", "=value")]
	[DataRow ("key=:value", "key", ":value")]
	[DataRow ("key=value \t\f", "key", "value \t\f")]
	[DataRow ("key=#value!:=end", "key", "#value!:=end")]
	[DataRow ("key=\"value\"", "key", "\"value\"")]
	[DataRow ("key='value'", "key", "'value'")]
	[DataRow ("\\#key=value", "#key", "value")]
	[DataRow ("\\!key=value", "!key", "value")]
	[DataRow ("a\\ b\\:c\\=d=value", "a b:c=d", "value")]
	[DataRow ("key=\\ value", "key", " value")]
	[DataRow ("key=\\t\\r\\n\\f\\\\", "key", "\t\r\n\f\\")]
	[DataRow ("\\t\\r\\n\\f\\\\=value", "\t\r\n\f\\", "value")]
	[DataRow ("key=\\b\\z\\0", "key", "bz0")]
	[DataRow ("\\u006bey=\\u00e9\\u4E2D", "key", "\u00e9\u4e2d")]
	[DataRow ("key=\\uD83D\\uDE00", "key", "\ud83d\ude00")]
	[DataRow ("key=\\u000g\\u000Z", "key", "\u0010\u0023")]
	[DataRow ("key=\\uu000", "key", "\ue000")]
	[DataRow ("key=first\\\n \t\fsecond", "key", "firstsecond")]
	[DataRow ("key=first\\\r \t\fsecond", "key", "firstsecond")]
	[DataRow ("key=first\\\r\n \t\fsecond", "key", "firstsecond")]
	[DataRow ("ke\\\n \t\fy=value", "key", "value")]
	[DataRow ("\\\n \t\fkey=value", "key", "value")]
	[DataRow ("key=\\\n \t\fvalue", "key", "value")]
	[DataRow ("key=\\\n#value", "key", "#value")]
	[DataRow ("key=\\\n!value", "key", "!value")]
	[DataRow ("key=\\\\\n", "key", "\\")]
	[DataRow ("key=\\\\\\\nvalue", "key", "\\value")]
	[DataRow ("key=first\\\n \t\f\n", "key", "first")]
	[DataRow ("key=first\\\n", "key", "first")]
	[DataRow ("key=first\\", "key", "first\uffff")]
	[DataRow ("key\\", "key\uffff", "")]
	[DataRow ("\\", "\uffff", "")]
	[DataRow ("\\\n", "", "")]
	[DataRow ("key=\\\n", "key", "")]
	[DataRow ("\u00e9=\u00ff", "\u00e9", "\u00ff")]
	[DataRow ("\u00ef\u00bb\u00bfkey=value", "\u00ef\u00bb\u00bfkey", "value")]
	[DataRow ("a\0b=c\0d", "a\0b", "c\0d")]
	public void ReadProperty (string contents, string key, string value)
	{
		var properties = ReadProperties (contents);
		Assert.AreEqual (1, properties.Count);
		Assert.AreEqual (value, properties [key]);
	}

	[TestMethod]
	[DataRow (1019, "\\u00e9", "\u00e9")]
	[DataRow (1020, "\\u00e9", "\u00e9")]
	[DataRow (1021, "\\u00e9", "\u00e9")]
	[DataRow (1022, "\\u00e9", "\u00e9")]
	[DataRow (1023, "\\u00e9", "\u00e9")]
	[DataRow (1024, "\\u00e9", "\u00e9")]
	[DataRow (1022, "\\\r\n \t\f", "")]
	[DataRow (1023, "\\\r\n \t\f", "")]
	[DataRow (1024, "\\\r\n \t\f", "")]
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

	[TestMethod]
	[DataRow ("\n")]
	[DataRow ("\r")]
	[DataRow ("\r\n")]
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

	[TestMethod]
	[DataRow ("")]
	[DataRow (" \t\f\r\n")]
	[DataRow ("# comment\\")]
	[DataRow ("! comment")]
	public void ReadEmptyProperties (string contents)
	{
		Assert.IsEmpty (ReadProperties (contents));
	}

	[TestMethod]
	[DataRow ("key=\\u")]
	[DataRow ("key=\\u123")]
	[DataRow ("key=\\u00-0")]
	[DataRow ("\\u12\n3=value")]
	public void ReadInvalidUnicodeEscape (string contents)
	{
		var exception = Assert.ThrowsExactly<InvalidOperationException> (() => ReadProperties (contents));
		Assert.IsNotNull (exception);
		Assert.IsInstanceOfType<FormatException> (exception.InnerException);
		Assert.AreEqual ("Invalid Unicode character.", exception.InnerException?.Message);
	}

	[TestMethod]
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
