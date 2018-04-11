using System;

namespace Android.Runtime {

	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Constructor | AttributeTargets.Field | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property)]
	public sealed class RegisterAttribute : Attribute {

		public RegisterAttribute (string name)
		{
			Name = name;
		}

		public RegisterAttribute (string name, string signature, string connector)
			: this (name)
		{
			Signature = signature;
			Connector = connector;
		}

		public string Connector {
			get;
			set;
		}

		public string Name {
			get;
			set;
		}

		public string Signature {
			get;
			set;
		}

		public bool DoNotGenerateAcw {get; set;}
	}
}
