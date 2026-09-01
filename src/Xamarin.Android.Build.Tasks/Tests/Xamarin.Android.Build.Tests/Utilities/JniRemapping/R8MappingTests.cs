using System;
using System.IO;
using System.Threading.Tasks;
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
			Assert.IsTrue (mapping.TryGetOriginalMethodName (originalClass, "x", new [] { "int" }, "void", out string first));
			Assert.AreEqual ("first", first);
			Assert.IsTrue (mapping.TryGetOriginalMethodName (originalClass, "x", Array.Empty<string> (), "void", out string second));
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

			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "onClick", new [] { "android.view.View" }, "void", out string renamed1));
			Assert.AreEqual ("a", renamed1);

			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "onClick", new [] { "android.view.View", "int" }, "void", out string renamed2));
			Assert.AreEqual ("b", renamed2);
		}

		[Test]
		public void ParsesMethodMappingWithLeadingAndTrailingLineRanges ()
		{
			// "startLine:endLine:returnType name(params):origStartLine:origEndLine -> obfuscated"
			var mapping = R8Mapping.Parse (new StringReader (
				"acme.orig.MyView -> a.b.C:\n" +
				"    4:10:void onCreate(android.os.Bundle):23:29 -> a\n"));

			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "onCreate", new [] { "android.os.Bundle" }, "void", out string renamed));
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

			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "run", new [] { "example.Foo" }, "void", out string renamed));
			Assert.AreEqual ("a", renamed);
		}

		[Test]
		public void ParsesMethodMappingWithOnlyATrailingLineRange ()
		{
			// No leading "startLine:endLine:" prefix, only the trailing ":originalStart:originalEnd".
			var mapping = R8Mapping.Parse (new StringReader (
				"acme.orig.MyView -> a.b.C:\n" +
				"    void onPause():23:29 -> a\n"));

			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "onPause", Array.Empty<string> (), "void", out string renamed));
			Assert.AreEqual ("a", renamed);
		}

		[Test]
		public void ParsesNoArgMethodMapping ()
		{
			var mapping = R8Mapping.Parse (new StringReader (
				"acme.orig.MyView -> a.b.C:\n" +
				"    void onStart() -> a\n"));

			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "onStart", Array.Empty<string> (), "void", out string renamed));
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
			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", R8Mapping.JniMemberNameToMappingName (".ctor"), new [] { "int" }, "void", out string renamed));
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
			Assert.IsFalse (mapping.TryGetRenamedMethod ("acme/orig/MyView", "missing", Array.Empty<string> (), "void", out _));
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

			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "onCreate", new [] { "android.os.Bundle" }, "void", out string renamedMethod));
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

			Assert.IsTrue (mapping.TryGetRenamedMethod ("androidx/collection/LongSparseArray", "keyAt", new [] { "int" }, "long", out string keyAt));
			Assert.AreEqual ("keyAt", keyAt);
			Assert.IsTrue (mapping.TryGetRenamedMethod ("androidx/collection/LongSparseArray", "indexOfKey", new [] { "long" }, "int", out string indexOfKey));
			Assert.AreEqual ("indexOfKey", indexOfKey);
			Assert.IsFalse (mapping.TryGetRenamedMethod ("androidx/collection/LongSparseArray", "androidx.collection.LongSparseArrayKt.commonGc", new [] { "androidx.collection.LongSparseArray" }, "void", out _));
		}

		[Test]
		public void IgnoresSameClassInlineCallFrameMappings ()
		{
			var mapping = R8Mapping.Parse (new StringReader (
				"acme.orig.MyView -> a.b.C:\n" +
				"    4:4:void inlined():23:23 -> a\n" +
				"    4:4:void caller():42:42 -> a\n" +
				"    # {'id':'com.android.tools.r8.synthesized'}\n"));

			Assert.IsFalse (mapping.TryGetRenamedMethod ("acme/orig/MyView", "inlined", [], "void", out _));
			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "caller", [], "void", out string caller));
			Assert.AreEqual ("a", caller);
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

			Assert.IsFalse (mapping.TryGetRenamedMethod ("androidx/collection/SimpleArrayMap", "getOrDefaultInternal", new [] { "java.lang.Object", "java.lang.Object" }, "java.lang.Object", out _));
			Assert.IsTrue (mapping.TryGetRenamedMethod ("androidx/collection/SimpleArrayMap", "get", new [] { "java.lang.Object" }, "java.lang.Object", out string get));
			Assert.AreEqual ("get", get);
			Assert.IsTrue (mapping.TryGetRenamedMethod ("androidx/collection/SimpleArrayMap", "getOrDefault", new [] { "java.lang.Object", "java.lang.Object" }, "java.lang.Object", out string getOrDefault));
			Assert.AreEqual ("getOrDefault", getOrDefault);
		}

		[Test]
		public void ThrowsOnMemberLineBeforeAnyClassLine ()
		{
			Assert.Throws<FormatException> (() => R8Mapping.Parse (new StringReader ("    int someField -> x\n")));
		}

		[Test]
		public void LoadReportsSourcePathInFormatError ()
		{
			string directory = Path.Combine (Root, "temp", TestName);
			string path = Path.Combine (directory, "seed.map");
			Directory.CreateDirectory (directory);
			File.WriteAllText (path, "not a mapping");

			FormatException error = Assert.Throws<FormatException> (() => R8Mapping.Load (path));

			Assert.That (error.Message, Does.StartWith ($"{path}:1:"));
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

			CollectionAssert.AreEquivalent (new [] {
				"class 'acme/orig/MyView': seed name 'a/b/C', final name 'x/y/Z'",
				"field 'acme/orig/MyView.count': seed name 'a', final name 'd'",
				"method 'acme/orig/MyView.onClick(android.view.View):void': seed name 'b', final name 'e'",
			}, seed.GetCompatibilityConflicts (final, new [] {
				"C\tacme/orig/MyView",
				"F\tacme/orig/MyView\tcount",
				"M\tacme/orig/MyView\tonClick(android.view.View):void",
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
				"M\tacme/orig/Removed\tremoved():void",
				"M\tacme/orig/Kept\tremoved():void",
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
			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "onClick", new [] { "android.view.View" }, "void", out _));

			CollectionAssert.AreEquivalent (new [] {
				"C\tacme/orig/MyView",
				"F\tacme/orig/MyView\tcount",
				"M\tacme/orig/MyView\tonClick(android.view.View):void",
			}, mapping.AccessedEntries);
		}

		[TestCase ("field", "F\tacme/orig/MyView\tcount")]
		[TestCase ("method", "M\tacme/orig/MyView\tonClick(android.view.View):void")]
		[TestCase ("name-only method", "M\tacme/orig/MyView\tonStart():void")]
		public void MemberOnlyLookupTracksOwningClass (string lookupKind, string expectedMemberEntry)
		{
			R8Mapping mapping = R8Mapping.Parse (new StringReader ("""
				acme.orig.MyView -> a.b.C:
				    int count -> a
				    void onClick(android.view.View) -> b
				    void onStart() -> c

				"""));

			bool found = lookupKind switch {
				"field" => mapping.TryGetRenamedField ("acme/orig/MyView", "count", out _),
				"method" => mapping.TryGetRenamedMethod ("acme/orig/MyView", "onClick", new [] { "android.view.View" }, "void", out _),
				"name-only method" => mapping.TryGetRenamedMethodByNameOnly ("acme/orig/MyView", "onStart", out _),
				_ => throw new InvalidOperationException ($"Unknown lookup kind '{lookupKind}'."),
			};

			Assert.IsTrue (found);
			CollectionAssert.AreEqual (new [] {
				"C\tacme/orig/MyView",
				expectedMemberEntry,
			}, mapping.AccessedEntries);
		}

		[Test]
		public void AccessedEntriesIsAThreadSafeSnapshot ()
		{
			R8Mapping mapping = R8Mapping.Parse (new StringReader ("""
				acme.orig.MyView -> a.b.C:
				    int count -> a
				    void onClick(android.view.View) -> b

				"""));
			var emptySnapshot = mapping.AccessedEntries;

			Parallel.For (0, 100, iteration => {
				mapping.TryGetRenamedClass ("acme/orig/MyView", out _);
				mapping.TryGetRenamedField ("acme/orig/MyView", "count", out _);
				mapping.TryGetRenamedMethod ("acme/orig/MyView", "onClick", new [] { "android.view.View" }, "void", out _);
			});

			CollectionAssert.IsEmpty (emptySnapshot);
			CollectionAssert.AreEquivalent (new [] {
				"C\tacme/orig/MyView",
				"F\tacme/orig/MyView\tcount",
				"M\tacme/orig/MyView\tonClick(android.view.View):void",
			}, mapping.AccessedEntries);
		}

		[Test]
		public void ReportsPostLinkEntriesRemovedByFinalR8 ()
		{
			R8Mapping seed = R8Mapping.Parse (new StringReader ("""
				acme.orig.Missing -> a.b.A:
				acme.orig.Removed -> a.b.B:
				acme.orig.Members -> a.b.C:
				    int keptField -> a
				    int missingField -> b
				    void kept() -> a
				    void missing() -> b

				"""));
			R8Mapping final = R8Mapping.Parse (new StringReader ("""
				acme.orig.Removed -> R8$$REMOVED$$CLASS$$0:
				acme.orig.Members -> a.b.C:
				    int keptField -> a
				    void kept() -> a

				"""));

			CollectionAssert.AreEqual (new [] {
				"class 'acme/orig/Missing'",
				"class 'acme/orig/Removed'",
				"field 'acme/orig/Members.missingField'",
				"method 'acme/orig/Members.missing():void'",
			}, seed.GetReachabilityConflicts (final, new [] {
				"C\tacme/orig/Missing",
				"C\tacme/orig/Removed",
				"C\tacme/orig/Members",
				"F\tacme/orig/Members\tkeptField",
				"F\tacme/orig/Members\tmissingField",
				"M\tacme/orig/Members\tkept():void",
				"M\tacme/orig/Members\tmissing():void",
			}));
		}

		[TestCase ("F\tacme/orig/MyView\tcount")]
		[TestCase ("M\tacme/orig/MyView\tonClick():void")]
		public void MemberOnlyManifestReportsRemovedDeclaringClass (string requiredEntry)
		{
			R8Mapping seed = R8Mapping.Parse (new StringReader ("""
				acme.orig.MyView -> a.b.C:
				    int count -> a
				    void onClick() -> b

				"""));
			R8Mapping final = R8Mapping.Parse (new StringReader (""));

			CollectionAssert.AreEqual (new [] { "class 'acme/orig/MyView'" }, seed.GetReachabilityConflicts (final, new [] { requiredEntry }));
		}

		[Test]
		public void ReverseMappingRejectsAmbiguousMemberCandidates ()
		{
			R8Mapping mapping = R8Mapping.Parse (new StringReader ("""
				acme.orig.MyView -> a.b.C:
				    int first -> a
				    java.lang.String second -> a
				    void run() -> b
				    void invoke(int) -> b

				"""));
			IJniNameMapping reverse = mapping.CreateReverseMapping ();

			Assert.IsFalse (reverse.TryMapField ("a/b/C", "a", out _));
			Assert.IsFalse (reverse.TryMapMethodByNameOnly ("a/b/C", "b", out _));
			CollectionAssert.IsEmpty (mapping.AccessedEntries);
		}

		[Test]
		public void ReverseMappingUsesSingleAllowedMemberCandidate ()
		{
			R8Mapping mapping = R8Mapping.Parse (new StringReader ("""
				acme.orig.MyView -> a.b.C:
				    int first -> a
				    java.lang.String second -> a
				    void run() -> b
				    void invoke(int) -> b

				"""));
			mapping.RestrictReverseLookupsTo (new [] {
				"C\tacme/orig/MyView",
				"F\tacme/orig/MyView\tsecond",
				"M\tacme/orig/MyView\tinvoke(int):void",
			});
			IJniNameMapping reverse = mapping.CreateReverseMapping ();

			Assert.IsTrue (reverse.TryMapField ("a/b/C", "a", out string originalField));
			Assert.AreEqual ("second", originalField);
			Assert.IsTrue (reverse.TryMapMethodByNameOnly ("a/b/C", "b", out string originalMethod));
			Assert.AreEqual ("invoke", originalMethod);
			CollectionAssert.AreEquivalent (new [] {
				"C\tacme/orig/MyView",
				"F\tacme/orig/MyView\tsecond",
				"M\tacme/orig/MyView\tinvoke(int):void",
			}, mapping.AccessedEntries);
		}

		[Test]
		public void ReverseMappingRejectsAmbiguousMergedClassCandidates ()
		{
			R8Mapping mapping = R8Mapping.Parse (new StringReader ("""
				acme.orig.First -> a.b.C:
				    int first -> a
				acme.orig.Second -> a.b.C:
				    int second -> a

				"""));
			IJniNameMapping reverse = mapping.CreateReverseMapping ();

			Assert.IsFalse (mapping.TryGetOriginalClass ("a/b/C", out _));
			Assert.IsFalse (reverse.TryMapClass ("a/b/C", out _));
			Assert.IsFalse (reverse.TryMapField ("a/b/C", "a", out _));
			CollectionAssert.IsEmpty (mapping.AccessedEntries);
		}

		[Test]
		public void ReverseMappingUsesSingleAllowedMergedClassCandidate ()
		{
			R8Mapping mapping = R8Mapping.Parse (new StringReader ("""
				acme.orig.First -> a.b.C:
				    int first -> a
				acme.orig.Second -> a.b.C:
				    int second -> a

				"""));
			mapping.RestrictReverseLookupsTo (new [] {
				"C\tacme/orig/Second",
				"F\tacme/orig/Second\tsecond",
			});
			IJniNameMapping reverse = mapping.CreateReverseMapping ();

			Assert.IsTrue (reverse.TryMapClass ("a/b/C", out string originalClass));
			Assert.AreEqual ("acme/orig/Second", originalClass);
			Assert.IsTrue (reverse.TryMapField ("a/b/C", "a", out string originalField));
			Assert.AreEqual ("second", originalField);
			CollectionAssert.AreEquivalent (new [] {
				"C\tacme/orig/Second",
				"F\tacme/orig/Second\tsecond",
			}, mapping.AccessedEntries);
		}

		[Test]
		public void ReverseMappingRejectsMergedClassWithNoAllowedCandidate ()
		{
			R8Mapping mapping = R8Mapping.Parse (new StringReader ("""
				acme.orig.First -> a.b.C:
				acme.orig.Second -> a.b.C:

				"""));
			mapping.RestrictReverseLookupsTo (new [] {
				"C\tacme/orig/Unrelated",
			});
			IJniNameMapping reverse = mapping.CreateReverseMapping ();

			Assert.IsFalse (reverse.TryMapClass ("a/b/C", out _));
			CollectionAssert.IsEmpty (mapping.AccessedEntries);
		}

		[Test]
		public void CreatesDeterministicManifestContent ()
		{
			Assert.AreEqual ("C\tacme/orig/A\nC\tacme/orig/B\n", R8Mapping.CreateManifestContent (new [] {
				"C\tacme/orig/B",
				"C\tacme/orig/A",
			}));
		}

		[Test]
		public void NestedDescriptorTypeMatchesMappingKey ()
		{
			R8Mapping mapping = R8Mapping.Parse (new StringReader ("""
				acme.orig.MyView -> a.b.C:
				    void m(acme.Outer$Inner) -> a

				"""));
			var parameterTypes = JniDescriptorText.MethodDescriptorToJavaParameterTypes ("(Lacme/Outer$Inner;)V");

			CollectionAssert.AreEqual (new [] { "acme.Outer$Inner" }, parameterTypes);
			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "m", parameterTypes, "void", out string renamed));
			Assert.AreEqual ("a", renamed);
		}

		[Test]
		public void DistinguishesMethodsByReturnType ()
		{
			R8Mapping mapping = R8Mapping.Parse (new StringReader ("""
				acme.orig.MyView -> a.b.C:
				    java.lang.Object value() -> a
				    java.lang.String value() -> b

				"""));

			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "value", [], "java.lang.Object", out string objectMethod));
			Assert.AreEqual ("a", objectMethod);
			Assert.IsTrue (mapping.TryGetRenamedMethod ("acme/orig/MyView", "value", [], "java.lang.String", out string stringMethod));
			Assert.AreEqual ("b", stringMethod);
		}
	}
}
