using System;

namespace Java.Interop {

	[Serializable]
	[AttributeUsage (AttributeTargets.Method, 
			AllowMultiple=false, 
			Inherited=false)]
#if !JCW_ONLY_TYPE_NAMES
	public
#endif  // !JCW_ONLY_TYPE_NAMES
	partial class ExportFieldAttribute : Attribute {

		public ExportFieldAttribute (string name)
		{
			Name = name;
		}

		public string                 Name                    {get; set;}
	}
}


