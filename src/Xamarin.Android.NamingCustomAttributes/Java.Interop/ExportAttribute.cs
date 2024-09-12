using System;
using System.Diagnostics.CodeAnalysis;

namespace Java.Interop {

	[Serializable]
	[AttributeUsage (AttributeTargets.Method | AttributeTargets.Constructor, 
			AllowMultiple=false, 
			Inherited=false)]
#if !NETSTANDARD2_0
	[RequiresUnreferencedCode ("[ExportAttribute] uses dynamic features.")]
#endif
#if !JCW_ONLY_TYPE_NAMES
	public
#endif  // !JCW_ONLY_TYPE_NAMES
	partial class ExportAttribute : Attribute {

		[DynamicDependency (DynamicallyAccessedMemberTypes.All, "Java.Interop.DynamicCallbackCodeGenerator", "Mono.Android.Export")]
		public ExportAttribute ()
		{
		}
		
		[DynamicDependency (DynamicallyAccessedMemberTypes.All, "Java.Interop.DynamicCallbackCodeGenerator", "Mono.Android.Export")]
		public ExportAttribute (string name)
		{
			Name = name;
		}

		public string?                Name                    {get; private set;}
		public string?                SuperArgumentsString    {get; set;}
		public Type []?               Throws                  {get; set;}
		internal string []?           ThrownNames             {get; set;} // msbuild internal use
	}
}


