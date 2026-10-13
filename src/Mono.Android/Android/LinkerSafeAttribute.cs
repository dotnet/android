using System;

namespace Android
{
	[Obsolete ("For .NET 6+, please use: [assembly: global::System.Reflection.AssemblyMetadata(\"IsTrimmable\", \"True\")]", error: true)]
	[AttributeUsage (AttributeTargets.Assembly)]
	public sealed class LinkerSafeAttribute : Attribute
	{
	}
}
