using System;
using System.IO;
using System.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class GenerateGdbEnvironment : Task {
    
		[Required]
		public  string  OutputFile        { get; set; }
		public  string  GdbSymbolsPath    { get; set; }
		public  int     GdbTargetPort     { get; set; }

		public override bool Execute ()
		{
			using (var o = File.CreateText (OutputFile)) {
				o.NewLine  = "\n";
				
				var symbolsPath = FixupPath (GdbSymbolsPath);
				
				o.WriteLine ("set solib-search-path {0}", FixupPath (GdbSymbolsPath));
				o.WriteLine ("file {0}",                  FixupPath (Path.Combine (GdbSymbolsPath, "app_process")));
				o.WriteLine ("target remote :{0}",        GdbTargetPort);
				o.WriteLine ("handle SIGXCPU SIG33 SIG35 SIGPWR SIGTTIN SIGTTOU SIGSYS nostop noprint");
			}

			return true;
		}
		
		static string FixupPath (string path)
		{
			if (string.IsNullOrEmpty (path))
				return path;
			return Path.GetFullPath (path).Replace ('\\', '/');
		}
	}
}

