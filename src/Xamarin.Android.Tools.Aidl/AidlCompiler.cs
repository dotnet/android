using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Irony.Parsing;
using Mono.Cecil;

namespace Xamarin.Android.Tools.Aidl
{
	public class AidlCompiler
	{
		public class Result
		{
			public Result ()
			{
				LogMessages = new Dictionary<string,Irony.LogMessage> ();
			}
			
			public IDictionary<string,Irony.LogMessage> LogMessages { get; private set; }
		}
	
		Dictionary<string,CompilationUnit> units = new Dictionary<string,CompilationUnit> ();
		string output_path;
		string output_ns;
		
		public event Action<string,string> FileWritten;
		
		public Result Run (ConverterOptions opts, Func<string,AssemblyDefinition> resolveAssembly)
		{
			return Run (opts, resolveAssembly, ((path, sourceFile) => {
				var file = Path.GetFileNameWithoutExtension (sourceFile);
				file = Path.Combine (output_path, /*file [0] == 'I' ? file : 'I' +*/ file) + ".cs";
				return file;
			}));
		}

		public Result Run (ConverterOptions opts, Func<string,AssemblyDefinition> resolveAssembly, Func<string,string,string> outputFilenameSelector)
		{
			var result = new Result ();
			var database = new BindingDatabase (opts.References, resolveAssembly);
			var lang = new LanguageData (new AidlGrammar () { LanguageFlags = LanguageFlags.Default | LanguageFlags.CreateAst });
			var parser = new Parser (lang);
			output_path = opts.OutputDirectory;
			output_ns = opts.OutputNS;
			foreach (var file in opts.InputFiles) {
				var pt = parser.Parse (File.ReadAllText (file), file);
				if (pt.HasErrors ())
					foreach (var l in pt.ParserMessages)
						result.LogMessages.Add (file, l);
				else
					units.Add (file, (CompilationUnit) pt.Root.AstNode);
			}
			if (result.LogMessages.Any (e => e.Value.Level == Irony.ErrorLevel.Error))
				return result;
			
			if (output_path != null && !Directory.Exists (output_path))
				Directory.CreateDirectory (output_path);
			output_path = output_path ?? "./";
			
			var parcelables = new List<TypeName> ();
			foreach (var u in units.Values)
				foreach (Parcelable t in u.Types.Where (t => t is Parcelable))
					parcelables.Add (u.Package == null ? t.Name : new TypeName (u.Package.Identifiers.Concat (t.Name.Identifiers).ToArray ()));
			
			foreach (var pair in units) {
				string file = outputFilenameSelector (output_path, pair.Key);
				var sw = new StringWriter ();
				new CSharpCodeGenerator (sw, database).GenerateCode (pair.Value, parcelables, opts);
				string csharp = sw.ToString ();
				if (pair.Value.Package != null && output_ns != null)
					csharp = csharp.Replace (pair.Value.Package.ToString (), output_ns);
				using (var fw = File.CreateText (file))
					fw.Write (csharp);
				FileWritten (file, csharp);
			}

			return result;
		}
	}
}

