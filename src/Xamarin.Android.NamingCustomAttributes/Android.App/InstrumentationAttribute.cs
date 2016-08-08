using System;

namespace Android.App {

	[Serializable]
	[AttributeUsage (AttributeTargets.Assembly | AttributeTargets.Class, 
			AllowMultiple=true, 
			Inherited=false)]
	public sealed partial class InstrumentationAttribute : Attribute {

		public InstrumentationAttribute ()
		{
		}

		public bool                   FunctionalTest  {get; set;}
		public bool                   HandleProfiling {get; set;}
		public string                 Icon            {get; set;}
		public string                 Label           {get; set;}
		public string                 Name            {get; set;}
		public string                 TargetPackage   {get; set;}
	}
}

