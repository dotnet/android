#nullable enable

using System;

namespace Java.Interop
{
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false)]
	public sealed class JniTypeSignatureAttribute : Attribute {

		int arrayRank;

		public JniTypeSignatureAttribute (string simpleReference)
		{
			JniRuntime.JniTypeManager.AssertSimpleReference (simpleReference, nameof (simpleReference));

			SimpleReference     = simpleReference;
		}

		public      bool        IsKeyword               {get; set;}

		public      string      SimpleReference         {get; private set;}
		public      int         ArrayRank               {
			get {return arrayRank; }
			set {
				if (value < 0)
					throw new ArgumentException ("ArrayRank cannot be less than zero.", nameof (value));
				arrayRank = value;
			}
		}

		public      bool        GenerateJavaPeer        {get; set;}
	}
}

