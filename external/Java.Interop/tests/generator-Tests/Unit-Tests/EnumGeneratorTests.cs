using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Java.Interop.Tools.Generator.Enumification;
using MonoDroid.Generation;
using NUnit.Framework;
using NUnit.Framework.Internal;
using Xamarin.Android.Binder;

namespace generatortests
{
	[TestFixture]
	class EnumGeneratorTests : CodeGeneratorTestBase
	{
		protected new EnumGenerator generator;

		protected override CodeGenerationTarget Target => CodeGenerationTarget.XAJavaInterop1;

		[SetUp]
		public new void SetUp ()
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

		[Test]
		public void ObsoletedOSPlatformAttributeSupport ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='android.app' jni-name='android/app'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='ActivityManager' static='false' visibility='public' jni-signature='Landroid/app/ActivityManager;'>
			      <field deprecated='deprecated' final='true' name='RECENT_IGNORE_UNAVAILABLE' jni-signature='Ljava/lang/String;' static='true' transient='false' type='java.lang.String' type-generic-aware='java.lang.String' value='&quot;android.permission.BIND_CHOOSER_TARGET_SERVICE&quot;' visibility='public' volatile='false' deprecated-since='31' api-since='30' />
			    </class>
			  </package>
			</api>";

			options.UseObsoletedOSPlatformAttributes = true;

			var enu = CreateEnum ();
			var gens = ParseApiDefinition (xml);

			generator.WriteEnumeration (options, enu, gens.ToArray ());

			// Ensure [ObsoletedOSPlatform] and [SupportedOSPlatform] are written
			Assert.True (writer.ToString ().NormalizeLineEndings ().Contains ("[global::System.Runtime.Versioning.SupportedOSPlatformAttribute(\"android30.0\")][global::System.Runtime.Versioning.ObsoletedOSPlatform(\"android31.0\")]WithExcluded=1"), writer.ToString ());
		}

		[Test]
		public void ObsoleteAttributeSupport ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='android.app' jni-name='android/app'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='ActivityManager' static='false' visibility='public' jni-signature='Landroid/app/ActivityManager;'>
			      <field deprecated='deprecated' final='true' name='RECENT_IGNORE_UNAVAILABLE' jni-signature='Ljava/lang/String;' static='true' transient='false' type='java.lang.String' type-generic-aware='java.lang.String' value='&quot;android.permission.BIND_CHOOSER_TARGET_SERVICE&quot;' visibility='public' volatile='false' deprecated-since='31' api-since='30' />
			    </class>
			  </package>
			</api>";

			var enu = CreateEnum ();
			var gens = ParseApiDefinition (xml);

			generator.WriteEnumeration (options, enu, gens.ToArray ());

			// Ensure [Obsolete] is written
			Assert.True (writer.ToString ().NormalizeLineEndings ().Contains ("[global::System.Obsolete(@\"deprecated\")]WithExcluded=1"), writer.ToString ());
		}

		[Test]
		public void ObsoleteFieldButNotEnumAttributeSupport ()
		{
			var xml = @"<api>
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='android.app' jni-name='android/app'>
			    <class abstract='false' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='ActivityManager' static='false' visibility='public' jni-signature='Landroid/app/ActivityManager;'>
			      <field deprecated='This constant will be removed in the future version. Use Android.App.RecentTaskFlags enum directly instead of this field.' final='true' name='RECENT_IGNORE_UNAVAILABLE' jni-signature='Ljava/lang/String;' static='true' transient='false' type='java.lang.String' type-generic-aware='java.lang.String' value='&quot;android.permission.BIND_CHOOSER_TARGET_SERVICE&quot;' visibility='public' volatile='false' deprecated-since='31' api-since='30' />
			    </class>
			  </package>
			</api>";

			var enu = CreateEnum ();
			var gens = ParseApiDefinition (xml);

			generator.WriteEnumeration (options, enu, gens.ToArray ());

			// [Obsolete] should not be written because the value isn't deprecated, just the _field_ is deprecated because we want people to use the enum instead
			Assert.False (writer.ToString ().NormalizeLineEndings ().Contains ("[global::System.Obsolete(@\"deprecated\")]WithExcluded=1"), writer.ToString ());
		}

		protected new string GetExpected (string testName)
		{
			var root = Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location);

			return File.ReadAllText (Path.Combine (root, "Unit-Tests", "EnumGeneratorExpectedResults", $"{testName}.txt")).NormalizeLineEndings ();
		}

		KeyValuePair<string, EnumMappings.EnumDescription> CreateEnum ()
		{
			var enu = new EnumMappings.EnumDescription {
				Members = new List<ConstantEntry> {
					new ConstantEntry { EnumMember = "WithExcluded", Value = "1", JavaSignature = "android/app/ActivityManager.RECENT_IGNORE_UNAVAILABLE", ApiLevel = 30 },
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
