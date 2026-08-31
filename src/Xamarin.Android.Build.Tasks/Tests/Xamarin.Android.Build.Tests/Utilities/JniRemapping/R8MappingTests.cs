using System;
using System.IO;
using NUnit.Framework;
using Xamarin.Android.Tasks.JniRemapping;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class R8MappingTests : BaseTest
	{
		[Test]
		public void ParsesSimpleClassAndFieldMapping ()
		{
			var mapping = R8Mapping.Parse (new StringReader (
				"acme.orig.MyView -> a.b.C:\n" +
				"    int someField -> x\n"));

			Assert.IsTrue (mapping.TryGetRenamedClass ("acme/orig/MyView", out string renamedClass));
			Assert.AreEqual ("a/b/C", renamedClass);

			Assert.IsTrue (mapping.TryGetRenamedField ("acme/orig/MyView", "someField", out string renamedField));
			Assert.AreEqual ("x", renamedField);
		}

		[Test]
		public void LooksUpOriginalClassAndMethodNames ()
		{
			var mapping = R8Mapping.Parse (new StringReader (
				"acme.orig.MyView -> a.b.C:\n" +
				"    void first(int) -> x\n" +
				"    void first(java.lang.String) -> x\n" +
				"    void second() -> x\n"));

			Assert.IsTrue (mapping.TryGetOriginalClass ("a/b/C", out string originalClass));
			Assert.AreEqual ("acme/orig/MyView", originalClass);
			CollectionAssert.AreEquivalent (new [] { "first", "second" }, mapping.GetOriginalMethodNames (originalClass, "x"));
			Assert.IsTrue (mapping.TryGetOriginalMethodName (originalClass, "x", new [] { "int" }, out string first));
			Assert.AreEqual ("first", first);
			Assert.IsTrue (mapping.TryGetOriginalMethodName (originalClass, "x", Array.Empty<string> (), out string second));
			Assert.AreEqual ("second", second);
			Assert.IsFalse (mapping.TryGetOriginalClass ("a/b/Missing", out _));
		}

		[Test]
		public void ParsesMethodMappingWithParameters ()
		{
			var mapping = R8Mapping.Parse (new StringReader (
				"""
				acme.orig.MyView -> a.b.C:
				    void onClick(android.view.View) -> a
				    void onClick(android.view.View,int) -> b

				"""));

			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "onClick", new [] { "android.view.View" }, out string renamed1));
			Assert.AreEqual ("a", renamed1);

			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "onClick", new [] { "android.view.View", "int" }, out string renamed2));
			Assert.AreEqual ("b", renamed2);
		}

		[Test]
		public void ParsesMethodMappingWithLeadingAndTrailingLineRanges ()
		{
			// "startLine:endLine:returnType name(params):origStartLine:origEndLine -> obfuscated"
			var mapping = R8Mapping.Parse (new StringReader (
				"acme.orig.MyView -> a.b.C:\n" +
				"    4:10:void onCreate(android.os.Bundle):23:29 -> a\n"));

			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "onCreate", new [] { "android.os.Bundle" }, out string renamed));
			Assert.AreEqual ("a", renamed);
		}

		[Test]
		public void ParsesMethodMappingWithASingleTrailingLineNumber ()
		{
			// R8 sometimes collapses the trailing original range to a single line number when
			// start and end coincide: "startLine:endLine:returnType name(params):originalLine -> obfuscated".
			var mapping = R8Mapping.Parse (new StringReader (
				"example.Foo -> a.b.C:\n" +
				"acme.orig.MyView -> a.b.D:\n" +
				"    4:4:void run(example.Foo):2 -> a\n"));

			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "run", new [] { "example.Foo" }, out string renamed));
			Assert.AreEqual ("a", renamed);
		}

		[Test]
		public void ParsesMethodMappingWithOnlyATrailingLineRange ()
		{
			// No leading "startLine:endLine:" prefix, only the trailing ":originalStart:originalEnd".
			var mapping = R8Mapping.Parse (new StringReader (
				"acme.orig.MyView -> a.b.C:\n" +
				"    void onPause():23:29 -> a\n"));

			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "onPause", Array.Empty<string> (), out string renamed));
			Assert.AreEqual ("a", renamed);
		}

		[Test]
		public void ParsesNoArgMethodMapping ()
		{
			var mapping = R8Mapping.Parse (new StringReader (
				"acme.orig.MyView -> a.b.C:\n" +
				"    void onStart() -> a\n"));

			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "onStart", Array.Empty<string> (), out string renamed));
			Assert.AreEqual ("a", renamed);
		}

		[Test]
		public void KeepsDollarInNestedClassNames ()
		{
			var mapping = R8Mapping.Parse (new StringReader (
				"acme.orig.MyView$Inner -> a.b.C$D:\n"));

			Assert.IsTrue (mapping.TryGetRenamedClass ("acme/orig/MyView$Inner", out string renamed));
			Assert.AreEqual ("a/b/C$D", renamed);
		}

		[Test]
		public void TranslatesConstructorNameForLookup ()
		{
			var mapping = R8Mapping.Parse (new StringReader (
				"acme.orig.MyView -> a.b.C:\n" +
				"    void <init>(int) -> <init>\n"));

			// JVM never renames <init>, but the lookup key must still translate ".ctor" -> "<init>".
			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", R8Mapping.JniMemberNameToMappingName (".ctor"), new [] { "int" }, out string renamed));
			Assert.AreEqual ("<init>", renamed);
		}

		[Test]
		public void NameOnlyLookupSucceedsWhenUnambiguous ()
		{
			var mapping = R8Mapping.Parse (new StringReader (
				"acme.orig.MyView -> a.b.C:\n" +
				"    void onStart(int) -> a\n" +
				"    void onStart(int,int) -> a\n")); // Both overloads map to the same name.

			Assert.IsTrue (mapping.TryGetRenamedMethodByNameOnly ("acme/orig/MyView", "onStart", out string renamed));
			Assert.AreEqual ("a", renamed);
		}

		[Test]
		public void NameOnlyLookupFailsWhenAmbiguous ()
		{
			var mapping = R8Mapping.Parse (new StringReader (
				"acme.orig.MyView -> a.b.C:\n" +
				"    void onStart(int) -> a\n" +
				"    void onStart(int,int) -> b\n")); // Different renames - ambiguous without a descriptor.

			Assert.IsFalse (mapping.TryGetRenamedMethodByNameOnly ("acme/orig/MyView", "onStart", out _));
		}

		[Test]
		public void UnknownClassOrMemberReturnsFalse ()
		{
			var mapping = R8Mapping.Parse (new StringReader ("acme.orig.MyView -> a.b.C:\n"));

			Assert.IsFalse (mapping.TryGetRenamedClass ("acme/orig/Other", out _));
			Assert.IsFalse (mapping.TryGetRenamedField ("acme/orig/MyView", "missing", out _));
			Assert.IsFalse (mapping.TryGetRenamedMethod ("acme/orig/MyView", "missing", Array.Empty<string> (), out _));
		}

		[Test]
		public void IgnoresCommentsAndBlankLines ()
		{
			var mapping = R8Mapping.Parse (new StringReader (
				"# a comment\n" +
				"\n" +
				"acme.orig.MyView -> a.b.C:\n" +
				"\n" +
				"    int someField -> x\n"));

			Assert.IsTrue (mapping.TryGetRenamedClass ("acme/orig/MyView", out string renamed));
			Assert.AreEqual ("a/b/C", renamed);
		}

		[Test]
		public void IgnoresIndentedMetadataComments ()
		{
			// R8 emits indented "# {...}" comments under member lines to carry extra metadata
			// (e.g. inlining/source-position info); these must not be mistaken for member lines.
			var mapping = R8Mapping.Parse (new StringReader (
				"acme.orig.MyView -> a.b.C:\n" +
				"    void onCreate(android.os.Bundle) -> a\n" +
				"    # {'id':'com.android.tools.r8.synthesized'}\n" +
				"    int someField -> x\n"));

			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "onCreate", new [] { "android.os.Bundle" }, out string renamedMethod));
			Assert.AreEqual ("a", renamedMethod);

			Assert.IsTrue (mapping.TryGetRenamedField ("acme/orig/MyView", "someField", out string renamedField));
			Assert.AreEqual ("x", renamedField);
		}

		[Test]
		public void IgnoresQualifiedInlineCallFrameMappings ()
		{
			var mapping = R8Mapping.Parse (new StringReader (
				"androidx.collection.LongSparseArray -> a.b.C:\n" +
				"    299:299:void androidx.collection.LongSparseArrayKt.commonGc(androidx.collection.LongSparseArray) -> keyAt\n" +
				"    299:299:long keyAt(int):183 -> keyAt\n" +
				"    307:307:void androidx.collection.LongSparseArrayKt.commonGc(androidx.collection.LongSparseArray) -> indexOfKey\n" +
				"    307:307:int indexOfKey(long):209 -> indexOfKey\n"));

			Assert.IsTrue (mapping.TryGetRenamedMethod ("androidx/collection/LongSparseArray", "keyAt", new [] { "int" }, out string keyAt));
			Assert.AreEqual ("keyAt", keyAt);
			Assert.IsTrue (mapping.TryGetRenamedMethod ("androidx/collection/LongSparseArray", "indexOfKey", new [] { "long" }, out string indexOfKey));
			Assert.AreEqual ("indexOfKey", indexOfKey);
			Assert.IsFalse (mapping.TryGetRenamedMethod ("androidx/collection/LongSparseArray", "androidx.collection.LongSparseArrayKt.commonGc", new [] { "androidx.collection.LongSparseArray" }, out _));
		}

		[Test]
		public void TreatsMethodInlinedIntoMultipleDestinationsAsAmbiguous ()
		{
			var mapping = R8Mapping.Parse (new StringReader (
				"androidx.collection.SimpleArrayMap -> a.b.C:\n" +
				"    299:299:java.lang.Object getOrDefaultInternal(java.lang.Object,java.lang.Object) -> get\n" +
				"    299:299:java.lang.Object get(java.lang.Object):278 -> get\n" +
				"    299:299:java.lang.Object getOrDefaultInternal(java.lang.Object,java.lang.Object) -> getOrDefault\n" +
				"    299:299:java.lang.Object getOrDefault(java.lang.Object,java.lang.Object):294 -> getOrDefault\n"));

			Assert.IsFalse (mapping.TryGetRenamedMethod ("androidx/collection/SimpleArrayMap", "getOrDefaultInternal", new [] { "java.lang.Object", "java.lang.Object" }, out _));
			Assert.IsTrue (mapping.TryGetRenamedMethod ("androidx/collection/SimpleArrayMap", "get", new [] { "java.lang.Object" }, out string get));
			Assert.AreEqual ("get", get);
			Assert.IsTrue (mapping.TryGetRenamedMethod ("androidx/collection/SimpleArrayMap", "getOrDefault", new [] { "java.lang.Object", "java.lang.Object" }, out string getOrDefault));
			Assert.AreEqual ("getOrDefault", getOrDefault);
		}

		[Test]
		public void ThrowsOnMemberLineBeforeAnyClassLine ()
		{
			Assert.Throws<FormatException> (() => R8Mapping.Parse (new StringReader ("    int someField -> x\n")));
		}

		[Test]
		public void ReportsNamesThatDifferBetweenSeedAndFinalMappings ()
		{
			R8Mapping seed = R8Mapping.Parse (new StringReader ("""
				acme.orig.MyView -> a.b.C:
				    int count -> a
				    void onClick(android.view.View) -> b
				    void removed() -> c

				"""));
			R8Mapping final = R8Mapping.Parse (new StringReader ("""
				acme.orig.MyView -> x.y.Z:
				    int count -> d
				    void onClick(android.view.View) -> e
				acme.final.Only -> q.r.S:

				"""));

			CollectionAssert.AreEqual (new [] {
				"class 'acme/orig/MyView': seed name 'a/b/C', final name 'x/y/Z'",
				"field 'acme/orig/MyView.count': seed name 'a', final name 'd'",
				"method 'acme/orig/MyView.onClick(android.view.View)': seed name 'b', final name 'e'",
			}, seed.GetCompatibilityConflicts (final, new [] {
				"C\tacme/orig/MyView",
				"F\tacme/orig/MyView\tcount",
				"M\tacme/orig/MyView\tonClick(android.view.View)",
			}));
		}

		[Test]
		public void IgnoresMappingsRemovedByFinalShrinking ()
		{
			R8Mapping seed = R8Mapping.Parse (new StringReader ("""
				acme.orig.Kept -> a.b.C:
				    void kept() -> a
				    void removed() -> b
				acme.orig.Removed -> a.b.D:
				    int value -> a
				    void removed() -> b

				"""));
			R8Mapping final = R8Mapping.Parse (new StringReader ("""
				acme.orig.Kept -> a.b.C:
				    void kept() -> a
				acme.orig.Removed -> R8$$REMOVED$$CLASS$$0:
				    int value -> z
				    void removed() -> z

				"""));

			CollectionAssert.IsEmpty (seed.GetCompatibilityConflicts (final, new [] {
				"C\tacme/orig/Removed",
				"F\tacme/orig/Removed\tvalue",
				"M\tacme/orig/Removed\tremoved()",
				"M\tacme/orig/Kept\tremoved()",
			}));
		}

		[Test]
		public void TracksMappingsUsedByManagedRewriting ()
		{
			R8Mapping mapping = R8Mapping.Parse (new StringReader ("""
				acme.orig.MyView -> a.b.C:
				    int count -> a
				    void onClick(android.view.View) -> b

				"""));

			Assert.IsTrue (mapping.TryGetRenamedClass ("acme/orig/MyView", out _));
			Assert.IsTrue (mapping.TryGetRenamedField ("acme/orig/MyView", "count", out _));
			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "onClick", new [] { "android.view.View" }, out _));

			CollectionAssert.AreEquivalent (new [] {
				"C\tacme/orig/MyView",
				"F\tacme/orig/MyView\tcount",
				"M\tacme/orig/MyView\tonClick(android.view.View)",
			}, mapping.AccessedEntries);
		}
	}
}
