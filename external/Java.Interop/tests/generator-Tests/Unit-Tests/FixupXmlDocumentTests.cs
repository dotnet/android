using System;
using System.Xml.Linq;
using Java.Interop.Tools.Generator;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class FixupXmlDocumentTests
	{
		[Test]
		public void RemoveNode ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument ("<remove-node path=\"/api/package[@name='android']\" />");

			api.ApplyFixupFile (fixup);

			Assert.AreEqual ("<api><package name='java' jni-name='java' /></api>", api.ApiDocument.ToString (SaveOptions.DisableFormatting).Replace ('\"', '\''));
		}

		[Test]
		public void AddNode ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument ("<add-node path=\"/api\"><package name='new-package' /></add-node>");

			api.ApplyFixupFile (fixup);

			Assert.AreEqual ("<api><package name='android' jni-name='android' /><package name='java' jni-name='java' /><package name='new-package' /></api>", api.ApiDocument.ToString (SaveOptions.DisableFormatting).Replace ('\"', '\''));
		}

		[Test]
		public void ChangeNode ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument ("<change-node path=\"/api/package[@name='android']\">method</change-node>");

			api.ApplyFixupFile (fixup);

			Assert.AreEqual ("<api><method name='android' jni-name='android' /><package name='java' jni-name='java' /></api>", api.ApiDocument.ToString (SaveOptions.DisableFormatting).Replace ('\"', '\''));
		}

		[Test]
		public void MoveNode ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument ("<move-node path=\"/api/package[@name='java']\">/api/package[@name='android']</move-node>");

			api.ApplyFixupFile (fixup);

			Assert.AreEqual ("<api><package name='android' jni-name='android'><package name='java' jni-name='java' /></package></api>", api.ApiDocument.ToString (SaveOptions.DisableFormatting).Replace ('\"', '\''));
		}

		[Test]
		public void SetNewAttribute ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument ("<attr path=\"/api/package[@name='android']\" name='new-attr'>true</attr>");

			api.ApplyFixupFile (fixup);

			Assert.AreEqual ("<api><package name='android' jni-name='android' new-attr='true' /><package name='java' jni-name='java' /></api>", api.ApiDocument.ToString (SaveOptions.DisableFormatting).Replace ('\"', '\''));
		}

		[Test]
		public void ChangeAttribute ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument ("<attr path=\"/api/package[@name='android']\" name='name'>android2</attr>");

			api.ApplyFixupFile (fixup);

			Assert.AreEqual ("<api><package name='android2' jni-name='android' /><package name='java' jni-name='java' /></api>", api.ApiDocument.ToString (SaveOptions.DisableFormatting).Replace ('\"', '\''));
		}

		[Test]
		public void RemoveAttribute ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument ("<remove-attr path=\"/api/package[@name='android']\" name='name' />");

			api.ApplyFixupFile (fixup);

			Assert.AreEqual ("<api><package /><package name='java' jni-name='java' /></api>", api.ApiDocument.ToString (SaveOptions.DisableFormatting).Replace ('\"', '\''));
		}

		ApiXmlDocument GetXmlApiDocument ()
		{
			var api = "<api><package name='android' jni-name='android' /><package name='java' jni-name='java' /></api>";

			return new ApiXmlDocument (XDocument.Parse (api), "30", 0);
		}

		FixupXmlDocument GetFixupXmlDocument (string text)
		{
			return new FixupXmlDocument (XDocument.Parse ("<metadata>" + text + "</metadata>"));
		}
	}
}
