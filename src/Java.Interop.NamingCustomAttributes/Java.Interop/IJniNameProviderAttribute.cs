using System;
namespace Java.Interop
{
#if !JCW_ONLY_TYPE_NAMES
	public
#endif  // !JCW_ONLY_TYPE_NAMES
	interface IJniNameProviderAttribute
	{
		string Name { get; }
	}
}
