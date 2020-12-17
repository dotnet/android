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
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Java.Interop.Tools.Diagnostics;

using Mono.Cecil;

namespace Java.Interop.Tools.Cecil {

	public static class AssemblyResolverCoda {

		public static AssemblyDefinition GetAssembly (this IAssemblyResolver resolver, string fileName)
		{
			return resolver.Resolve (AssemblyNameReference.Parse (Path.GetFileNameWithoutExtension (fileName)));
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
	public class DirectoryAssemblyResolver : IAssemblyResolver {

		public ICollection<string> SearchDirectories {get; private set;}

		Dictionary<string, AssemblyDefinition?> cache;
		bool loadDebugSymbols;
		Action<TraceLevel, string>              logger;

		ReaderParameters                        loadReaderParameters;

		static  readonly    ReaderParameters    DefaultLoadReaderParameters = new ReaderParameters {
		};

		[Obsolete ("Use DirectoryAssemblyResolver(Action<TraceLevel, string>, bool, ReaderParameters)")]
		public DirectoryAssemblyResolver (Action<string, object[]> logWarnings, bool loadDebugSymbols, ReaderParameters? loadReaderParameters = null)
			: this ((TraceLevel level, string value) => logWarnings?.Invoke ("{0}", new[]{value}), loadDebugSymbols, loadReaderParameters)
		{
			if (logWarnings == null)
				throw new ArgumentNullException (nameof (logWarnings));
		}

		public DirectoryAssemblyResolver (Action<TraceLevel, string> logger, bool loadDebugSymbols, ReaderParameters? loadReaderParameters = null)
		{
			if (logger == null)
				throw new ArgumentNullException (nameof (logger));
			cache = new Dictionary<string, AssemblyDefinition?> ();
			this.loadDebugSymbols = loadDebugSymbols;
			this.logger       = logger;
			SearchDirectories = new List<string> ();
			this.loadReaderParameters = loadReaderParameters ?? DefaultLoadReaderParameters;
		}

		public void Dispose ()
		{
			Dispose (disposing: true);
			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose (bool disposing)
		{
			if (!disposing || cache == null)
				return;
			foreach (var e in cache) {
				e.Value?.Dispose ();
			}
			cache.Clear ();
		}

		public Dictionary<string, AssemblyDefinition?> ToResolverCache ()
		{
			return new Dictionary<string, AssemblyDefinition?>(cache);
		}

		public bool AddToCache (AssemblyDefinition assembly)
		{
			var name = Path.GetFileNameWithoutExtension (assembly.MainModule.FileName);

			if (cache.ContainsKey (name))
				return false;

			cache [name] = assembly;

			return true;
		}

		public virtual AssemblyDefinition? Load (string fileName, bool forceLoad = false)
		{
			if (!File.Exists (fileName))
				return null;

			AssemblyDefinition? assembly = null;
			var name = Path.GetFileNameWithoutExtension (fileName);
			if (!forceLoad && cache.TryGetValue (name, out assembly))
				return assembly;

			try {
				assembly  = ReadAssembly (fileName);
			} catch (Exception e) {
				Diagnostic.Error (9, e, Localization.Resources.CecilResolver_XA0009, fileName);
			}
			cache [name] = assembly;
			return assembly;
		}

		protected virtual AssemblyDefinition ReadAssembly (string file)
		{
			bool haveDebugSymbols = loadDebugSymbols &&
				(File.Exists (file + ".mdb") ||
				 File.Exists (Path.ChangeExtension (file, ".pdb")));
			var reader_parameters = new ReaderParameters () {
				ApplyWindowsRuntimeProjections  = loadReaderParameters.ApplyWindowsRuntimeProjections,
				AssemblyResolver                = this,
				MetadataImporterProvider        = loadReaderParameters.MetadataImporterProvider,
				InMemory                        = loadReaderParameters.InMemory,
				MetadataResolver                = loadReaderParameters.MetadataResolver,
				ReadingMode                     = loadReaderParameters.ReadingMode,
				ReadSymbols                     = haveDebugSymbols,
				ReadWrite                       = loadReaderParameters.ReadWrite,
				ReflectionImporterProvider      = loadReaderParameters.ReflectionImporterProvider,
				SymbolReaderProvider            = loadReaderParameters.SymbolReaderProvider,
				SymbolStream                    = loadReaderParameters.SymbolStream,
			};
			try {
				return AssemblyDefinition.ReadAssembly (file, reader_parameters);
			} catch (Exception ex) {
				logger (
						TraceLevel.Verbose,
						$"Failed to read '{file}' with debugging symbols. Retrying to load it without it. Error details are logged below.");
				logger (TraceLevel.Verbose, $"{ex.ToString ()}");
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

		public AssemblyDefinition Resolve (string fullName, ReaderParameters? parameters)
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

			string? assembly;
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

		public AssemblyDefinition Resolve (AssemblyNameReference reference, ReaderParameters? parameters)
		{
			var name = reference.Name;

			AssemblyDefinition? assembly;
			if (cache.TryGetValue (name, out assembly)) {
				if (assembly is null)
					throw CreateLoadException (reference);

				return assembly;
			}

			string? assemblyFile;
			AssemblyDefinition? candidate = null;
			foreach (var dir in SearchDirectories) {
				if ((assemblyFile = SearchDirectory (name, dir)) != null) {
					var loaded = Load (assemblyFile);
					if (Array.Equals (loaded?.Name.MetadataToken, reference.MetadataToken))
						return loaded;
					candidate = candidate ?? loaded;
				}
			}
			// signature mismatch, but return it as it used to do.
			if (candidate != null)
				return candidate;

			throw CreateLoadException (reference);
		}

		static FileNotFoundException CreateLoadException (AssemblyNameReference reference)
		{
			return new System.IO.FileNotFoundException (
					string.Format ("Could not load assembly '{0}, Version={1}, Culture={2}, PublicKeyToken={3}'. Perhaps it doesn't exist in the Mono for Android profile?",
						reference.Name,
						reference.Version,
						string.IsNullOrEmpty (reference.Culture) ? "neutral" : reference.Culture,
						reference.PublicKeyToken == null
						? "null"
						: string.Join ("", reference.PublicKeyToken.Select (b => b.ToString ("x2")))),
					reference.Name + ".dll");
		}

		string? SearchDirectory (string name, string directory)
		{
			if (Path.IsPathRooted (name) && File.Exists (name))
				return name;

			var file = Path.Combine (directory, name + ".dll");
			if (File.Exists (file))
				return file;

			file = Path.Combine (directory, name + ".exe");
			if (File.Exists (file))
				return file;

			return null;
		}
	}
}
