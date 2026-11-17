using System;
using System.Xml.Linq;
using Java.Interop.Tools.Generator;
using NUnit.Framework;

namespace Java.Interop.Tools.Generator_Tests
{
	public class NamespaceTransformTests
	{
		[Test]
		public void ParseNamespaceTransform ()
		{
			var doc = XDocument.Parse ("<metadata><ns-replace source='com.example' replacement='Xamarin' /></metadata>");
			var result = NamespaceTransform.TryParse (doc.Root.Element ("ns-replace"), out var nt);

			Assert.IsTrue (result);
			Assert.IsNotNull (nt);
			Assert.AreEqual ("com.example", nt.OldValue);
			Assert.AreEqual ("Xamarin", nt.NewValue);
		}

		[Test]
		public void ParseNamespaceTransform2 ()
		{
			var doc = XDocument.Parse ("<metadata><ns-replace source='com.example' replacement='' /></metadata>");
			var result = NamespaceTransform.TryParse (doc.Root.Element ("ns-replace"), out var nt);

			Assert.IsTrue (result);
			Assert.IsNotNull (nt);
			Assert.AreEqual ("com.example", nt.OldValue);
			Assert.AreEqual ("", nt.NewValue);
		}

		[Test]
		public void ParseInvalidNamespaceTransform ()
		{
			// Logs: warning BG8A07: Invalid namespace transform '<ns-replace source="com.example" />'
			var doc = XDocument.Parse ("<metadata><ns-replace source='com.example' /></metadata>");
			var result = NamespaceTransform.TryParse (doc.Root.Element ("ns-replace"), out var nt);

			Assert.IsFalse (result);
			Assert.IsNull (nt);
		}

		[Test]
		public void ParseInvalidNamespaceTransform2 ()
		{
			// Logs: warning BG8A07: Invalid namespace transform '<ns-replace source="" replacement="Xamarin" />'
			var doc = XDocument.Parse ("<metadata><ns-replace source='' replacement='Xamarin' /></metadata>");
			var result = NamespaceTransform.TryParse (doc.Root.Element ("ns-replace"), out var nt);

			Assert.IsFalse (result);
			Assert.IsNull (nt);
		}

		[Test]
		public void GetTransformedNamespace ()
		{
			// Normal and case-insensitive
			AssertTransformedNamespace ("Androidx.Core", "AndroidX.Core", new NamespaceTransform ("Androidx", "AndroidX"));
			AssertTransformedNamespace ("Androidx.Core", "AndroidX.Core", new NamespaceTransform ("androidx", "AndroidX"));

			// Replace 1 level with 2
			AssertTransformedNamespace ("Androidx.Core", "Xamarin.AndroidX.Core", new NamespaceTransform ("androidx", "Xamarin.AndroidX"));

			// Replace 2 levels with 1
			AssertTransformedNamespace ("Google.Androidx.Core", "AndroidX.Core", new NamespaceTransform ("Google.Androidx", "AndroidX"));

			// Replace 2 levels with 2
			AssertTransformedNamespace ("Google.Androidx.Core", "Xamarin.AndroidX.Core", new NamespaceTransform ("Google.Androidx", "Xamarin.AndroidX"));

			// Removing a match
			AssertTransformedNamespace ("Androidx.Core.Test", "Androidx.Test", new NamespaceTransform ("core", ""));
			AssertTransformedNamespace ("Androidx.Core.Test", "Androidx.Core", new NamespaceTransform ("test", ""));
			AssertTransformedNamespace ("Androidx.Core.Test", "Core.Test", new NamespaceTransform ("androidx", ""));

			// Multiple matches
			AssertTransformedNamespace ("Androidx.Androidx.Core", "AndroidX.AndroidX.Core", new NamespaceTransform ("androidx", "AndroidX"));
			AssertTransformedNamespace ("google.androidx.Core", "Xamarin.AndroidX.Core", new NamespaceTransform ("androidx", "AndroidX"), new NamespaceTransform ("google", "Xamarin"));

			// Starts with and ends with
			AssertTransformedNamespace ("example", "Transformed", new NamespaceTransform (".example.", "Transformed"));
			AssertTransformedNamespace ("Androidx.Core", "Transformed", new NamespaceTransform (".Androidx.Core.", "Transformed"));
			AssertTransformedNamespace ("Androidx.Core", "Androidx.Core", new NamespaceTransform (".Core.", "Transformed"));
			AssertTransformedNamespace ("Androidx.Core", "Androidx.Core", new NamespaceTransform (".AndroidX.", "Transformed"));

			// Starts with
			AssertTransformedNamespace ("Androidx.Androidx.Core", "AndroidX2.Androidx.Core", new NamespaceTransform ("AndroidX.", "AndroidX2"));
			AssertTransformedNamespace ("Androidx.Androidx.Core", "Androidx.Core", new NamespaceTransform ("AndroidX.", ""));

			// Ends with
			AssertTransformedNamespace ("Androidx.Core.Core", "Androidx.Core.Core2", new NamespaceTransform (".core", "Core2"));
			AssertTransformedNamespace ("Androidx.Core.Core", "Androidx.Core", new NamespaceTransform (".core", ""));

			// Only matches full level
			AssertTransformedNamespace ("AndroidX.Test.Tests", "AndroidX.NewTest.Tests", new NamespaceTransform ("Test", "NewTest"));
		}

		void AssertTransformedNamespace (string value, string expected, params NamespaceTransform [] transforms)
		{
			foreach (var nt in transforms)
				value = nt.Apply (value);

			Assert.AreEqual (expected, value);
		}
	}
}
