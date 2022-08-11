using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class BoundMethodExtensionStringOverload : MethodWriter
	{
		readonly Method method;
		readonly CodeGenerationOptions opt;
		readonly string self_type;

		public BoundMethodExtensionStringOverload (Method method, CodeGenerationOptions opt, string selfType)
		{
			this.method = method;
			this.opt = opt;
			self_type = selfType;

			Name = method.Name;
			IsStatic = true;

			SetVisibility (method.Visibility);
			ReturnType = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal).Replace ("Java.Lang.ICharSequence", "string").Replace ("global::string", "string"));

			SourceWriterExtensions.AddObsolete (Attributes, method.Deprecated);

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);

			Parameters.Add (new MethodParameterWriter ("self", new TypeReferenceWriter (selfType)) { IsExtension = true });
			this.AddMethodParametersStringOverloads (method.Parameters, opt);
		}

		protected override void WriteBody (CodeWriter writer)
		{
			SourceWriterExtensions.WriteMethodStringOverloadBody (writer, method, opt, true);
		}
	}
}
