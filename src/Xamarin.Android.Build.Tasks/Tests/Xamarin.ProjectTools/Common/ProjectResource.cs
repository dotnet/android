using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
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
}
