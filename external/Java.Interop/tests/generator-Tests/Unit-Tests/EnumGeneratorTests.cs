using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Java.Interop.Tools.Generator.Enumification;
using MonoDroid.Generation;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace generatortests
{
	[TestFixture]
	class EnumGeneratorTests
	{
		protected EnumGenerator generator;
		protected StringBuilder builder;
		protected StringWriter writer;

		[SetUp]
		public void SetUp ()
		{
			builder = new StringBuilder ();
			writer = new StringWriter (builder);

			generator = new EnumGenerator (writer);
		}

		[Test]
		public void WriteBasicEnum ()
		{
			var enu = CreateEnum ();
			enu.Value.FieldsRemoved = true;

			generator.WriteEnumeration (new CodeGenerationOptions (), enu, null);

			Assert.AreEqual (GetExpected (nameof (WriteBasicEnum)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteFlagsEnum ()
		{
			var enu = CreateEnum ();
			enu.Value.BitField = true;
			enu.Value.FieldsRemoved = true;

			generator.WriteEnumeration (new CodeGenerationOptions (), enu, null);

			Assert.AreEqual (GetExpected (nameof (WriteFlagsEnum)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteEnumWithGens ()
		{
			var enu = CreateEnum ();
			var gens = CreateGens ();

			generator.WriteEnumeration (new CodeGenerationOptions (), enu, gens);
			
			Assert.AreEqual (GetExpected (nameof (WriteEnumWithGens)), writer.ToString ().NormalizeLineEndings ());
		}

		protected string GetExpected (string testName)
		{
			var root = Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location);

			return File.ReadAllText (Path.Combine (root, "Unit-Tests", "EnumGeneratorExpectedResults", $"{testName}.txt")).NormalizeLineEndings ();
		}

		KeyValuePair<string, EnumMappings.EnumDescription> CreateEnum ()
		{
			var enu = new EnumMappings.EnumDescription {
				Members = new List<ConstantEntry> {
					new ConstantEntry { EnumMember = "WithExcluded", Value = "1", JavaSignature = "android/app/ActivityManager.RECENT_IGNORE_UNAVAILABLE" },
					new ConstantEntry { EnumMember = "IgnoreUnavailable", Value = "2", JavaSignature = "android/app/ActivityManager.RECENT_WITH_EXCLUDED" }
				},
				BitField = false,
				FieldsRemoved = false
			};

			return new KeyValuePair<string, EnumMappings.EnumDescription> ("Android.App.RecentTaskFlags", enu);
		}

		GenBase[] CreateGens ()
		{
			var klass = new TestClass (string.Empty, "android.app.ActivityManager");

			klass.Fields.Add (new TestField ("int", "RECENT_IGNORE_UNAVAILABLE"));

			return new [] { klass };
		}
	}
}
