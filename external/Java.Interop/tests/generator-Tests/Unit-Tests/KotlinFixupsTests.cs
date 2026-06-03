using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using Java.Interop.Tools.Generator;
using Java.Interop.Tools.Generator.Transformation;
using MonoDroid.Generation;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class KotlinFixupsTests
	{
		[Test]
		public void CreateMethod_EnsureKotlinImplFix ()
		{
			var xml = XDocument.Parse ("<package name=\"com.example.test\" jni-name=\"com/example/test\"><class name=\"test\"><method name=\"add-impl\" final=\"false\" /></class></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), new CodeGenerationOptions ());

			KotlinFixups.Fixup (new [] { (GenBase)klass }.ToList ());

			Assert.AreEqual ("Add", klass.Methods [0].Name);
			Assert.IsTrue (klass.Methods [0].IsFinal);
			Assert.IsFalse (klass.Methods [0].IsVirtual);
		}

		[Test]
		public void CreateMethod_EnsureKotlinHashcodeFix ()
		{
			var xml = XDocument.Parse ("<package name=\"com.example.test\" jni-name=\"com/example/test\"><class name=\"test\"><method name=\"add-h-_1V8i\" final=\"false\" /></class></package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), new CodeGenerationOptions ());

			KotlinFixups.Fixup (new [] { (GenBase) klass }.ToList ());

			Assert.AreEqual ("Add", klass.Methods [0].Name);
			Assert.IsTrue (klass.Methods [0].IsFinal);
			Assert.IsFalse (klass.Methods [0].IsVirtual);
		}

		[Test, NonParallelizable]
		public void CollidingHashSiblings_AreDeduplicated ()
		{
			// Two Kotlin hash-mangled siblings that erase to the same C# signature
			// (one `long` parameter). After the rename to `Add` both would collide,
			// so we keep only the first.
			var xml = XDocument.Parse (@"<package name='com.example.test' jni-name='com/example/test'>
				<class name='test'>
					<method name='add-AAAAAAA' final='false'>
						<parameter name='p0' type='long' jni-type='J' />
					</method>
					<method name='add-BBBBBBB' final='false'>
						<parameter name='p0' type='long' jni-type='J' />
					</method>
				</class>
			</package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), new CodeGenerationOptions ());

			using var warnings = CaptureWarnings ();
			KotlinFixups.Fixup (new [] { (GenBase) klass }.ToList ());

			Assert.AreEqual (1, klass.Methods.Count, "Duplicate hash-mangled sibling should have been removed.");
			Assert.AreEqual ("Add", klass.Methods [0].Name);
			Assert.AreEqual ("add-AAAAAAA", klass.Methods [0].JavaName, "The first hash-mangled sibling in source order should survive.");
			Assert.IsTrue (warnings.Messages.Any (m => m.Contains ("BG8C02")), "Expected BG8C02 warning, got: " + string.Join (Environment.NewLine, warnings.Messages));
		}

		[Test]
		public void NonCollidingHashSiblings_AreBothKept ()
		{
			// Two siblings with distinct parameter lists: both should rename to `Add`
			// and survive as overloads.
			var xml = XDocument.Parse (@"<package name='com.example.test' jni-name='com/example/test'>
				<class name='test'>
					<method name='add-AAAAAAA' final='false'>
						<parameter name='p0' type='long' jni-type='J' />
					</method>
					<method name='add-BBBBBBB' final='false'>
						<parameter name='p0' type='float' jni-type='F' />
					</method>
				</class>
			</package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), new CodeGenerationOptions ());

			KotlinFixups.Fixup (new [] { (GenBase) klass }.ToList ());

			Assert.AreEqual (2, klass.Methods.Count);
			Assert.IsTrue (klass.Methods.All (m => m.Name == "Add"));
		}

		[Test]
		public void MixedCollidingAndUniqueHashSiblings ()
		{
			// Three siblings of the same source-name: the long+long pair collide,
			// the float arg is unique. Expect 2 methods to survive.
			var xml = XDocument.Parse (@"<package name='com.example.test' jni-name='com/example/test'>
				<class name='test'>
					<method name='add-AAAAAAA' final='false'>
						<parameter name='p0' type='long' jni-type='J' />
					</method>
					<method name='add-BBBBBBB' final='false'>
						<parameter name='p0' type='long' jni-type='J' />
					</method>
					<method name='add-CCCCCCC' final='false'>
						<parameter name='p0' type='float' jni-type='F' />
					</method>
				</class>
			</package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), new CodeGenerationOptions ());

			KotlinFixups.Fixup (new [] { (GenBase) klass }.ToList ());

			Assert.AreEqual (2, klass.Methods.Count);
			Assert.IsTrue (klass.Methods.All (m => m.Name == "Add"));
			CollectionAssert.AreEquivalent (new [] { "long", "float" }, klass.Methods.Select (m => m.Parameters [0].RawNativeType).ToArray ());
		}

		[Test, NonParallelizable]
		public void MangledMethod_CollidesWithNonMangledOverload ()
		{
			// A pre-existing non-mangled overload `add(long)` plus a mangled
			// `add-AAAAAAA(long)` that also reduces to `add(long)` after rename.
			// The mangled one is the duplicate -- drop it, keep the non-mangled.
			var xml = XDocument.Parse (@"<package name='com.example.test' jni-name='com/example/test'>
				<class name='test'>
					<method name='add' final='false'>
						<parameter name='p0' type='long' jni-type='J' />
					</method>
					<method name='add-AAAAAAA' final='false'>
						<parameter name='p0' type='long' jni-type='J' />
					</method>
				</class>
			</package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), new CodeGenerationOptions ());

			using var warnings = CaptureWarnings ();
			KotlinFixups.Fixup (new [] { (GenBase) klass }.ToList ());

			Assert.AreEqual (1, klass.Methods.Count, "Mangled duplicate should have been removed, leaving the pre-existing non-mangled method.");
			Assert.AreEqual ("add", klass.Methods [0].JavaName, "The kept method should be the non-mangled one.");
			Assert.IsTrue (warnings.Messages.Any (m => m.Contains ("BG8C02")), "Expected BG8C02 warning.");
		}

		[Test, NonParallelizable]
		public void MangledMethod_CollidesWithNonMangledOverload_ReversedOrder ()
		{
			// Same as above but with the mangled method declared FIRST. The
			// non-mangled real Kotlin API must still win regardless of order.
			var xml = XDocument.Parse (@"<package name='com.example.test' jni-name='com/example/test'>
				<class name='test'>
					<method name='add-AAAAAAA' final='false'>
						<parameter name='p0' type='long' jni-type='J' />
					</method>
					<method name='add' final='false'>
						<parameter name='p0' type='long' jni-type='J' />
					</method>
				</class>
			</package>");
			var klass = XmlApiImporter.CreateClass (xml.Root, xml.Root.Element ("class"), new CodeGenerationOptions ());

			using var warnings = CaptureWarnings ();
			KotlinFixups.Fixup (new [] { (GenBase) klass }.ToList ());

			Assert.AreEqual (1, klass.Methods.Count, "Mangled duplicate should have been removed regardless of source order.");
			Assert.AreEqual ("add", klass.Methods [0].JavaName, "The non-mangled method should survive regardless of order.");
			Assert.IsTrue (warnings.Messages.Any (m => m.Contains ("BG8C02")), "Expected BG8C02 warning.");
		}

		static WarningCapture CaptureWarnings () => new WarningCapture ();

		sealed class WarningCapture : IDisposable
		{
			readonly Action<TraceLevel, string> previous;
			public List<string> Messages { get; } = new List<string> ();

			public WarningCapture ()
			{
				previous = Report.OutputDelegate;
				Report.OutputDelegate = (level, msg) => Messages.Add (msg);
			}

			public void Dispose ()
			{
				Report.OutputDelegate = previous;
			}
		}
	}
}
