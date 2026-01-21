namespace Java.Interop
{
	/// <summary>
	/// A type map universe used for mapping interfaces to their invoker types.
	/// This is separate from the main <see cref="Java.Lang.Object"/> universe
	/// to allow interface-to-invoker lookups via the .NET Type Mapping API.
	/// </summary>
	/// <remarks>
	/// This type is used as a generic parameter for <c>TypeMapAssociationAttribute&lt;InvokerUniverse&gt;</c>
	/// to create a separate type mapping namespace for interface-to-invoker relationships.
	/// 
	/// Example generated code:
	/// <code>
	/// [assembly: TypeMapAssociation&lt;InvokerUniverse&gt;(typeof(IMyInterface), typeof(IMyInterfaceInvoker))]
	/// </code>
	/// </remarks>
	sealed class InvokerUniverse
	{
	}
}
