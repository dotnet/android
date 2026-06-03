using System;
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
	}
}
