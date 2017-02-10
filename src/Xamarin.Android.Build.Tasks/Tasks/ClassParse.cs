// Copyright (C) 2012 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text;
using Bytecode = Xamarin.Android.Tools.Bytecode;
using System.Diagnostics;

namespace Xamarin.Android.Tasks
{
	public class ClassParse : Task
	{
		[Required]
		public string OutputFile { get; set; }

		[Required]
		public ITaskItem[] SourceJars { get; set; }

		public ITaskItem[] JavaDocPaths { get; set; }

		public ITaskItem[] Java7DocPaths { get; set; }

		public ITaskItem[] Java8DocPaths { get; set; }

		public ITaskItem[] DroidDocPaths { get; set; }

		public ITaskItem [] DroidDoc2Paths { get; set; }

		public ITaskItem [] JavaDocs { get; set; }

		public IEnumerable<ITaskItem> DocsPaths {
			get {
				Func<ITaskItem[],IEnumerable<ITaskItem>> f = l => l ?? Enumerable.Empty<ITaskItem> ();
				return f (JavaDocPaths).Concat (f (Java7DocPaths)).Concat (f (Java8DocPaths)).Concat (f (DroidDocPaths)).Concat (f (DroidDoc2Paths)).Concat (f (JavaDocs));
			}
		}

		public override bool Execute ()
		{
			Log.LogDebugMessage ("ClassParse Task");
			Log.LogDebugMessage ("  OutputFile: {0}", OutputFile);
			Log.LogTaskItems ("  SourceJars: ", SourceJars);
			Log.LogTaskItems ("  JavaDocPaths: ", JavaDocPaths);
			Log.LogTaskItems ("  Java7DocPaths: ", Java7DocPaths);
			Log.LogTaskItems ("  Java8DocPaths: ", Java8DocPaths);
			Log.LogTaskItems ("  DroidDocPaths: ", DroidDocPaths);
			Log.LogTaskItems ("  DroidDoc2Paths: ", DroidDoc2Paths);
			Log.LogTaskItems ("  JavaDocs: ", JavaDocs);

			using (var output = new StreamWriter (OutputFile, append: false, 
						encoding: new UTF8Encoding (encoderShouldEmitUTF8Identifier: false))) {
				Bytecode.Log.OnLog = LogEventHandler;
				var classPath = new Bytecode.ClassPath () {
					ApiSource = "class-parse",
					DocumentationPaths = (DocsPaths ?? Enumerable.Empty<ITaskItem> ()).Select(x => x.ItemSpec)
				};
				foreach (var jar in SourceJars) {
					if (Bytecode.ClassPath.IsJarFile (jar.ItemSpec)) {
						classPath.Load (jar.ItemSpec);
					}
				}
				classPath.SaveXmlDescription (output);
			}
			return true;
		}

		void LogEventHandler (TraceLevel type, int verbosity, string message, params object[] args)
		{
			switch (type) {
			case TraceLevel.Error:
				Log.LogError (message, args);
				break;
			case TraceLevel.Warning:
				Log.LogWarning (message, args);
				break;
			case TraceLevel.Info:
				Log.LogMessage ((MessageImportance)verbosity, message, args);
				break;
			default:
				Log.LogDebugMessage (message, args);
				break;

			}
		}
	}
}

