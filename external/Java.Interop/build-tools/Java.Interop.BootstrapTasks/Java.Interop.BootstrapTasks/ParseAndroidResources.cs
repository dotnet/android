using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;


namespace Java.Interop.BootstrapTasks
{
	public class ParseAndroidResources : Task
	{
		public  ITaskItem AndroidResourceFile       { get; set; }
		public  ITaskItem OutputFile                { get; set; }
		public  string    DeclaringNamespaceName    { get; set; }
		public  string    DeclaringClassName        { get; set; }

		public override bool Execute ()
		{
			using (var o = File.CreateText (OutputFile.ItemSpec)) {
				o.WriteLine ($"namespace {DeclaringNamespaceName};");
				o.WriteLine ();
				o.WriteLine ($"partial class {DeclaringClassName} {{");
				var resources	= ParseAndroidResourceFile (AndroidResourceFile.ItemSpec);
				foreach (var declType in resources.Keys.OrderBy (x => x)) {
					o.WriteLine ($"\tpublic static class @{declType} {{");
					var decls = resources [declType];
					foreach (var decl in decls.Keys.OrderBy (x => x)) {
						o.WriteLine ($"\t\tpublic const int {decl} = {decls [decl]};");
					}
					o.WriteLine ("\t}");
				}
				o.WriteLine ("}");
				o.WriteLine ();
			}

			return !Log.HasLoggedErrors;
		}

		Dictionary<string, Dictionary<string, string>> ParseAndroidResourceFile (string file)
		{
			var resources       = new Dictionary<string, Dictionary<string, string>> ();
			using (var reader    = File.OpenText (file)) {
				string line;
				while ((line = reader.ReadLine ()) != null) {
					if (line.StartsWith ("#"))
						continue;
					var items = line.Split (' ');
					if (items.Length != 4)
						continue;
					var type    = items [0];
					if (string.Compare (type, "int", StringComparison.Ordinal) != 0)
						continue;
					var decl    = items [1];
					var name    = items [2];
					var value   = items [3];
					if (!resources.TryGetValue (decl, out var declResources))
						resources.Add (decl, declResources = new Dictionary<string, string>());
					declResources.Add (name, value);
				}
			}

			return resources;
		}
	}
}
