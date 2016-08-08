using System;

namespace Java.Interop {

	[Serializable]
	[AttributeUsage (AttributeTargets.Method | AttributeTargets.Constructor, 
			AllowMultiple=false, 
			Inherited=false)]
#if !JCW_ONLY_TYPE_NAMES
	public
#endif  // !JCW_ONLY_TYPE_NAMES
	partial class ExportAttribute : Attribute {

		public ExportAttribute ()
		{
		}
		
		public ExportAttribute (string name)
		{
			Name = name;
		}

		public string                 Name                    {get; private set;}
		public string                 SuperArgumentsString    {get; set;}
		public Type []                Throws                  {get; set;}
		internal string []            ThrownNames             {get; set;} // msbuild internal use
	}
}


