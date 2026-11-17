using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Xamarin.Android.Tools.Bytecode;

namespace Xamarin.Android.Tools.BytecodeTests
{
	// Exercises the real Kotlin bytecode produced by the Gradle fixture under
	// kotlin-gradle/ to confirm that the JVM-level mangling we expect (and that
	// the generator's KotlinFixups must now de-collide) is actually what kotlinc
	// emits for @JvmInline value-class parameters. See dotnet/java-interop#1431.
	[TestFixture]
	public class KotlinInlineClassCollisionTests : ClassFileFixture
	{
		[Test]
		public void Widgets_HasCollidingHashMangledSiblings ()
		{
			var klass = LoadClassFile ("Widgets.class");

			// Kotlin emits one mangled method per inline-class overload:
			//   tint-<hash>(J)V    for MyColor (ULong-backed)
			//   tint-<hash>(J)V    for MyAlpha (ULong-backed) — collides with MyColor
			//   tint-<hash>(F)V    for MyDp    (Float-backed) — unique
			var tints = klass.Methods
				.Where (m => m.Name.StartsWith ("tint-", StringComparison.Ordinal))
				.ToList ();

			Assert.AreEqual (3, tints.Count, "Expected three `tint-<hash>` overloads from the Gradle fixture.");

			var longTints = tints.Where (m => m.Descriptor == "(J)V").ToList ();
			Assert.AreEqual (2, longTints.Count,
				"Expected two `tint-<hash>(J)V` siblings (MyColor + MyAlpha) — this is the multi-sibling collision case from dotnet/java-interop#1431.");

			Assert.AreEqual (1, tints.Count (m => m.Descriptor == "(F)V"),
				"Expected one unique `tint-<hash>(F)V` (MyDp) that should survive deduplication.");
		}

		[Test]
		public void Widgets_HasNonCollidingHashMangledOverloads ()
		{
			var klass = LoadClassFile ("Widgets.class");

			var pads = klass.Methods
				.Where (m => m.Name.StartsWith ("pad-", StringComparison.Ordinal))
				.ToList ();

			Assert.AreEqual (2, pads.Count);
			CollectionAssert.AreEquivalent (
				new [] { "(F)F", "(FF)F" },
				pads.Select (m => m.Descriptor).ToArray (),
				"`pad` overloads have distinct JVM signatures and should both survive after rename.");
		}

		[Test]
		public void InlineClasses_AreEmittedAsValueClasses ()
		{
			// Sanity check that @JvmInline really produced a JvmInline annotation on
			// the inline-class type — this is what step (2) of #1431 will key on.
			var myColor = LoadClassFile ("MyColor.class");

			var annotations = myColor.Attributes
				.OfType<RuntimeVisibleAnnotationsAttribute> ()
				.SelectMany (a => a.Annotations)
				.Select (a => a.Type)
				.ToList ();

			Assert.Contains ("Lkotlin/jvm/JvmInline;", annotations);
		}

		// dotnet/java-interop#1431 (Phase 2): KotlinFixups must surface the inline
		// class's underlying primitive on each `@JvmInline value class` ClassFile so
		// the generator can later emit a strongly-typed wrapper struct.
		[Test]
		public void Fixup_StampsKotlinInlineClassUnderlyingJniType ()
		{
			var classes = LoadInlineClassFixture ();

			KotlinFixups.Fixup (classes);

			var byName = classes.ToDictionary (c => c.ThisClass.Name.Value);

			// MyColor and MyAlpha are both ULong-backed -> JNI primitive `J`.
			Assert.AreEqual ("J", byName ["xat/bytecode/tests/MyColor"].KotlinInlineClassUnderlyingJniType);
			Assert.AreEqual ("J", byName ["xat/bytecode/tests/MyAlpha"].KotlinInlineClassUnderlyingJniType);

			// MyDp is Float-backed -> JNI primitive `F`.
			Assert.AreEqual ("F", byName ["xat/bytecode/tests/MyDp"].KotlinInlineClassUnderlyingJniType);

			// Non-inline classes must NOT be stamped.
			Assert.IsNull (byName ["xat/bytecode/tests/Widgets"].KotlinInlineClassUnderlyingJniType);
		}

		// dotnet/java-interop#1431 (Phase 2): for every Kotlin function whose
		// source-level parameter type is a known inline class, KotlinFixups must
		// stamp the `KotlinInlineClassJniType` on that parameter so the generator
		// can swap the parameter's symbol for a wrapper-struct projection while
		// keeping JNI marshaling on the underlying primitive.
		[Test]
		public void Fixup_StampsParameterInlineClassJniType ()
		{
			var classes = LoadInlineClassFixture ();
			KotlinFixups.Fixup (classes);

			var widgets = classes.Single (c => c.ThisClass.Name.Value == "xat/bytecode/tests/Widgets");

			var tints = widgets.Methods
				.Where (m => m.Name.StartsWith ("tint-", StringComparison.Ordinal))
				.ToList ();

			// Each tint() should have exactly one parameter, and that parameter
			// should be stamped with the JNI signature of the inline class it
			// originally came from in Kotlin source.
			var stampedJniTypes = tints
				.Select (m => m.GetParameters ().Single ().KotlinInlineClassJniType)
				.Where (j => !string.IsNullOrEmpty (j))
				.OrderBy (j => j, StringComparer.Ordinal)
				.ToList ();

			CollectionAssert.AreEquivalent (
				new [] {
					"Lxat/bytecode/tests/MyAlpha;",
					"Lxat/bytecode/tests/MyColor;",
					"Lxat/bytecode/tests/MyDp;",
				},
				stampedJniTypes,
				"Each tint(<inline class>) parameter must be stamped with its inline-class JNI signature.");
		}

		// dotnet/java-interop#1431 (Phase 2): when a method's Kotlin-source-level
		// return type is a `@JvmInline value class`, KotlinFixups must stamp the
		// method's `KotlinInlineClassReturnJniType` so the generator can project
		// the return type to the wrapper struct.
		[Test]
		public void Fixup_StampsReturnInlineClassJniType ()
		{
			var classes = LoadInlineClassFixture ();
			KotlinFixups.Fixup (classes);

			var widgets = classes.Single (c => c.ThisClass.Name.Value == "xat/bytecode/tests/Widgets");

			var pads = widgets.Methods
				.Where (m => m.Name.StartsWith ("pad-", StringComparison.Ordinal))
				.ToList ();

			Assert.IsTrue (pads.Count > 0, "Expected `pad` overloads in fixture.");
			Assert.IsTrue (
				pads.All (m => m.KotlinInlineClassReturnJniType == "Lxat/bytecode/tests/MyDp;"),
				$"All `pad` overloads return MyDp; got: [{string.Join (", ", pads.Select (m => m.KotlinInlineClassReturnJniType ?? "<null>"))}]");
		}

		// dotnet/java-interop#1431 (Phase 2): KotlinFixups.FixupProperty must also
		// stamp inline-class JNI types on property getter return values and setter
		// parameters, not just on function parameters/returns.
		[Test]
		public void Fixup_StampsPropertyInlineClassJniType ()
		{
			var classes = LoadInlineClassFixture ();
			KotlinFixups.Fixup (classes);

			var widgets = classes.Single (c => c.ThisClass.Name.Value == "xat/bytecode/tests/Widgets");

			// `var tintColor: MyColor` emits a mangled getter/setter pair —
			// the property finder must look past the inline-class mangled name
			// suffix (`-<hash>`) AND past the erased primitive return/parameter
			// to bind these to the Kotlin property.
			var getter = widgets.Methods.Single (m => m.Name.StartsWith ("getTintColor-", StringComparison.Ordinal));
			var setter = widgets.Methods.Single (m => m.Name.StartsWith ("setTintColor-", StringComparison.Ordinal));

			Assert.AreEqual ("Lxat/bytecode/tests/MyColor;", getter.KotlinInlineClassReturnJniType,
				"Inline-class typed property getter must be stamped with the wrapper's JNI signature.");
			Assert.AreEqual ("Lxat/bytecode/tests/MyColor;", setter.GetParameters ().Single ().KotlinInlineClassJniType,
				"Inline-class typed property setter parameter must be stamped with the wrapper's JNI signature.");
		}

		// dotnet/java-interop#1431 (Phase 2): the new fields must round-trip
		// through XmlClassDeclarationBuilder onto the api.xml that the generator
		// consumes.
		[Test]
		public void XmlOutput_ContainsKotlinInlineClassAttributes ()
		{
			var classes = LoadInlineClassFixture ();
			KotlinFixups.Fixup (classes);

			var classPath = new ClassPath { ApiSource = "class-parse" };
			foreach (var c in classes)
				classPath.Add (c);

			var sw = new System.IO.StringWriter ();
			classPath.SaveXmlDescription (sw);
			var xml = sw.ToString ();

			StringAssert.Contains ("kotlin-inline-class=\"true\"", xml);
			StringAssert.Contains ("kotlin-inline-class-underlying-jni-type=\"J\"", xml);
			StringAssert.Contains ("kotlin-inline-class-underlying-jni-type=\"F\"", xml);
			StringAssert.Contains ("kotlin-inline-class-jni-type=\"Lxat/bytecode/tests/MyColor;\"", xml);
			StringAssert.Contains ("kotlin-inline-class-return-jni-type=\"Lxat/bytecode/tests/MyDp;\"", xml);
		}

		static List<ClassFile> LoadInlineClassFixture () => new List<ClassFile> {
			LoadClassFile ("MyColor.class"),
			LoadClassFile ("MyAlpha.class"),
			LoadClassFile ("MyDp.class"),
			LoadClassFile ("Widgets.class"),
		};
	}
}
