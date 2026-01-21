using System;

namespace Java.Interop
{
	/// <summary>
	/// Attribute applied to alias types that map multiple .NET types to the same Java class name.
	/// This enables disambiguation when resolving UCO function pointers for types that share a Java class name.
	/// </summary>
	/// <remarks>
	/// When multiple .NET types are registered with the same Java class name (e.g., multiple classes
	/// extending the same Android Activity), this attribute is applied to an alias type that stores
	/// the mapping keys for each variant.
	/// 
	/// Example:
	/// <code>
	/// // Two .NET types registered with the same Java class "B"
	/// [Register("B")]
	/// class B1 : A { }
	/// 
	/// [Register("B")]
	/// class B2 : A { }
	/// 
	/// // Generated alias type that maps indices to specific types
	/// [JavaInteropAliases("B[0]", "B[1]")]
	/// class B_Aliases { }
	/// </code>
	/// 
	/// The alias keys are used by the native code to specify which .NET type variant
	/// should be used when resolving function pointers for marshal methods.
	/// </remarks>
	[AttributeUsage (AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	sealed class JavaInteropAliasesAttribute : Attribute
	{
		/// <summary>
		/// Creates a new instance of the <see cref="JavaInteropAliasesAttribute"/> with the specified alias keys.
		/// </summary>
		/// <param name="aliasKeys">The alias keys that map to specific .NET type variants (e.g., "B[0]", "B[1]").</param>
		public JavaInteropAliasesAttribute (params string[] aliasKeys)
		{
			AliasKeys = aliasKeys ?? throw new ArgumentNullException (nameof (aliasKeys));
		}

		/// <summary>
		/// Gets the alias keys that map indices to specific .NET type variants.
		/// </summary>
		public string[] AliasKeys { get; }
	}
}
