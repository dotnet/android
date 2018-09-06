using System.Collections.Generic;

namespace Xamarin.Android.Tasks
{
	public partial class Profile
	{
		public static readonly HashSet<string> Sdk = new HashSet<string> {
			"mscorlib",
			"System",
			"System.Core",
			"System.Data",
			"System.EnterpriseServices",
			"System.Net.Http",
			"System.Runtime.Serialization",
			"System.ServiceModel",
			"System.ServiceModel.Web",
			"System.Transactions",
			"System.Web.Services",
			"System.Xml",
			"System.Xml.Linq",
			"System.Json",
			"System.Numerics",
			"Microsoft.CSharp",
			"Mono.CSharp",
			"Mono.CompilerServices.SymbolWriter",
			"Mono.Security",
			"Mono.Data.Tds",
			"Mono.Data.Sqlite",
		};

		// KEEP THIS SORTED ALPHABETICALLY, CASE-INSENSITIVE
		public static readonly string[] ValidAbis = new[]{
			"arm64-v8a",
			"armeabi-v7a",
			"x86",
			"x86_64",
		};

		public static readonly string[] ValidProfilers = new[]{
			"log",
		};
	}
}

