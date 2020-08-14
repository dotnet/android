using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	// This is a field that is not a constant, and thus we need to generate it as a
	// property so it can access the Java field.
	public class BoundFieldAsProperty : PropertyWriter
	{
		readonly Field field;
		readonly CodeGenerationOptions opt;

		public BoundFieldAsProperty (GenBase type, Field field, CodeGenerationOptions opt)
		{
			this.field = field;
			this.opt = opt;

			Name = field.Name;

			var fieldType = field.Symbol.IsArray ? "IList<" + field.Symbol.ElementType + ">" + opt.NullableOperator : opt.GetTypeReferenceName (field);
			PropertyType = new TypeReferenceWriter (fieldType);

			Comments.Add ($"// Metadata.xml XPath field reference: path=\"{type.MetadataXPathReference}/field[@name='{field.JavaName}']\"");

			if (field.IsEnumified)
				Attributes.Add (new GeneratedEnumAttr ());

			Attributes.Add (new RegisterAttr (field.JavaName, additionalProperties: field.AdditionalAttributeString ()));

			if (field.IsDeprecated)
				Attributes.Add (new ObsoleteAttr (field.DeprecatedComment, field.IsDeprecatedError) { NoAtSign = true });

			SetVisibility (field.Visibility);
			UseExplicitPrivateKeyword = true;

			IsStatic = field.IsStatic;

			HasGet = true;

			if (!field.IsConst)
				HasSet = true;
		}

		public override void Write (CodeWriter writer)
		{
			// This is just a temporary hack to write the [GeneratedEnum] attribute before the // Metadata.xml
			// comment so that we are 100% equal to pre-refactor.
			var generated_attr = Attributes.OfType<GeneratedEnumAttr> ().FirstOrDefault ();

			generated_attr?.WriteAttribute (writer);
			writer.WriteLine ();

			base.Write (writer);
		}

		public override void WriteAttributes (CodeWriter writer)
		{
			// Part of above hack ^^
			foreach (var att in Attributes.Where (p => !(p is GeneratedEnumAttr)))
				att.WriteAttribute (writer);
		}

		protected override void WriteGetterBody (CodeWriter writer)
		{
			writer.WriteLine ($"const string __id = \"{field.JavaName}.{field.Symbol.JniName}\";");
			writer.WriteLine ();

			var invokeType = SourceWriterExtensions.GetInvokeType (field.GetMethodPrefix);
			var indirect = field.IsStatic ? "StaticFields" : "InstanceFields";
			var invoke = "Get{0}Value";

			invoke = string.Format (invoke, invokeType);

			writer.WriteLine ($"var __v = {field.Symbol.ReturnCast}_members.{indirect}.{invoke} (__id{(field.IsStatic ? "" : ", this")});");

			if (field.Symbol.IsArray) {
				writer.WriteLine ($"return global::Android.Runtime.JavaArray<{opt.GetOutputName (field.Symbol.ElementType)}>.FromJniHandle (__v.Handle, JniHandleOwnership.TransferLocalRef);");
			} else if (field.Symbol.NativeType != field.Symbol.FullName) {
				writer.WriteLine ($"return {field.Symbol.ReturnCast}{(field.Symbol.FromNative (opt, invokeType != "Object" ? "__v" : "__v.Handle", true) + opt.GetNullForgiveness (field))};");
			} else {
				writer.WriteLine ("return __v;");
			}
		}

		protected override void WriteSetterBody (CodeWriter writer)
		{
			writer.WriteLine ($"const string __id = \"{field.JavaName}.{field.Symbol.JniName}\";");
			writer.WriteLine ();

			var invokeType = SourceWriterExtensions.GetInvokeType (field.GetMethodPrefix);
			var indirect = field.IsStatic ? "StaticFields" : "InstanceFields";

			string arg;
			bool have_prep = false;

			if (field.Symbol.IsArray) {
				arg = opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName ("value"));
				writer.WriteLine ($"IntPtr {arg} = global::Android.Runtime.JavaArray<{opt.GetOutputName (field.Symbol.ElementType)}>.ToLocalJniHandle (value);");
			} else {
				foreach (var prep in field.SetParameters.GetCallPrep (opt)) {
					have_prep = true;
					writer.WriteLine (prep);
				}

				arg = field.SetParameters [0].ToNative (opt);

				if (field.SetParameters.HasCleanup && !have_prep) {
					arg = opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName ("value"));
					writer.WriteLine ($"IntPtr {arg} = global::Android.Runtime.JNIEnv.ToLocalJniHandle (value);");
				}
			}

			writer.WriteLine ("try {");

			writer.WriteLine ($"\t_members.{indirect}.SetValue (__id{(field.IsStatic ? "" : ", this")}, {(invokeType != "Object" ? arg : "new JniObjectReference (" + arg + ")")});");

			writer.WriteLine ("} finally {");
			writer.Indent ();

			if (field.Symbol.IsArray) {
				writer.WriteLine ($"global::Android.Runtime.JNIEnv.DeleteLocalRef ({arg});");
			} else {
				foreach (var cleanup in field.SetParameters.GetCallCleanup (opt))
					writer.WriteLine (cleanup);
				if (field.SetParameters.HasCleanup && !have_prep) {
					writer.WriteLine ($"global::Android.Runtime.JNIEnv.DeleteLocalRef ({arg});");
				}
			}

			writer.Unindent ();
			writer.WriteLine ("}");
		}
	}
}
