using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Linker;

namespace MonoDroid.Tuner
{
	class LinkerOptions
	{
		public AssemblyDefinition MainAssembly { get; set; }
		public IEnumerable<AssemblyDefinition> RetainAssemblies { get; set; }
		public string OutputDirectory { get; set; }
		public bool LinkSdkOnly { get; set; }
		public bool LinkNone { get; set; }
		public string [] LinkDescriptions { get; set; }
		public AssemblyResolver Resolver { get; set; }
		public IEnumerable<string> SkippedAssemblies { get; set; }
		public I18nAssemblies I18nAssemblies { get; set; }
		public string ProguardConfiguration { get; set; }
		public bool DumpDependencies { get; set; }
		public string HttpClientHandlerType { get; set; }
		public string TlsProvider { get; set; }
		public bool PreserveJniMarshalMethods { get; set; }
	}
}
