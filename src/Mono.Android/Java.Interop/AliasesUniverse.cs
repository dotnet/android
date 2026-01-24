namespace Java.Interop
{
	/// <summary>
	/// A type map universe used for linking aliased types to their alias holder.
	/// This is separate from the main <see cref="Java.Lang.Object"/> universe
	/// to prevent key collisions.
	/// </summary>
	/// <remarks>
	/// This type is used as a generic parameter for <c>TypeMapAssociationAttribute&lt;AliasesUniverse&gt;</c>
	/// to link each aliased target type back to its alias holder type. This is only used by the
	/// trimmer and is never queried at runtime.
	/// 
	/// Example generated code:
	/// <code>
	/// [assembly: TypeMapAssociation&lt;AliasesUniverse&gt;(typeof(MyHandler), typeof(com_example_MyHandler_Aliases))]
	/// </code>
	/// </remarks>
	sealed class AliasesUniverse
	{
	}
}
