using System;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;
using System.Linq;

namespace Xamarin.Android.Tools.BytecodeTests
{
	[TestFixture]
	public class NullableAnnotationTests : ClassFileFixture
	{
		[Test]
		public void RuntimeInvisibleAnnotations ()
		{
			var c = LoadClassFile ("NotNullClass.class");

			// Method with no annotations
			var null_method = c.Methods.First (m => m.Name == "nullFunc");

			Assert.AreEqual (0, null_method.Attributes.OfType<RuntimeInvisibleAnnotationsAttribute> ().Count ());
			Assert.AreEqual (0, null_method.Attributes.OfType<RuntimeInvisibleParameterAnnotationsAttribute> ().Count ());

			// Method with not-null parameter and return value annotations
			var notnull_method = c.Methods.First (m => m.Name == "notNullFunc");
			var return_ann = notnull_method.Attributes.OfType<RuntimeInvisibleAnnotationsAttribute> ().FirstOrDefault ()?.Annotations;
			var param_ann = notnull_method.Attributes.OfType<RuntimeInvisibleParameterAnnotationsAttribute> ().FirstOrDefault ()?.Annotations;

			Assert.NotNull (return_ann);
			Assert.IsTrue (return_ann.Any (a => a.Type == "Landroid/annotation/NonNull;"));

			Assert.NotNull (param_ann);
			Assert.IsTrue (param_ann.Any (a => a.ParameterIndex == 0 && a.Annotations[0].Type == "Landroid/annotation/NonNull;"));

			// Field with no annotations
			var null_field = c.Fields.First (f => f.Name == "nullField");

			Assert.AreEqual (0, null_field.Attributes.OfType<RuntimeInvisibleAnnotationsAttribute> ().Count ());
			Assert.AreEqual (0, null_field.Attributes.OfType<RuntimeInvisibleParameterAnnotationsAttribute> ().Count ());

			// Field with not-null annotation
			var notnull_field = c.Fields.First (f => f.Name == "notNullField");

			var field_ann = notnull_method.Attributes.OfType<RuntimeInvisibleAnnotationsAttribute> ().FirstOrDefault ()?.Annotations;

			Assert.NotNull (field_ann);
			Assert.IsTrue (field_ann.Any (a => a.Type == "Landroid/annotation/NonNull;"));
		}

		[Test]
		public void NullableAnnotationOutput ()
		{
			var c = LoadClassFile ("NotNullClass.class");
			var builder = new XmlClassDeclarationBuilder (c);
			var xml = builder.ToXElement ();

			var method = xml.Elements ("method").First (m => m.Attribute ("name").Value == "notNullFunc");
			Assert.AreEqual ("true", method.Attribute ("return-not-null").Value);

			var parameter = method.Element ("parameter");
			Assert.AreEqual ("true", parameter.Attribute ("not-null").Value);

			var field = xml.Elements ("field").First (f => f.Attribute ("name").Value == "notNullField");
			Assert.AreEqual ("true", field.Attribute ("not-null").Value);
		}
	}
}

