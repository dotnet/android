using System;
using System.IO;
using System.CodeDom.Compiler;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;
using MonoDroid.Tuner;

namespace Xamarin.Android.Tasks
{
	public class GenerateResourceDesignerIntermediateClass : AndroidTask
	{
		public override string TaskPrefix => "GRDIC";

		private const string ResourceDesigner = $"{FixLegacyResourceDesignerStep.DesignerAssemblyNamespace}.Resource";
		private const string ResourceDesignerConstants = $"{FixLegacyResourceDesignerStep.DesignerAssemblyNamespace}.ResourceConstant";

		private const string CSharpTemplate = @"// This is an Auto Generated file DO NOT EDIT
using System;

namespace %NAMESPACE% {
	public class Resource : %BASECLASS% {
	}
}
";
		private const string FSharpTemplate = @"// This is an Auto Generated file DO NOT EDIT
namespace %NAMESPACE%

type Resource = %BASECLASS%
";

		public string Namespace { get; set; }
		public bool IsApplication { get; set; } = false;
		public ITaskItem OutputFile { get; set; }
		public override bool RunTask ()
		{
			string ns = IsApplication ? ResourceDesignerConstants : ResourceDesigner;
			var extension = Path.GetExtension (OutputFile.ItemSpec);
			var language = string.Compare (extension, ".fs", StringComparison.OrdinalIgnoreCase) == 0 ? "F#" : CodeDomProvider.GetLanguageFromExtension (extension);
			//bool isVB = string.Equals (extension, ".vb", StringComparison.OrdinalIgnoreCase);
			bool isFSharp = string.Equals (language, "F#", StringComparison.OrdinalIgnoreCase);
			bool isCSharp = string.Equals (language, "C#", StringComparison.OrdinalIgnoreCase);
			string template = "";
			if (isCSharp)
				template = CSharpTemplate.Replace ("%NAMESPACE%", Namespace).Replace ("%BASECLASS%", ns);
			else if (isFSharp)
				template = FSharpTemplate.Replace ("%NAMESPACE%", Namespace).Replace ("%BASECLASS%", ns);

			Files.CopyIfStringChanged (template, OutputFile.ItemSpec);
			return !Log.HasLoggedErrors;
		}
	}
}
