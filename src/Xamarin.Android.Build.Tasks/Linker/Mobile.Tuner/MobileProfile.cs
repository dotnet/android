using System;
using System.Collections.Generic;

using Mono.Cecil;

using Mono.Tuner;

namespace Mobile.Tuner {

	public abstract class MobileProfile : Profile {

		static readonly HashSet<string> Sdk = new HashSet<string> {
			"mscorlib",
			"System",
			"System.ComponentModel.Composition",
			"System.ComponentModel.DataAnnotations",
			"System.Core",
			"System.Data",
			"System.Data.Services.Client",
			"System.IO.Compression.FileSystem",
			"System.IO.Compression",
			"System.Json",
			"System.Net",
			"System.Net.Http",
			"System.Numerics",
			"System.Runtime.Serialization",
			"System.ServiceModel",
			"System.ServiceModel.Web",
			"System.Transactions",
			"System.Web.Services",
			"System.Windows",
			"System.Xml",
			"System.Xml.Linq",
			"System.Xml.Serialization",
			"Microsoft.CSharp",
			"Microsoft.VisualBasic",
			"Mono.CSharp",
			"Mono.Cairo",
			"Mono.CompilerServices.SymbolWriter",
			"Mono.Data.Tds",
			"Mono.Data.Sqlite",
			"Mono.Security",
			"OpenTK",
			"OpenTK-1.0",
			// Facades assemblies (PCL)
			"System.Collections.Concurrent",
			"System.Collections",
			"System.ComponentModel.Annotations",
			"System.ComponentModel.EventBasedAsync",
			"System.ComponentModel",
			"System.Diagnostics.Contracts",
			"System.Diagnostics.Debug",
			"System.Diagnostics.Tools",
			"System.Dynamic.Runtime",
			"System.EnterpriseServices",
			"System.Globalization",
			"System.IO",
			"System.Linq.Expressions",
			"System.Linq.Parallel",
			"System.Linq.Queryable",
			"System.Linq",
			"System.Net.NetworkInformation",
			"System.Net.Primitives",
			"System.Net.Requests",
			"System.ObjectModel",
			"System.Reflection.Extensions",
			"System.Reflection.Primitives",
			"System.Reflection",
			"System.Resources.ResourceManager",
			"System.Runtime.Extensions",
			"System.Runtime.InteropServices",
			"System.Runtime.InteropServices.WindowsRuntime",
			"System.Runtime.Numerics",
			"System.Runtime.Serialization.Json",
			"System.Runtime.Serialization.Primitives",
			"System.Runtime.Serialization.Xml",
			"System.Runtime",
			"System.Security.Principal",
			"System.ServiceModel.Http",
			"System.ServiceModel.Primitives",
			"System.Text.Encoding.Extensions",
			"System.Text.Encoding",
			"System.Text.RegularExpressions",
			"System.Threading.Tasks.Parallel",
			"System.Threading.Tasks",
			"System.Threading",
			"System.Xml.ReaderWriter",
			"System.Xml.XDocument",
			"System.Xml.XmlSerializer",
		};

		protected override bool IsSdk (string assemblyName)
		{
			return Sdk.Contains (assemblyName);
		}
	}
}
