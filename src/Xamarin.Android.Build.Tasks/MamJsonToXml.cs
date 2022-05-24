using System;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class MamJsonToXml : AndroidTask
	{
		public  override    string  TaskPrefix  => "A2C";

		[Required]
		public  ITaskItem[] MappingFiles        { get; set; }

		[Required]
		public  ITaskItem   XmlMappingOutput    { get; set; }

		public override bool RunTask ()
		{
			var parser = new MamJsonParser (this.CreateTaskLogger ());
			foreach (var file in MappingFiles) {
				parser.Load (file.ItemSpec);
			}
			var tree   = parser.ToXml ();
			using (var o = File.CreateText (XmlMappingOutput.ItemSpec)) {
				o.WriteLine (tree.ToString ());
			}
			return true;
		}
	}
}
