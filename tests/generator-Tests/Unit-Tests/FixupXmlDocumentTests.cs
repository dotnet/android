using System;
using System.Linq;
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
		public void AddNode_PreservesJniOverrides ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument ("<add-node path=\"/api/package[@name='android']\"><method name='test' jni-signature='(Ljava/lang/Object;)V'><parameter name='value' type='java.lang.String' jni-type='Ljava/lang/Object;' /></method></add-node>");

			api.ApplyFixupFile (fixup);

			var method = api.ApiDocument.Root.Element ("package").Element ("method");
			Assert.AreEqual ("(Ljava/lang/Object;)V", method.Attribute ("managed-jni-signature").Value);
			Assert.AreEqual ("Ljava/lang/Object;", method.Element ("parameter").Attribute ("managed-jni-type").Value);
		}

		[Test]
		public void AddNode_GenericJniTypeUsesErasedMethodSignature ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument ("<add-node path=\"/api/package[@name='android']\"><method name='onNext' jni-signature='(Ljava/lang/Object;)V' return='void'><parameter name='value' type='T' jni-type='TT;' /></method></add-node>");

			api.ApplyFixupFile (fixup);

			var method = api.ApiDocument.Root.Element ("package").Element ("method");
			Assert.Multiple (() => {
				Assert.AreEqual ("(Ljava/lang/Object;)V", method.Attribute ("managed-jni-signature").Value);
				Assert.IsNull (method.Element ("parameter").Attribute ("managed-jni-type"));
			});
		}

		[Test]
		public void AddNode_DoesNotPreserveConstructorJniType ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument ("<add-node path=\"/api/package[@name='android']\"><constructor name='test'><parameter name='value' type='java.lang.String' jni-type='TT;' /></constructor></add-node>");

			api.ApplyFixupFile (fixup);

			var constructor = api.ApiDocument.Root.Element ("package").Element ("constructor");
			Assert.IsNull (constructor.Element ("parameter").Attribute ("managed-jni-type"));
		}

		[Test]
		public void AddNode_ParameterTransformInvalidatesJniOverrides ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument (
				"<add-node path=\"/api/package[@name='android']\"><method name='test' jni-signature='(I)V' return='void'><parameter name='value' type='int' jni-type='I' /></method></add-node>" +
				"<attr path=\"/api/package[@name='android']/method[@name='test']/parameter[@name='value']\" name='type'>java.lang.String</attr>");

			api.ApplyFixupFile (fixup);

			var method = api.ApiDocument.Root.Element ("package").Element ("method");
			Assert.Multiple (() => {
				Assert.IsNull (method.Attribute ("managed-jni-signature"));
				Assert.IsNull (method.Element ("parameter").Attribute ("managed-jni-type"));
			});
		}

		[Test]
		public void AddNode_ReturnTransformInvalidatesJniOverride ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument (
				"<add-node path=\"/api/package[@name='android']\"><method name='test' jni-signature='()I' return='int' /></add-node>" +
				"<attr path=\"/api/package[@name='android']/method[@name='test']\" name='return'>java.lang.String</attr>");

			api.ApplyFixupFile (fixup);

			var method = api.ApiDocument.Root.Element ("package").Element ("method");
			Assert.IsNull (method.Attribute ("managed-jni-signature"));
		}

		[Test]
		public void AddNode_EnumTransformsPreserveJniOverrides ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument (
				"<add-node path=\"/api/package[@name='android']\"><method name='test' jni-signature='(Lexample/Listener;I)I' return='int'><parameter name='listener' type='example.Listener' jni-type='Lexample/Listener;' /><parameter name='value' type='int' jni-type='I' /></method></add-node>" +
				"<attr path=\"/api/package[@name='android']/method[@name='test']/parameter[@name='value']\" name='enumType'>Example.MyEnum</attr>" +
				"<attr path=\"/api/package[@name='android']/method[@name='test']\" name='enumReturn'>Example.MyEnum</attr>");

			api.ApplyFixupFile (fixup);

			var method = api.ApiDocument.Root.Element ("package").Element ("method");
			Assert.Multiple (() => {
				Assert.AreEqual ("(Lexample/Listener;I)I", method.Attribute ("managed-jni-signature").Value);
				Assert.AreEqual ("Lexample/Listener;", method.Elements ("parameter").First ().Attribute ("managed-jni-type").Value);
				Assert.AreEqual ("I", method.Elements ("parameter").Last ().Attribute ("managed-jni-type").Value);
			});
		}

		[Test]
		public void AddNode_ParameterInvalidatesJniSignatureOverride ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument (
				"<add-node path=\"/api/package[@name='android']\"><method name='test' jni-signature='(Ljava/lang/String;)V' return='void'><parameter name='value' type='java.lang.String' jni-type='Ljava/lang/String;' /></method></add-node>" +
				"<add-node path=\"/api/package[@name='android']/method[@name='test']\"><parameter name='flags' type='int' /></add-node>");

			api.ApplyFixupFile (fixup);

			var method = api.ApiDocument.Root.Element ("package").Element ("method");
			Assert.IsNull (method.Attribute ("managed-jni-signature"));
			Assert.AreEqual ("Ljava/lang/String;", method.Elements ("parameter").First ().Attribute ("managed-jni-type").Value);
		}

		[Test]
		public void AddNode_DirectParameterPreservesJniTypeOverride ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument (
				"<add-node path=\"/api/package[@name='android']\"><method name='test' jni-signature='()V' return='void' /></add-node>" +
				"<add-node path=\"/api/package[@name='android']/method[@name='test']\"><parameter name='value' type='java.lang.String' jni-type='Ljava/lang/Object;' /></add-node>");

			api.ApplyFixupFile (fixup);

			var method = api.ApiDocument.Root.Element ("package").Element ("method");
			Assert.Multiple (() => {
				Assert.IsNull (method.Attribute ("managed-jni-signature"));
				Assert.AreEqual ("Ljava/lang/Object;", method.Element ("parameter").Attribute ("managed-jni-type").Value);
			});
		}

		[Test]
		public void RemoveNode_ParameterInvalidatesJniSignatureOverride ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument (
				"<add-node path=\"/api/package[@name='android']\"><method name='test' jni-signature='(Ljava/lang/String;I)V' return='void'><parameter name='value' type='java.lang.String' jni-type='Ljava/lang/String;' /><parameter name='flags' type='int' jni-type='I' /></method></add-node>" +
				"<remove-node path=\"/api/package[@name='android']/method[@name='test']/parameter[@name='flags']\" />");

			api.ApplyFixupFile (fixup);

			var method = api.ApiDocument.Root.Element ("package").Element ("method");
			Assert.IsNull (method.Attribute ("managed-jni-signature"));
			Assert.AreEqual ("Ljava/lang/String;", method.Element ("parameter").Attribute ("managed-jni-type").Value);
		}

		[Test]
		public void AddNode_DoesNotPreserveEmptyJniOverrides ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument ("<add-node path=\"/api/package[@name='android']\"><method name='test' jni-signature='' return='void'><parameter name='value' type='int' jni-type='' /></method></add-node>");

			api.ApplyFixupFile (fixup);

			var method = api.ApiDocument.Root.Element ("package").Element ("method");
			Assert.Multiple (() => {
				Assert.IsNull (method.Attribute ("managed-jni-signature"));
				Assert.IsNull (method.Element ("parameter").Attribute ("managed-jni-type"));
			});
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
		public void ChangeNode_ParameterInvalidatesJniSignatureOverride ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument (
				"<add-node path=\"/api/package[@name='android']\"><method name='test' jni-signature='(I)V' return='void'><parameter name='value' type='int' jni-type='I' /></method></add-node>" +
				"<change-node path=\"/api/package[@name='android']/method[@name='test']/parameter[@name='value']\">field</change-node>");

			api.ApplyFixupFile (fixup);

			var method = api.ApiDocument.Root.Element ("package").Element ("method");
			Assert.IsNull (method.Attribute ("managed-jni-signature"));
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
		public void MoveNode_ParameterInvalidatesJniSignatureOverrides ()
		{
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument (
				"<add-node path=\"/api/package[@name='android']\"><method name='source' jni-signature='(I)V' return='void'><parameter name='value' type='int' jni-type='I' /></method><method name='destination' jni-signature='()V' return='void' /></add-node>" +
				"<move-node path=\"/api/package[@name='android']/method[@name='source']/parameter\">/api/package[@name='android']/method[@name='destination']</move-node>");

			api.ApplyFixupFile (fixup);

			var methods = api.ApiDocument.Root.Element ("package").Elements ("method").ToArray ();
			Assert.Multiple (() => {
				Assert.IsNull (methods [0].Attribute ("managed-jni-signature"));
				Assert.IsNull (methods [1].Attribute ("managed-jni-signature"));
				Assert.AreEqual ("I", methods [1].Element ("parameter").Attribute ("managed-jni-type").Value);
			});
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

			Assert.AreEqual ("<api><package jni-name='android' /><package name='java' jni-name='java' /></api>", api.ApiDocument.ToString (SaveOptions.DisableFormatting).Replace ('\"', '\''));
		}

		[Test]
		public void RemoveNotFoundAttribute ()
		{
			// Attribute 'foo' doesn't exist on node
			var api = GetXmlApiDocument ();
			var fixup = GetFixupXmlDocument ("<remove-attr path=\"/api/package[@name='android']\" name='foo' />");

			api.ApplyFixupFile (fixup);

			Assert.AreEqual ("<api><package name='android' jni-name='android' /><package name='java' jni-name='java' /></api>", api.ApiDocument.ToString (SaveOptions.DisableFormatting).Replace ('\"', '\''));
		}

		[Test]
		public void ParseNamespaceTransforms ()
		{
			var fixup = GetFixupXmlDocument ("<ns-replace source='androidx' replacement='AndroidX' /><ns-replace source='com.google' replacement='Xamarin' />");
			var transforms = fixup.GetNamespaceTransforms ();

			Assert.AreEqual (2, transforms.Count);
			Assert.AreEqual ("androidx", transforms [0].OldValue);
			Assert.AreEqual ("AndroidX", transforms [0].NewValue);
			Assert.AreEqual ("com.google", transforms [1].OldValue);
			Assert.AreEqual ("Xamarin", transforms [1].NewValue);
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
