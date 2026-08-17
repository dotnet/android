#nullable enable

using System;

using Mono.Cecil;

namespace Android.Runtime {

	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Constructor | AttributeTargets.Field | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property)]
#if !JCW_ONLY_TYPE_NAMES
	public
#endif  // !JCW_ONLY_TYPE_NAMES
	sealed class RegisterAttribute : Attribute, Java.Interop.IJniNameProviderAttribute {

		string? connector;
		string name;
		string? signature;

		public RegisterAttribute (string name)
		{
			this.name = name;
		}

		public RegisterAttribute (string name, string signature, string connector)
			: this (name)
		{
			this.connector = connector;
			this.signature = signature;
		}
#if HAVE_CECIL
		public RegisterAttribute (string name, CustomAttribute? originAttribute)
			: this (name)
		{
			OriginAttribute = originAttribute;
		}

		public RegisterAttribute (string name, string signature, string connector, CustomAttribute? originAttribute)
			: this (name, signature, connector)
		{
			OriginAttribute = originAttribute;
		}

		public CustomAttribute? OriginAttribute { get; }
#endif  // HAVE_CECIL

		public string? Connector {
			get { return connector; }
			set { connector = value; }
		}

		public string Name {
			get { return name; }
			set { name = value; }
		}

		public string? Signature {
			get { return signature; }
			set { signature = value; }
		}

		public bool DoNotGenerateAcw {get; set;}

		/// <summary>
		/// Gets or sets whether instances of this type are created by managed code before being passed to Java.
		/// </summary>
		/// <remarks>
		/// Build tooling may trim the Java peer when the managed type is unreachable. This should only be set
		/// for types that are not independently instantiated or discovered from Java.
		/// </remarks>
		public bool IsManagedCreated {get; set;}

		public int ApiSince {get; set;}
	}
}
