using System;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Collections;
using System.IO;
using System.Xml;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks {

	public class CalculateLayoutCodeBehind : Task {

		readonly string LayoutDirSuffix = "layout";
		readonly string classSuffix = "class";
		readonly string toolsNamespace = "http://schemas.xamarin.com/android/tools";

		/// <summary>
		/// Designer Specific Property. Can be used to calcualte the code behind
		/// file for one specific file. Saves having to process all the resources.
		/// </summary>
		/// <value>The specific file.</value>
		public ITaskItem SpecificFile { get; set; }

		[Required]
		public ITaskItem [] ResourceFiles { get; set; }

		[Output]
		public ITaskItem [] CodeBehindFiles { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("CalculateLayoutCodeBehind Task");
			Log.LogDebugMessage ("  SpecificFile: {0}", SpecificFile);
			Log.LogDebugTaskItems ("  ResourceFiles:", ResourceFiles);

			string partialClassNames = null;
			var codeBehindFiles = new List<ITaskItem> ();
			if (SpecificFile != null && File.Exists (SpecificFile.ItemSpec)) {
				string fileName = SpecificFile.ItemSpec;
				if (IsCodeBehindLayoutFile (fileName, out partialClassNames))
					CalculateCodeBehindFilenames (fileName, partialClassNames, codeBehindFiles);
			} else {
				foreach (var item in ResourceFiles) {
					string fileName = item.ItemSpec;
					if (!IsCodeBehindLayoutFile (fileName, out partialClassNames))
						continue;
					CalculateCodeBehindFilenames (fileName, partialClassNames, codeBehindFiles);
				}
			}

			CodeBehindFiles = codeBehindFiles.ToArray ();
			if (CodeBehindFiles.Length == 0) {
				Log.LogDebugMessage ("  No layout file qualifies for code-behind generation");
			}
			Log.LogDebugTaskItems ("  CodeBehindFiles:", CodeBehindFiles);
			return !Log.HasLoggedErrors;
		}

		void CalculateCodeBehindFilenames (string fileName, string partialClassNames, List<ITaskItem> codeBehindFiles)
		{
			string [] classes = partialClassNames?.Split (',');
			if (classes == null || classes.Length == 0)
				return;

			foreach (string c in classes) {
				string cl = c?.Trim ();
				if (String.IsNullOrEmpty (cl))
					continue;

				codeBehindFiles.Add(CreateCodeBehindTaskItem (fileName, cl));
			}
		}

		ITaskItem CreateCodeBehindTaskItem (string fileName, string partialClassName)
		{
			var ret = new TaskItem (fileName);
			ret.SetMetadata("CodeBehindFileName", $"{Path.GetFileNameWithoutExtension (fileName)}-{partialClassName}.g.cs");
			ret.SetMetadata("ClassName", partialClassName);
			return ret;
		}

		protected bool IsCodeBehindLayoutFile (string fileName, out string partialClassNames)
		{
			partialClassNames = null;
			if (String.IsNullOrEmpty (fileName) || !File.Exists(fileName))
				return false;

			if (!Path.GetDirectoryName(fileName).EndsWith (LayoutDirSuffix, StringComparison.OrdinalIgnoreCase))
				return false;

			if (!fileName.EndsWith(".axml", StringComparison.OrdinalIgnoreCase) && !fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
				return false;

			using (var fs = File.OpenRead (fileName)) {
				using (var reader = XmlReader.Create (fs)) {
					while (reader.Read ()) {
						if (reader.NodeType != XmlNodeType.Element)
							continue;
						if (reader.IsStartElement ()) {
							if (reader.HasAttributes) {
								partialClassNames = reader.GetAttribute (classSuffix, toolsNamespace);
								if (!string.IsNullOrEmpty (partialClassNames)) {
									return true;
								}

							}
							// only read the root element. if it doesn't have the data we need
							// its not an auto layout file.
							break;
						}
					}
				}
			}
			return false;
		}
	}
}