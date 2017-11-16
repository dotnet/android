using System;
using System.IO;
using NUnit.Framework;
using Xamarin.Android.Tools.Bytecode;

namespace Xamarin.Android.Tools.BytecodeTests
{
	[TestFixture]
	public class ParameterFixupTests : ClassFileFixture {

		[Test]
		public void XmlDeclaration_FixedUpFromOtherClasses ()
		{
			AssertXmlDeclaration (new [] {"IParameterInterface.class","ParameterAbstractClass.class", "ParameterClass.class", "ParameterClass2.class"}, "ParameterFixup.xml");
		}

		[Test]
		public void XmlDeclaration_FixedUpFromDocumentation()
		{
			var androidSdkPath  = Environment.GetEnvironmentVariable ("ANDROID_SDK_PATH");
			if (string.IsNullOrEmpty (androidSdkPath)) {
				Assert.Ignore ("The `ANDROID_SDK_PATH` environment variable isn't set; " +
						"cannot test importing parameter names from HTML. Skipping...");
				return;
			}
			try {
				AssertXmlDeclaration ("Collection.class", "ParameterFixupFromDocs.xml", Path.Combine (Environment.GetEnvironmentVariable ("ANDROID_SDK_PATH"), "docs", "reference"));
			} catch (Exception ex) {
				Assert.Fail ("An unexpected exception was thrown : {0}", ex);
			}
		}

		[Test]
		public void XmlDeclaration_FixedUpFromApiXmlDocumentation ()
		{
			string tempFile = null;

			try {
				tempFile = LoadToTempFile ("ParameterFixupApiXmlDocs.xml");

				AssertXmlDeclaration ("Collection.class", "ParameterFixupFromDocs.xml", tempFile);
			} finally {
				if (File.Exists (tempFile))
					File.Delete (tempFile);
			}
		}

		[Test]
		public void XmlDeclaration_DoesNotThrowAnExceptionIfDocumentationNotFound ()
		{
			try {
				AssertXmlDeclaration (new [] {"IParameterInterface.class","ParameterAbstractClass.class", "ParameterClass.class", "ParameterClass2.class"}, "ParameterFixup.xml", "SomeNonExistantPath");
			} catch (Exception ex) {
				Assert.Fail ("An unexpected exception was thrown : {0}", ex);
			}
		}

		[Test]
		public void DocletType_ShouldDetectApiXml ()
		{
			string tempFile = null;

			try {
				tempFile = LoadToTempFile ("ParameterFixupApiXmlDocs.xml");

				Assert.AreEqual (JavaDocletType._ApiXml, JavaMethodParameterNameProvider.GetDocletType (tempFile));
			} finally {
				if (File.Exists (tempFile))
					File.Delete (tempFile);
			}
		}

		[Test]
		public void DocletType_ShouldDetectDroidDocs ()
		{
			var androidSdkPath = Environment.GetEnvironmentVariable ("ANDROID_SDK_PATH");
			if (string.IsNullOrEmpty (androidSdkPath)) {
				Assert.Ignore("The `ANDROID_SDK_PATH` environment variable isn't set; " +
						"cannot test importing parameter names from HTML. Skipping...");
				return;
			}

			var droidDocsPath = Path.Combine (androidSdkPath, "docs", "reference");

			if (!Directory.Exists (droidDocsPath))
				Assert.Fail("The Android SDK Documentation path `{0}` was not found.", droidDocsPath);
				
			Assert.AreEqual(JavaDocletType.DroidDoc2, JavaMethodParameterNameProvider.GetDocletType(droidDocsPath));
		}

		[Test]
		public void XmlDeclaration_FixedUpFromParameterDescription ()
		{
			string tempFile = null;

			try {
				tempFile = LoadToTempFile ("ParameterDescription.txt");

				AssertXmlDeclaration (new string [] { "Collection.class" }, "ParameterFixupFromDocs.xml", tempFile);
			}
			finally {
				if (File.Exists (tempFile))
					File.Delete (tempFile);
			}

			try {
				tempFile = LoadToTempFile ("ParameterDescription.txt");

				AssertXmlDeclaration (new string [] { "NestedInterface$DnsSdTxtRecordListener.class" }, "ParameterFixupFromDescriptionText.xml", tempFile);
			} finally {
				if (File.Exists (tempFile))
					File.Delete (tempFile);
			}
		}
	}
}

