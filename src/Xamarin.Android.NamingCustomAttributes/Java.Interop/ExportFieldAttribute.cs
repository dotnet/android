using System;
using System.Reflection;

namespace Java.Interop {

	[Serializable]
	[AttributeUsage (AttributeTargets.Method, 
			AllowMultiple=false, 
			Inherited=false)]
#if !JCW_ONLY_TYPE_NAMES
	public
#endif  // !JCW_ONLY_TYPE_NAMES
	partial class ExportFieldAttribute : BaseExportAttribute {

		public ExportFieldAttribute (string name)
		{
			Name = name;
		}

		public string                 Name                    {get; set;}

		internal override Delegate CreateDynamicCallback (MethodInfo method)
		{
			return CreateDynamicCallbackCore (method);
		}
	}
}
