using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

#nullable enable

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

namespace Xamarin.Android.Tools.BytecodeTests
{
	[TestFixture]
	public class JSpecifyAnnotationTests : ClassFileFixture
	{
		static XElement BuildXml (string classResource, string? packageInfoResource = null)
		{
			var classPath = new ClassPath ();
			if (packageInfoResource != null)
				classPath.Add (LoadClassFile (packageInfoResource));
			var c = LoadClassFile (classResource);
			classPath.Add (c);

			var packageInfo = packageInfoResource != null
				? classPath.GetPackageInfo (c.PackageName)
				: null;
			return new XmlClassDeclarationBuilder (c, packageInfo).ToXElement ();
		}

		static XElement Method (XElement classXml, string name)
			=> classXml.Elements ().First (e => (e.Name.LocalName == "method" || e.Name.LocalName == "constructor")
				&& e.Attribute ("name")?.Value == name);

		static XElement Field (XElement classXml, string name)
			=> classXml.Elements ("field").First (f => f.Attribute ("name")?.Value == name);

		static XElement FirstParameter (XElement method)
		{
			return method.Element ("parameter")
				?? throw new AssertionException ($"method '{method.Attribute ("name")?.Value}' has no <parameter>");
		}

		static string? Attr (XElement e, string name) => e.Attribute (name)?.Value;

		[Test]
		public void PackageMarked_DefaultReferenceMembers_AreNotNull ()
		{
			var xml = BuildXml ("JSpecifyPackageMarked.class", "package-info.class");

			var m = Method (xml, "defaultReturn");
			Assert.AreEqual ("true", Attr (m, "return-not-null"),
				"unannotated reference return in @NullMarked package must be not-null");

			Assert.AreEqual ("true", Attr (FirstParameter (m), "not-null"),
				"unannotated reference parameter in @NullMarked package must be not-null");

			var f = Field (xml, "defaultField");
			Assert.AreEqual ("true", Attr (f, "not-null"),
				"unannotated reference field in @NullMarked package must be not-null");
		}

		[Test]
		public void PackageMarked_PrimitiveMembers_HaveNoNotNullAttribute ()
		{
			var xml = BuildXml ("JSpecifyPackageMarked.class", "package-info.class");

			var m = Method (xml, "primitiveReturn");
			Assert.IsNull (Attr (m, "return-not-null"),
				"primitive return must not get a not-null attribute");

			var f = Field (xml, "primitiveField");
			Assert.IsNull (Attr (f, "not-null"),
				"primitive field must not get a not-null attribute");
		}

		[Test]
		public void PackageMarked_NullableAnnotationOverridesScope ()
		{
			var xml = BuildXml ("JSpecifyPackageMarked.class", "package-info.class");

			var m = Method (xml, "nullableReturn");
			Assert.IsNull (Attr (m, "return-not-null"),
				"@Nullable return must override the @NullMarked default");

			Assert.IsNull (Attr (FirstParameter (m), "not-null"),
				"@Nullable parameter must override the @NullMarked default");

			var f = Field (xml, "nullableField");
			Assert.IsNull (Attr (f, "not-null"),
				"@Nullable field must override the @NullMarked default");
		}

		// `@NullUnmarked` on a method is intentionally not honored yet
		// (only class- and package-level scope are). This test pins the
		// current behavior so the limitation is visible.
		[Test]
		public void PackageMarked_MethodLevelNullUnmarked_IsNotYetHonored ()
		{
			var xml = BuildXml ("JSpecifyPackageMarked.class", "package-info.class");

			var m = Method (xml, "unmarkedReturn");
			Assert.AreEqual ("true", Attr (m, "return-not-null"),
				"method-level @NullUnmarked is not yet honored; the package-level @NullMarked scope still applies");
		}

		[Test]
		public void PackageMarked_TypeVariableUsage_HasParametricNullness ()
		{
			var xml = BuildXml ("JSpecifyPackageMarked.class", "package-info.class");

			var m = Method (xml, "typeVariableReturn");
			Assert.IsNull (Attr (m, "return-not-null"),
				"unannotated type-variable return must not gain not-null from the scope default");

			Assert.IsNull (Attr (FirstParameter (m), "not-null"),
				"unannotated type-variable parameter must not gain not-null from the scope default");
		}

		[Test]
		public void ClassMarked_DefaultReferenceMembers_AreNotNull ()
		{
			var xml = BuildXml ("JSpecifyClassMarked.class");

			var m = Method (xml, "defaultReturn");
			Assert.AreEqual ("true", Attr (m, "return-not-null"),
				"unannotated reference return in @NullMarked class must be not-null");

			Assert.AreEqual ("true", Attr (FirstParameter (m), "not-null"),
				"unannotated reference parameter in @NullMarked class must be not-null");

			var f = Field (xml, "defaultField");
			Assert.AreEqual ("true", Attr (f, "not-null"),
				"unannotated reference field in @NullMarked class must be not-null");
		}

		[Test]
		public void ClassMarked_NullableOverrides ()
		{
			var xml = BuildXml ("JSpecifyClassMarked.class");

			var m = Method (xml, "nullableReturn");
			Assert.IsNull (Attr (m, "return-not-null"),
				"@Nullable return must override the class-level @NullMarked default");

			Assert.IsNull (Attr (FirstParameter (m), "not-null"),
				"@Nullable parameter must override the class-level @NullMarked default");

			var f = Field (xml, "nullableField");
			Assert.IsNull (Attr (f, "not-null"),
				"@Nullable field must override the class-level @NullMarked default");
		}

		[Test]
		public void Unmarked_NoScope_NoDefaultNotNull ()
		{
			var xml = BuildXml ("JSpecifyUnmarked.class");

			var m = Method (xml, "defaultReturn");
			Assert.IsNull (Attr (m, "return-not-null"),
				"outside a @NullMarked scope, unannotated reference returns must not gain not-null");

			Assert.IsNull (Attr (FirstParameter (m), "not-null"),
				"outside a @NullMarked scope, unannotated reference parameters must not gain not-null");

			var f = Field (xml, "defaultField");
			Assert.IsNull (Attr (f, "not-null"),
				"outside a @NullMarked scope, unannotated reference fields must not gain not-null");
		}

		[Test]
		public void Unmarked_ExplicitNonNullAnnotation_IsHonored ()
		{
			var xml = BuildXml ("JSpecifyUnmarked.class");

			var m = Method (xml, "nonNullReturn");
			Assert.AreEqual ("true", Attr (m, "return-not-null"),
				"explicit @NonNull return must produce not-null even outside a @NullMarked scope");

			Assert.AreEqual ("true", Attr (FirstParameter (m), "not-null"),
				"explicit @NonNull parameter must produce not-null even outside a @NullMarked scope");

			var f = Field (xml, "nonNullField");
			Assert.AreEqual ("true", Attr (f, "not-null"),
				"explicit @NonNull field must produce not-null even outside a @NullMarked scope");
		}

		[Test]
		public void PackageMarked_DeclarationNullable_OverridesScope ()
		{
			var xml = BuildXml ("JSpecifyPackageMarked.class", "package-info.class");

			var m = Method (xml, "declarationNullableReturn");
			Assert.IsNull (Attr (m, "return-not-null"),
				"declaration-level @Nullable return must override the @NullMarked default");

			Assert.IsNull (Attr (FirstParameter (m), "not-null"),
				"declaration-level @Nullable parameter must override the @NullMarked default");

			var f = Field (xml, "declarationNullableField");
			Assert.IsNull (Attr (f, "not-null"),
				"declaration-level @Nullable field must override the @NullMarked default");
		}

		[Test]
		public void PackageMarked_NestedNullable_DoesNotLeakToContainer ()
		{
			var xml = BuildXml ("JSpecifyPackageMarked.class", "package-info.class");

			var f = Field (xml, "nestedNullableField");
			Assert.AreEqual ("true", Attr (f, "not-null"),
				"nested @Nullable on an inner type argument must not affect the container's nullness; "
					+ "the field's List type is non-null in a @NullMarked scope");
		}

		[Test]
		public void TypeAnnotationsAttribute_IsParsed ()
		{
			var c = LoadClassFile ("JSpecifyPackageMarked.class");
			var method = c.Methods.First (m => m.Name == "nullableReturn");
			var typeAnn = method.Attributes
				.OfType<RuntimeInvisibleTypeAnnotationsAttribute> ()
				.FirstOrDefault ()
				?? throw new AssertionException ("javac must emit a RuntimeInvisibleTypeAnnotations attribute for @Nullable members");
			Assert.IsTrue (typeAnn.Annotations.Any (a =>
				a.Annotation.Type == "Lorg/jspecify/annotations/Nullable;"
					&& a.TargetType == TypeAnnotationTargetType.MethodReturn),
				"@Nullable return type annotation must be parsed with MethodReturn target");
			Assert.IsTrue (typeAnn.Annotations.Any (a =>
				a.Annotation.Type == "Lorg/jspecify/annotations/Nullable;"
					&& a.TargetType == TypeAnnotationTargetType.MethodFormalParameter
					&& a.FormalParameterIndex == 0),
				"@Nullable parameter type annotation must be parsed with MethodFormalParameter target");
		}

		[Test]
		public void PackageInfo_IsRecognized ()
		{
			var c = LoadClassFile ("package-info.class");
			Assert.IsTrue (c.IsPackageInfo,
				"package-info.class must be flagged via IsPackageInfo");
		}
	}
}
