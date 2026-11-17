using System.Text;

namespace Xamarin.ProjectTools;

public class ProjectResource
{
	public string Path { get; set; }
	public string Content { get; set; }
	public byte [] BinaryContent { get; set; }
	public DateTimeOffset? Timestamp { get; set; }
	public Encoding Encoding { get; set; }
	public bool Deleted { get; set; }
	public FileAttributes Attributes { get; set;}
}
