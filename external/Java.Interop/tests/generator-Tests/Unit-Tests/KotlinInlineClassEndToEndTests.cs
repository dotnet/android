using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Java.Interop.Tools.Generator;
using MonoDroid.Generation;
using NUnit.Framework;
using Xamarin.Android.Binder;
using Xamarin.Android.Tools.Bytecode;

namespace generatortests
{
	// dotnet/java-interop#1431 (Phase 2): end-to-end test that drives real
	// Kotlin .class files (compiled by tests/Xamarin.Android.Tools.Bytecode-Tests/
	// kotlin-gradle/) through KotlinFixups -> XmlClassDeclarationBuilder ->
	// XmlApiImporter -> JavaInteropCodeGenerator and asserts the projected C#
	// output uses the generated `readonly partial struct` wrapper types in
	// method signatures while keeping the JNI marshaling on the underlying
	// primitive.
	[TestFixture]
	public class KotlinInlineClassEndToEndTests
	{
		[Test]
		public void GeneratorProjectsRealKotlinInlineClassesAsStructs ()
		{
			var apiXml = BuildApiXmlFromKotlinFixture ();

			// Sanity: api.xml carries the Phase 2 attributes class-parse emits.
			StringAssert.Contains ("kotlin-inline-class=\"true\"", apiXml);
			StringAssert.Contains ("kotlin-inline-class-jni-type=\"Lxat/bytecode/tests/MyColor;\"", apiXml);
			StringAssert.Contains ("kotlin-inline-class-return-jni-type=\"Lxat/bytecode/tests/MyDp;\"", apiXml);

			var output = GenerateCSharp (apiXml, out var gens);

			// MyColor / MyAlpha / MyDp must be projected as readonly partial structs.
			StringAssert.Contains ("readonly partial struct MyColor", output);
			StringAssert.Contains ("readonly partial struct MyAlpha", output);
			StringAssert.Contains ("readonly partial struct MyDp", output);

			// Underlying-primitive marshaling: MyColor/MyAlpha back J (long),
			// MyDp backs F (float).
			StringAssert.Contains ("public static implicit operator long (MyColor", output);
			StringAssert.Contains ("public static implicit operator MyColor (long", output);
			StringAssert.Contains ("public static implicit operator float (MyDp", output);
			StringAssert.Contains ("public static implicit operator MyDp (float", output);

			// Widgets.tint(MyColor) / tint(MyAlpha) / tint(MyDp) overloads must
			// project the inline-class param in the C# signature. The Kotlin
			// compiler mangles the JVM names for inline-class binary compat
			// (e.g. `tint-Rn_QMJI`); we recover the unmangled name so they
			// emit as plain C# overloads distinguished by struct type.
			StringAssert.Contains ("Tint (Xat.Bytecode.Tests.MyColor color)", output);
			StringAssert.Contains ("Tint (Xat.Bytecode.Tests.MyAlpha alpha)", output);
			StringAssert.Contains ("Tint (Xat.Bytecode.Tests.MyDp dp)", output);

			// Widgets.pad(MyDp): MyDp -> the return type uses MyDp.
			StringAssert.Contains ("Xat.Bytecode.Tests.MyDp Pad (Xat.Bytecode.Tests.MyDp dp)", output);
			StringAssert.Contains ("Xat.Bytecode.Tests.MyDp Pad (Xat.Bytecode.Tests.MyDp dp1, Xat.Bytecode.Tests.MyDp dp2)", output);

			// Widgets.tintColor: MyColor -> a Kotlin `var` typed as an inline
			// class projects as a C# property typed as the wrapper struct, with
			// the getter/setter still marshaling the underlying primitive (long)
			// across JNI. This exercises the KotlinFixups.FixupProperty path.
			StringAssert.Contains ("Xat.Bytecode.Tests.MyColor TintColor", output);

			// And the JVM-mangled hash-suffix names must NOT leak into the
			// generated C# (regression guard for the unmangling path).
			StringAssert.DoesNotContain ("Tint_", output);
			StringAssert.DoesNotContain ("Pad_", output);

			// And no `Java.Lang.Object`-derived peer class for the inline classes
			// (the wrapper struct fully replaces the peer-class binding).
			StringAssert.DoesNotContain ("public partial class MyColor", output);
			StringAssert.DoesNotContain ("public partial class MyAlpha", output);
			StringAssert.DoesNotContain ("public partial class MyDp", output);

			// All three inline classes survived as ClassGen entries with the
			// IsKotlinInlineClass flag set.
			Assert.IsTrue (gens.OfType<ClassGen> ().Count (g => g.IsKotlinInlineClass) >= 3,
				$"Expected at least 3 inline-class ClassGens, generator output was:\n{output}");
		}

		// Run the four kotlin-gradle .class files through KotlinFixups and
		// XmlClassDeclarationBuilder to produce the same api.xml the generator
		// would normally consume off disk.
		static string BuildApiXmlFromKotlinFixture ()
		{
			var classes = new List<ClassFile> {
				LoadClassFile ("MyColor.class"),
				LoadClassFile ("MyAlpha.class"),
				LoadClassFile ("MyDp.class"),
				LoadClassFile ("Widgets.class"),
			};
			Xamarin.Android.Tools.Bytecode.KotlinFixups.Fixup (classes);

			var classPath = new ClassPath { ApiSource = "class-parse" };
			foreach (var c in classes)
				classPath.Add (c);

			var sw = new StringWriter ();
			classPath.SaveXmlDescription (sw);
			var xml = sw.ToString ();

			// XmlApiImporter needs java.lang.Object in the symbol table so the
			// generated peer types (and their RetVal/Parameter symbols) resolve
			// during Validate. Splice a minimal stub package into the api root.
			var doc = XDocument.Parse (xml);
			doc.Root.AddFirst (XElement.Parse (
				"<package name='java.lang' jni-name='java/lang'>" +
				"<class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />" +
				"</package>"));
			return doc.ToString ();
		}

		static ClassFile LoadClassFile (string resource)
		{
			var assembly = typeof (KotlinInlineClassEndToEndTests).Assembly;
			var name = assembly.GetManifestResourceNames ()
				.FirstOrDefault (n => n.EndsWith ("." + resource, System.StringComparison.OrdinalIgnoreCase))
				?? throw new FileNotFoundException ($"Embedded resource '{resource}' not found.");
			using (var stream = assembly.GetManifestResourceStream (name))
				return new ClassFile (stream);
		}

		// Drive the parsed api.xml through XmlApiImporter + Validate + the
		// JavaInteropCodeGenerator to produce the C# binding text.
		static string GenerateCSharp (string apiXml, out List<GenBase> gens)
		{
			var options = new CodeGenerationOptions {
				CodeGenerationTarget = CodeGenerationTarget.JavaInterop1,
			};
			var sb = new System.Text.StringBuilder ();
			var writer = new StringWriter (sb);
			var generator = options.CreateCodeGenerator (writer);

			var doc = XDocument.Parse (apiXml);
			gens = XmlApiImporter.Parse (doc, options);

			foreach (var gen in gens)
				options.SymbolTable.AddType (gen);

			foreach (var gen in gens)
				gen.FixupAccessModifiers (options);

			// dotnet/java-interop#1431 (Phase 2): match the real CodeGenerator
			// pipeline (see CodeGenerator.cs line 209), which runs
			// Java.Interop.Tools.Generator.Transformation.KotlinFixups.Fixup
			// (including RemoveCollidingSiblings) before Validate. Without
			// this, the test would mask the interaction between Phase 1's
			// mangled-name dedup and Phase 2's inline-class projection.
			Java.Interop.Tools.Generator.Transformation.KotlinFixups.Fixup (gens);

			foreach (var gen in gens)
				gen.Validate (options, new GenericParameterDefinitionList (), generator.Context);

			foreach (var gen in gens)
				gen.FillProperties ();

			foreach (var gen in gens)
				gen.FixupMethodOverrides (options);

			var info = new GenerationInfo ("", "", "MyAssembly");
			foreach (var gen in gens) {
				generator.Context.ContextTypes.Push (gen);
				generator.WriteType (gen, string.Empty, info);
				generator.Context.ContextTypes.Pop ();
			}

			return sb.ToString ();
		}
	}
}
