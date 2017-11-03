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

		public ITaskItem [] DocumentationPaths { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("ClassParse Task");
			Log.LogDebugMessage ("  OutputFile: {0}", OutputFile);
			Log.LogTaskItems ("  SourceJars: ", SourceJars);
			Log.LogTaskItems ("  DocumentationPaths: ", DocumentationPaths);

			using (var output = new StreamWriter (OutputFile, append: false, 
						encoding: new UTF8Encoding (encoderShouldEmitUTF8Identifier: false))) {
				Bytecode.Log.OnLog = LogEventHandler;
				var classPath = new Bytecode.ClassPath () {
					ApiSource = "class-parse",
					DocumentationPaths = (DocumentationPaths ?? Enumerable.Empty<ITaskItem> ()).Select(x => x.ItemSpec)
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

