#nullable enable
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tasks;

namespace Microsoft.Android.Tasks;

[MSBuildMultiThreadableTask]
public class MamJsonToXml : MultithreadedAndroidTask
{
	public  override    string  TaskPrefix  => "A2C";

	[Required]
	public  ITaskItem[] MappingFiles        { get; set; } = [];

	[Required]
	public  ITaskItem?  XmlMappingOutput    { get; set; }

	public override bool RunTask ()
	{
		if (XmlMappingOutput == null) {
			throw new System.InvalidOperationException ($"{nameof (XmlMappingOutput)} is required.");
		}
		var parser = new MamJsonParser (this.CreateTaskLogger ());
		foreach (var file in MappingFiles) {
			using var stream = File.OpenRead (TaskEnvironment.GetAbsolutePath (file.ItemSpec));
			parser.Load (stream);
		}
		var tree   = parser.ToXml ();
		using (var o = File.CreateText (TaskEnvironment.GetAbsolutePath (XmlMappingOutput.ItemSpec))) {
			o.WriteLine (tree.ToString ());
		}
		return true;
	}
}
