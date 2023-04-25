using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;
using Xamarin.Android.Binder;

namespace generator.SourceWriters
{
	// When a property has a type of 'Java.Lang.ICharSequence' we usually generate
	// an overload with type 'string' as a convenience for the user.
	public class BoundPropertyStringVariant : PropertyWriter
	{
		public BoundPropertyStringVariant (Property property, CodeGenerationOptions opt, BoundAbstractProperty original)
			: this (property, opt, original.IsVirtual)
		{ }

		public BoundPropertyStringVariant (Property property, CodeGenerationOptions opt, BoundProperty original)
			: this(property, opt, original.IsVirtual)
		{ }

		private BoundPropertyStringVariant (Property property, CodeGenerationOptions opt, bool isOriginalVirtual)
		{
			var is_array = property.Getter.RetVal.IsArray;

			Name = property.Name;

			PropertyType = new TypeReferenceWriter ("string" + (is_array ? "[]" : string.Empty)) {
				Nullable = opt.SupportNullableReferenceTypes
			};

			SetVisibility ((property.Setter ?? property.Getter).Visibility);

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, property.Getter, opt);

			string arrayConvertMethod = opt.GetStringArrayToCharSequenceArrayMethodName ();

			HasGet = true;

			if (is_array)
				GetBody.Add ($"return {arrayConvertMethod} ({property.AdjustedName});");
			else
				GetBody.Add ($"return {property.AdjustedName} == null ? null : {property.AdjustedName}.ToString ();");

			if (property.Setter is null)
				return;

			HasSet = true;

			if (is_array) {
				SetBody.Add ($"global::Java.Lang.ICharSequence[] jlsa = {arrayConvertMethod} (value);");
				SetBody.Add ($"{property.AdjustedName} = jlsa;");
				SetBody.Add ($"foreach (var jls in jlsa) if (jls != null) jls.Dispose ();");
			} else {
				if (isOriginalVirtual) {
					SetBody.Add ($"var jls = value == null ? null : new global::Java.Lang.String (value);");
					SetBody.Add ($"{property.AdjustedName} = jls;");
					SetBody.Add ($"if (jls != null) jls.Dispose ();");
				} else {
					// Emit a "fast" path if the property is non-virtual
					IsUnsafe = true;
					var method = property.Setter;
					var parameter = method.Parameters [0];

					SetBody.Add ($"const string __id = \"{method.JavaName}.{method.JniSignature}\";");
					SetBody.Add ($"global::Java.Interop.JniObjectReference {parameter.ToNative (opt)} = global::Java.Interop.JniEnvironment.Strings.NewString (value);");

					SetBody.Add ("try {");

					SourceWriterExtensions.AddMethodBodyTryBlock (SetBody, method, opt);

					SetBody.Add ("} finally {");
					SetBody.Add ($"\tglobal::Java.Interop.JniObjectReference.Dispose (ref {parameter.ToNative (opt)});");
					SetBody.Add ("}");
				}
			}
		}
	}
}
