using System.IO;

namespace Microsoft.Android.Tasks;

public class GenerateTypeMapMemberProguardConfiguration : GenerateTypeMapProguardConfiguration
{
	public override string TaskPrefix => "GTMMPC";

	protected override void WriteClassRule (TextWriter writer, string name)
	{
		writer.WriteLine ($"-keepclassmembers class {name} {{ *; }}");
		writer.WriteLine ($"-keepclassmembers interface {name} {{ *; }}");
	}
}
