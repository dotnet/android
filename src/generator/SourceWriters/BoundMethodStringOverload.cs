using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class BoundMethodStringOverload : MethodWriter
	{
		readonly Method method;
		readonly CodeGenerationOptions opt;

		public BoundMethodStringOverload (Method method, CodeGenerationOptions opt, GenBase declaringType, bool? isShadow = null)
		{
			this.method = method;
			this.opt = opt;

			Name = method.Name;
			IsStatic = method.IsStatic;

			SetVisibility (method.Visibility);
			IsShadow = isShadow ?? declaringType.RequiresNewStringOverload (method, opt);
			ReturnType = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal).Replace ("Java.Lang.ICharSequence", "string").Replace ("global::string", "string"));

			SourceWriterExtensions.AddObsolete (Attributes, method.Deprecated, opt, deprecatedSince: method.DeprecatedSince);
			SourceWriterExtensions.AddRestrictToWarning (Attributes, method.AnnotatedVisibility, false, opt);

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);

			method.JavadocInfo?.AddJavadocs (Comments);

			this.AddMethodParametersStringOverloads (method.Parameters, opt);
		}

		protected override void WriteBody (CodeWriter writer)
		{
			SourceWriterExtensions.WriteMethodStringOverloadBody (writer, method, opt, false);
		}
	}
}
