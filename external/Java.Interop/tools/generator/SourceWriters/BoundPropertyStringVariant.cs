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
		public BoundPropertyStringVariant (Property property, CodeGenerationOptions opt)
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
				SetBody.Add ($"var jls = value == null ? null : new global::Java.Lang.String (value);");
				SetBody.Add ($"{property.AdjustedName} = jls;");
				SetBody.Add ($"if (jls != null) jls.Dispose ();");
			}
		}
	}
}
