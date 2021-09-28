using System;

namespace Android
{
#if NETCOREAPP
	[Obsolete ("For .NET 6+, please use: [assembly: global::System.Reflection.AssemblyMetadata(\"IsTrimmable\", \"True\")]")]
#endif  // NETCOREAPP
	[AttributeUsage (AttributeTargets.Assembly)]
	public sealed class LinkerSafeAttribute : Attribute
	{
	}
}

