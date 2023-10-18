using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Java.Interop.Tools.Cecil;
using Mono.Cecil;

namespace Xamarin.Android.Tasks
{
	public class CheckClientHandlerType : AndroidTask
	{
		public override string TaskPrefix => "CCHT";

		[Required]
		public string ClientHandlerType { get; set; }

		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; }

		public override bool RunTask ()
		{
			string[] types = ClientHandlerType.Split (',');
			string type = types[0].Trim ();
			string assembly = "Mono.Android";

			if (types.Length > 1) {
				assembly = types[1].Trim ();
			}
			// load the assembly.
			ITaskItem foundAssembly = null;
			foreach (var asm in ResolvedAssemblies) {
				string filename = Path.GetFileNameWithoutExtension (asm.ItemSpec);
				if (string.CompareOrdinal (assembly, filename) == 0) {
					foundAssembly = asm;
					break;
				}
			}
			if (foundAssembly == null) {
				Log.LogCodedError ("XA1033", Xamarin.Android.Tasks.Properties.Resources.XA1033, assembly);
				return !Log.HasLoggedErrors;
			}
			// find the type.
			var readerParameters = new ReaderParameters {
				ReadSymbols = false,
			};
			using (var resolver = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: false, loadReaderParameters: readerParameters)) {
				foreach (var asm in ResolvedAssemblies) {
					var path = Path.GetFullPath (Path.GetDirectoryName (asm.ItemSpec));
					if (!resolver.SearchDirectories.Contains (path)) {
						resolver.SearchDirectories.Add (path);
					}
				}

				var assemblyDefinition = resolver.GetAssembly (Path.GetFullPath (foundAssembly.ItemSpec));
				TypeDefinition handlerType = null;
				foreach (var model in assemblyDefinition.Modules) {
					handlerType = assemblyDefinition.MainModule.GetType (type);
					if (handlerType != null)
						break;
				}
				if (handlerType == null) {
					Log.LogCodedError ("XA1032", Xamarin.Android.Tasks.Properties.Resources.XA1032, type, assembly);
					return false;
				}

				if (Extends (handlerType, "System.Net.Http.HttpClientHandler")) {
					Log.LogCodedError ("XA1031", Xamarin.Android.Tasks.Properties.Resources.XA1031_HCH, type);
				}

				if (!Extends (handlerType, "System.Net.Http.HttpMessageHandler")) {
					Log.LogCodedError ("XA1031", Xamarin.Android.Tasks.Properties.Resources.XA1031, type, "System.Net.Http.HttpMessageHandler");
				}

				return !Log.HasLoggedErrors;
			}
		}

		static bool Extends (TypeDefinition type, string validBase) {
			var bt = type.Resolve ();
			while (bt != null) {
				if (bt.FullName == validBase)
					return true;
				bt = bt.BaseType?.Resolve () ?? null;
			}
			return false;
		}
	}
}
