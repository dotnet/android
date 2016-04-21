using System;

namespace Android.App {

	[Serializable]
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Assembly,
			AllowMultiple=true, 
			Inherited=false)]
	public sealed partial class MetaDataAttribute : Attribute {

		public MetaDataAttribute (string name)
		{
			Name = name;
		}

		public string   Name {get; private set;}
		public string   Resource {get; set;}
		public string   Value {get; set;}
	}
}
