using System;

namespace Java.Interop {

	[Serializable]
	[AttributeUsage (AttributeTargets.Parameter | AttributeTargets.ReturnValue, 
			AllowMultiple=false, 
			Inherited=false)]
#if !JCW_ONLY_TYPE_NAMES
	public
#endif  // !JCW_ONLY_TYPE_NAMES
	partial class ExportParameterAttribute : Attribute {

		public ExportParameterAttribute (ExportParameterKind kind)
		{
			Kind = kind;
		}

		public ExportParameterKind Kind { get; private set; }
	}
}


