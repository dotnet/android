//
// LinkContext.cs
//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// (C) 2006 Jb Evain
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Java.Interop.Tools.Cecil;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mono.Linker {

	public class LinkContext : TypeDefinitionCache, IDisposable {

		Dictionary<string, AssemblyAction> _actions;
		string _outputDirectory;

		DirectoryAssemblyResolver _resolver;
		ReaderParameters _readerParameters;
		ISymbolReaderProvider _symbolReaderProvider;
		ISymbolWriterProvider _symbolWriterProvider;
		AnnotationStore _annotations;

		public AnnotationStore Annotations {
			get { return _annotations; }
		}

		public string OutputDirectory {
			get { return _outputDirectory; }
			set { _outputDirectory = value; }
		}

		public System.Collections.IDictionary Actions {
			get { return _actions; }
		}

		public DirectoryAssemblyResolver Resolver {
			get { return _resolver; }
		}

		public ReaderParameters ReaderParameters {
			get { return _readerParameters; }
		}

		public ISymbolReaderProvider SymbolReaderProvider {
			get { return _symbolReaderProvider; }
			set { _symbolReaderProvider = value; }
		}

		public ISymbolWriterProvider SymbolWriterProvider {
			get { return _symbolWriterProvider; }
			set { _symbolWriterProvider = value; }
		}

		public Tracer Tracer { get; private set; }

		public LinkContext (DirectoryAssemblyResolver resolver)
			: this(resolver, new ReaderParameters
			{
				AssemblyResolver = resolver
			})
		{
		}

		public LinkContext (DirectoryAssemblyResolver resolver, ReaderParameters readerParameters)
		{
			_resolver = resolver;
			_actions = new Dictionary<string, AssemblyAction> ();
			SymbolReaderProvider = new DefaultSymbolReaderProvider (false);
			_annotations = new AnnotationStore (this);
			Tracer = new Tracer (this);
		}

		public AssemblyDefinition Resolve (string name)
		{
			if (File.Exists (name)) {
				try {
					return AssemblyDefinition.ReadAssembly (name, _readerParameters);
				} catch (Exception e) {
					throw new AssemblyResolutionException (new AssemblyNameReference (name, new Version ()), e);
				}
			}

			return Resolve (new AssemblyNameReference (name, new Version ()));
		}

		public AssemblyDefinition Resolve (IMetadataScope scope)
		{
			AssemblyNameReference reference = GetReference (scope);
			try {
				AssemblyDefinition assembly = _resolver.Resolve (reference, _readerParameters);

				if (assembly != null)
					RegisterAssembly (assembly);

				return assembly;
			}
			catch (Exception e) {
				throw new AssemblyResolutionException (reference, e);
			}
		}

		public void RegisterAssembly (AssemblyDefinition assembly)
		{
			if (SeenFirstTime (assembly)) {
				SafeReadSymbols (assembly);
				SetDefaultAction (assembly);
			}
		}

		protected bool SeenFirstTime (AssemblyDefinition assembly)
		{
			return !_annotations.HasAction (assembly);
		}

		public virtual void SafeReadSymbols (AssemblyDefinition assembly)
		{
			if (assembly.MainModule.HasSymbols)
				return;

			if (_symbolReaderProvider == null)
				throw new ArgumentNullException (nameof (_symbolReaderProvider));

			try {
				var symbolReader = _symbolReaderProvider.GetSymbolReader (
					assembly.MainModule,
					assembly.MainModule.FileName);

				if (symbolReader == null)
					return;

				try {
					assembly.MainModule.ReadSymbols (symbolReader);
				} catch {
					symbolReader.Dispose ();
					return;
				}

				// Add symbol reader to annotations only if we have successfully read it
				_annotations.AddSymbolReader (assembly, symbolReader);
			} catch { }
		}

		public virtual ICollection<AssemblyDefinition> ResolveReferences (AssemblyDefinition assembly)
		{
			List<AssemblyDefinition> references = new List<AssemblyDefinition> ();
			if (assembly == null)
				return references;
			foreach (AssemblyNameReference reference in assembly.MainModule.AssemblyReferences) {
				AssemblyDefinition definition = Resolve (reference);
				if (definition != null)
					references.Add (definition);
			}
			return references;
		}

		static AssemblyNameReference GetReference (IMetadataScope scope)
		{
			AssemblyNameReference reference;
			if (scope is ModuleDefinition) {
				AssemblyDefinition asm = ((ModuleDefinition) scope).Assembly;
				reference = asm.Name;
			} else
				reference = (AssemblyNameReference) scope;

			return reference;
		}
		
		public void SetAction (AssemblyDefinition assembly, AssemblyAction defaultAction)
		{
			RegisterAssembly (assembly);

			if (!_actions.TryGetValue (assembly.Name.Name, out AssemblyAction action))
				action = defaultAction;

			Annotations.SetAction (assembly, action);
		}

		protected void SetDefaultAction (AssemblyDefinition assembly)
		{
			AssemblyNameDefinition name = assembly.Name;
			if (!_actions.TryGetValue (name.Name, out var action)) {
				action = default;
			}

			_annotations.SetAction (assembly, action);
		}

		public virtual AssemblyDefinition [] GetAssemblies ()
		{
			return _resolver.ToResolverCache ().Values.ToArray ();
		}

		public void Dispose ()
		{
			_resolver.Dispose ();
		}

		// NOTE: methods below are no-op and implemented in MSBuildLinkContext
		public virtual void LogMessage (string message) { }
		
		public virtual void LogWarning (string code, string message) { }

		public virtual void LogError (string code, string message) { }
	}
}
