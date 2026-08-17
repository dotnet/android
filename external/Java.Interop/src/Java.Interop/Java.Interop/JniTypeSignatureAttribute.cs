#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;

namespace Java.Interop
{
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false)]
	public sealed class JniTypeSignatureAttribute : Attribute {

		int arrayRank;

		public JniTypeSignatureAttribute (string simpleReference)
		{
#if !JCW_ONLY_TYPE_NAMES
			JniRuntime.JniTypeManager.AssertSimpleReference (simpleReference, nameof (simpleReference));
#endif  // !JCW_ONLY_TYPE_NAMES

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

		/// <summary>
		/// Gets or sets whether instances of this type are created by managed code before being passed to Java.
		/// </summary>
		/// <remarks>
		/// Build tooling may trim the Java peer when the managed type is unreachable. This should only be set
		/// for types that are not independently instantiated or discovered from Java.
		/// </remarks>
		public      bool        IsManagedCreated        {get; set;}

		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
		public      Type?       InvokerType             {get; set;}
	}
}
