using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class MethodExtensionAsyncWrapper : MethodWriter
	{
		public MethodExtensionAsyncWrapper (Method method, CodeGenerationOptions opt, string selfType)
		{
			JavaProjectionWarnings.AddObsoleteUseSuppressions (this, method, opt);

			Name = method.AdjustedName + "Async";
			IsStatic = true;

			SetVisibility (method.Visibility);

			ReturnType = new TypeReferenceWriter ("global::System.Threading.Tasks.Task");

			if (!method.IsVoid)
				ReturnType.Name += "<" + opt.GetTypeReferenceName (method.RetVal) + ">";

			// The wrapper exists only to invoke the method it wraps, so a deprecated method
			// has a deprecated wrapper.
			SourceWriterExtensions.AddObsolete (Attributes, method.Deprecated, opt, deprecatedSince: method.DeprecatedSince);

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);

			Body.Add ($"return global::System.Threading.Tasks.Task.Run (() => self.{method.AdjustedName} ({method.Parameters.GetCall (opt)}));");

			Parameters.Add (new MethodParameterWriter ("self", new TypeReferenceWriter (selfType)) { IsExtension = true });

			this.AddMethodParameters (method.Parameters, opt);
		}
	}
}
