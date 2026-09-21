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

		public MethodAsyncWrapper (Method method, CodeGenerationOptions opt)
		{
			JavaProjectionWarnings.AddObsoleteUseSuppressions (this, method, opt);

			this.method = method;
			this.opt = opt;

			Name = method.AdjustedName + "Async";
			IsStatic = method.IsStatic;

			SetVisibility (method.Visibility);

			ReturnType = new TypeReferenceWriter ("global::System.Threading.Tasks.Task");

			if (!method.IsVoid)
				ReturnType.Name += "<" + opt.GetTypeReferenceName (method.RetVal) + ">";

			// The wrapper exists only to invoke the method it wraps, so a deprecated method
			// has a deprecated wrapper.
			SourceWriterExtensions.AddObsolete (Attributes, method.Deprecated, opt, deprecatedSince: method.DeprecatedSince);

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);

			Body.Add ($"return global::System.Threading.Tasks.Task.Run (() => {method.AdjustedName} ({method.Parameters.GetCall (opt)}));");

			this.AddMethodParameters (method.Parameters, opt);
		}
	}
}
