using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class MethodAsyncWrapper : MethodWriter
	{
		readonly Method method;
		readonly CodeGenerationOptions opt;
		readonly bool suppress_obsolete_warning;

		public MethodAsyncWrapper (Method method, CodeGenerationOptions opt, GenBase declaringType = null, bool? isShadow = null, bool? isObsoleteContext = null)
		{
			this.method = method;
			this.opt = opt;

			Name = method.AdjustedName + "Async";
			IsStatic = method.IsStatic;

			SetVisibility (method.Visibility);
			IsShadow = isShadow ?? declaringType?.RequiresNew (Name, method, opt) == true;

			ReturnType = new TypeReferenceWriter ("global::System.Threading.Tasks.Task");

			if (!method.IsVoid)
				ReturnType.Name += "<" + opt.GetTypeReferenceName (method.RetVal) + ">";

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);

			Body.Add ($"return global::System.Threading.Tasks.Task.Run (() => {method.AdjustedName} ({method.Parameters.GetCall (opt)}));");

			this.AddMethodParameters (method.Parameters, opt);

			var inObsoleteContext = isObsoleteContext ?? SourceWriterExtensions.IsTypeObsolete (declaringType ?? method.DeclaringType, opt);
			suppress_obsolete_warning = !inObsoleteContext && (
				SourceWriterExtensions.WillEmitObsolete (method.Deprecated, opt, deprecatedSince: method.DeprecatedSince) ||
				SourceWriterExtensions.ReferencesObsoleteType (method, opt));
		}

		public override void Write (CodeWriter writer)
		{
			SourceWriterExtensions.WriteWithWarningsDisabled (writer, suppress_obsolete_warning, "0618", () => base.Write (writer));
		}
	}
}
