//
// AssemblyResolver.cs
//
// Author:
//   Jb Evain (jbevain@novell.com)
//   Jonathan Pryor (jpryor@novell.com)
//
// (C) 2010 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Mono.Cecil;

namespace Xamarin.Android.Tuner {

	static class AssemblyResolverCoda {

		public static AssemblyDefinition GetAssembly (this IAssemblyResolver resolver, string fileName)
		{
			return resolver.Resolve (Path.GetFileNameWithoutExtension (fileName));
		}
	}

	/*
	 * IAssemblyResolver which looks for assembly references within
	 * DirectoryAssemblyResolver.SearchDirectories, in SearchDirectories order.
	 *
	 * Can't use Mono.Cecil.BaseAssemblyResolver as that special-cases
	 * mscorlib.dll and tries a directory based on
	 * `typeof (object).Module.FullyQualifiedName`, which will never be valid.
	 */
	class DirectoryAssemblyResolver : IAssemblyResolver {

		public ICollection<string> SearchDirectories {get; private set;}
		
		Dictionary<string, AssemblyDefinition> cache;
		bool loadDebugSymbols;
		Action<string> logWarnings;

		public DirectoryAssemblyResolver (Action<string> logWarnings, bool loadDebugSymbols)
		{
			if (logWarnings == null)
				throw new ArgumentNullException (nameof (logWarnings));
			cache = new Dictionary<string, AssemblyDefinition> ();
			this.loadDebugSymbols = loadDebugSymbols;
			this.logWarnings = logWarnings;
			SearchDirectories = new List<string> ();
		}

		public IDictionary ToResolverCache ()
		{
			var resolver_cache = new Hashtable ();
			foreach (var pair in cache)
				resolver_cache.Add (pair.Key, pair.Value);

			return resolver_cache;
		}

		public virtual AssemblyDefinition Load (string fileName)
		{
			if (!File.Exists (fileName))
				return null;

			AssemblyDefinition assembly;
			var name = Path.GetFileNameWithoutExtension (fileName);
			if (cache.TryGetValue (name, out assembly))
				return assembly;

			try {
				assembly  = ReadAssembly (fileName);
			} catch (Exception e) {
				Diagnostic.Error (9, e, "Error while loading assembly: {0}", fileName);
			}
			cache.Add (name, assembly);
			return assembly;
		}

		protected virtual AssemblyDefinition ReadAssembly (string file)
		{
			var reader_parameters = new ReaderParameters () {
				AssemblyResolver  = this,
				ReadSymbols       = loadDebugSymbols && (File.Exists(file + ".mdb") || File.Exists(Path.ChangeExtension(file, ".pdb"))),
			};
			try {
				return AssemblyDefinition.ReadAssembly (file, reader_parameters);
			} catch (Exception ex) {
				logWarnings (string.Format ("Failed to read '{0}' with debugging symbols. Retrying to load it without it. Error details are logged below.", file));
				logWarnings (ex.ToString ());
				reader_parameters.ReadSymbols = false;
				return AssemblyDefinition.ReadAssembly (file, reader_parameters);
			}
		}

		public AssemblyDefinition GetAssembly (string fileName)
		{
			return Resolve (Path.GetFileNameWithoutExtension (fileName));
		}

		public AssemblyDefinition Resolve (string fullName)
		{
			return Resolve (fullName, null);
		}
		
		public AssemblyDefinition Resolve (string fullName, ReaderParameters parameters)
		{
			return Resolve (AssemblyNameReference.Parse (fullName), parameters);
		}

		public AssemblyDefinition Resolve (AssemblyNameReference reference)
		{
			return Resolve (reference, null);
		}
		
		public string FindAssemblyFile (string fullName)
		{
			return FindAssemblyFile (AssemblyNameReference.Parse (fullName));
		}

		public string FindAssemblyFile (AssemblyNameReference reference)
		{
			var name = reference.Name;

			string assembly;
			foreach (var dir in SearchDirectories)
				if ((assembly = SearchDirectory (name, dir)) != null)
					return assembly;
			
			throw new System.IO.FileNotFoundException (
				string.Format ("Could not load assembly '{0}, Version={1}, Culture={2}, PublicKeyToken={3}'. Perhaps it doesn't exist in the Mono for Android profile?",
			               name, 
			               reference.Version, 
			               string.IsNullOrEmpty (reference.Culture) ? "neutral" : reference.Culture, 
			               reference.PublicKeyToken == null
			               ? "null"
			               : string.Join ("", reference.PublicKeyToken.Select(b => b.ToString ("x2")))),
				name + ".dll");
		}

		public AssemblyDefinition Resolve (AssemblyNameReference reference, ReaderParameters parameters)
		{
			var name = reference.Name;

			AssemblyDefinition assembly;
			if (cache.TryGetValue (name, out assembly))
				return assembly;

			string assemblyFile;
			AssemblyDefinition candidate = null;
			foreach (var dir in SearchDirectories) {
				if ((assemblyFile = SearchDirectory (name, dir)) != null) {
					var loaded = Load (assemblyFile);
					if (Array.Equals (loaded.Name.MetadataToken, reference.MetadataToken))
						return loaded;
					candidate = candidate ?? loaded;
				}
			}
			// signature mismatch, but return it as it used to do.
			if (candidate != null)
				return candidate;

			throw new System.IO.FileNotFoundException (
					string.Format ("Could not load assembly '{0}, Version={1}, Culture={2}, PublicKeyToken={3}'. Perhaps it doesn't exist in the Mono for Android profile?",
						name, 
						reference.Version, 
						string.IsNullOrEmpty (reference.Culture) ? "neutral" : reference.Culture, 
						reference.PublicKeyToken == null
						? "null"
						: string.Join ("", reference.PublicKeyToken.Select(b => b.ToString ("x2")))),
					name + ".dll");
		}

		string SearchDirectory (string name, string directory)
		{
			if (Path.IsPathRooted (name) && File.Exists (name))
				return name;

			var file = DirectoryGetFile (directory, name + ".dll");
			if (file.Length > 0)
				return file;

			file = DirectoryGetFile (directory, name + ".exe");
			if (file.Length > 0)
				return file;

			return null;
		}

		static string DirectoryGetFile (string directory, string file)
		{
			if (!Directory.Exists (directory))
				return "";

			var files = Directory.GetFiles (directory, file);
			if (files != null && files.Length > 0)
				return files [0];

			return "";
		}
	}
}
