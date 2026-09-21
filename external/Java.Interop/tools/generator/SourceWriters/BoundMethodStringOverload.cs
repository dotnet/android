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

		public BoundMethodStringOverload (Method method, CodeGenerationOptions opt, GenBase declaringType = null)
		{
			this.method = method;
			this.opt = opt;

			Name = method.Name;
			IsStatic = method.IsStatic;

			// The synthesized overload is a separate, non-virtual member, so it hides rather
			// than overrides the overload synthesized for the same method on a base type.
			IsShadow = declaringType != null && !method.IsStatic &&
					declaringType.StringOverloadRequiresNew (Name, method, opt);

			SetVisibility (method.Visibility);
			ReturnType = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal).Replace ("Java.Lang.ICharSequence", "string").Replace ("global::string", "string"));

			SourceWriterExtensions.AddObsolete (Attributes, method.Deprecated, opt, deprecatedSince: method.DeprecatedSince);

			JavaProjectionWarnings.AddObsoleteSuppressions (this, method, opt);
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
