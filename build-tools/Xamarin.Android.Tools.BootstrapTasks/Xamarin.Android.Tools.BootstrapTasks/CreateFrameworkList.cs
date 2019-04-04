using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class CreateFrameworkList : Task
	{
		[Required]
		public                  ITaskItem       FrameworkListFile           { get; set; }

		[Required]
		public                  ITaskItem       FrameworkDirectory          { get; set; }

		[Required]
		public                  ITaskItem[]     FrameworkFiles              { get; set; }

		public                  ITaskItem[]     FrameworkFileOverrides      { get; set; }

		[Required]
		public                  string          Redist                      { get; set; }

		[Required]
		public                  string          Name                        { get; set; }

		public override bool Execute()
		{
			var files = new List<ITaskItem>(FrameworkFiles);
			files.Sort ((x, y) => {
					var a = Path.GetFileNameWithoutExtension (x.ItemSpec);
					var b = Path.GetFileNameWithoutExtension (y.ItemSpec);
					return string.Compare (a, b, StringComparison.OrdinalIgnoreCase);
			});
			var contents = new XElement ("FileList",
					new XAttribute ("Redist", Redist),
					new XAttribute ("Name", Name),
					files.Select (f => ToFileElement (f)));
			contents.Save (FrameworkListFile.ItemSpec);
			return true;
		}

		XElement ToFileElement (ITaskItem file)
		{
			var path = Path.Combine (FrameworkDirectory.ItemSpec, file.ItemSpec);
			if (!File.Exists (path)) {
				path = Path.Combine (FrameworkDirectory.ItemSpec, "Facades", file.ItemSpec);
			}

			var assemblyName    = AssemblyName.GetAssemblyName (path);

			var taskVersion     = Nullable (file.GetMetadata ("Version"));
			var overrideVersion = Nullable (FrameworkFileOverrides?.FirstOrDefault (o => o.ItemSpec == file.ItemSpec)?.GetMetadata ("Version"));
			var assemblyVersion = assemblyName.Version.ToString ();
			var version         = taskVersion ?? overrideVersion ?? assemblyVersion;

			var publicKeyToken  = string.Join ("", assemblyName.GetPublicKeyToken ().Select(b => b.ToString ("x2")));

			return new XElement ("File",
					new XAttribute ("AssemblyName", assemblyName.Name),
					new XAttribute ("Version", version),
					new XAttribute ("PublicKeyToken", publicKeyToken),
					new XAttribute ("ProcessorArchitecture", assemblyName.ProcessorArchitecture.ToString ()));
		}

		static string Nullable (string value) => string.IsNullOrEmpty (value) ? null : value;
	}
}
