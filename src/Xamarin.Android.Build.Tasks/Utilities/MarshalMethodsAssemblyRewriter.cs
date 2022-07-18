#if ENABLE_MARSHAL_METHODS
using System;
using System.Collections.Generic;
using System.IO;

using Java.Interop.Tools.Cecil;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	class MarshalMethodsAssemblyRewriter
	{
		IDictionary<string, IList<MarshalMethodEntry>> methods;
		ICollection<AssemblyDefinition> uniqueAssemblies;
		IDictionary <string, HashSet<string>> assemblyPaths;
		TaskLoggingHelper log;

		public MarshalMethodsAssemblyRewriter (IDictionary<string, IList<MarshalMethodEntry>> methods, ICollection<AssemblyDefinition> uniqueAssemblies, IDictionary <string, HashSet<string>> assemblyPaths, TaskLoggingHelper log)
		{
			this.methods = methods ?? throw new ArgumentNullException (nameof (methods));
			this.uniqueAssemblies = uniqueAssemblies ?? throw new ArgumentNullException (nameof (uniqueAssemblies));
			this.assemblyPaths = assemblyPaths ?? throw new ArgumentNullException (nameof (assemblyPaths));
			this.log = log ?? throw new ArgumentNullException (nameof (log));
		}

		public void Rewrite (DirectoryAssemblyResolver resolver)
		{
			MethodDefinition unmanagedCallersOnlyAttributeCtor = GetUnmanagedCallersOnlyAttributeConstructor (resolver);
			var unmanagedCallersOnlyAttributes = new Dictionary<AssemblyDefinition, CustomAttribute> ();
			foreach (AssemblyDefinition asm in uniqueAssemblies) {
				unmanagedCallersOnlyAttributes.Add (asm, CreateImportedUnmanagedCallersOnlyAttribute (asm, unmanagedCallersOnlyAttributeCtor));
			}

			Console.WriteLine ("Adding the [UnmanagedCallersOnly] attribute to native callback methods and removing unneeded fields+methods");
			foreach (IList<MarshalMethodEntry> methodList in methods.Values) {
				foreach (MarshalMethodEntry method in methodList) {
					Console.WriteLine ($"\t{method.NativeCallback.FullName} (token: 0x{method.NativeCallback.MetadataToken.RID:x})");
					Console.WriteLine ($"\t  Top type == '{method.DeclaringType}'");
					Console.WriteLine ($"\t  NativeCallback == '{method.NativeCallback}'");
					Console.WriteLine ($"\t  Connector == '{method.Connector}'");
					Console.WriteLine ($"\t  method.NativeCallback.CustomAttributes == {ToStringOrNull (method.NativeCallback?.CustomAttributes)}");
					Console.WriteLine ($"\t  method.Connector.DeclaringType == {ToStringOrNull (method.Connector?.DeclaringType)}");
					Console.WriteLine ($"\t  method.Connector.DeclaringType.Methods == {ToStringOrNull (method.Connector.DeclaringType?.Methods)}");
					Console.WriteLine ($"\t  method.CallbackField == {ToStringOrNull (method.CallbackField)}");
					Console.WriteLine ($"\t  method.CallbackField?.DeclaringType == {ToStringOrNull (method.CallbackField?.DeclaringType)}");
					Console.WriteLine ($"\t  method.CallbackField?.DeclaringType.Fields == {ToStringOrNull (method.CallbackField?.DeclaringType?.Fields)}");
					method.NativeCallback.CustomAttributes.Add (unmanagedCallersOnlyAttributes [method.NativeCallback.Module.Assembly]);
					method.Connector?.DeclaringType?.Methods?.Remove (method.Connector);
					method.CallbackField?.DeclaringType?.Fields?.Remove (method.CallbackField);
				}
			}

			Console.WriteLine ();
			Console.WriteLine ("Rewriting assemblies");

			var newAssemblyPaths = new List<string> ();
			foreach (AssemblyDefinition asm in uniqueAssemblies) {
				foreach (string path in GetAssemblyPaths (asm)) {
					var writerParams = new WriterParameters {
						WriteSymbols = (File.Exists (path + ".mdb") || File.Exists (Path.ChangeExtension (path, ".pdb"))),
					};

					string output = $"{path}.new";
					Console.WriteLine ($"\t{asm.Name} => {output}");
					asm.Write (output, writerParams);
					newAssemblyPaths.Add (output);
				}
			}

			// Replace old versions of the assemblies only after we've finished rewriting without issues, otherwise leave the new
			// versions around.
			foreach (string path in newAssemblyPaths) {
				string target = Path.Combine (Path.GetDirectoryName (path), Path.GetFileNameWithoutExtension (path));
				MoveFile (path, target);

				string source = Path.ChangeExtension (path, ".pdb");
				if (File.Exists (source)) {
					target = Path.ChangeExtension (Path.Combine (Path.GetDirectoryName (source), Path.GetFileNameWithoutExtension (source)), ".pdb");

					MoveFile (source, target);
				}

				source = $"{path}.mdb";
				if (File.Exists (source)) {
					target = Path.ChangeExtension (path, ".mdb");
					MoveFile (source, target);
				}
			}

			Console.WriteLine ();
			Console.WriteLine ("Method tokens:");
			foreach (IList<MarshalMethodEntry> methodList in methods.Values) {
				foreach (MarshalMethodEntry method in methodList) {
					Console.WriteLine ($"\t{method.NativeCallback.FullName} (token: 0x{method.NativeCallback.MetadataToken.RID:x})");
				}
			}

			void MoveFile (string source, string target)
			{
				Console.WriteLine ($"Moving '{source}' => '{target}'");
				Files.CopyIfChanged (source, target);
				try {
					File.Delete (source);
				} catch (Exception) {
					log.LogWarning ($"Unable to delete source file '{source}' when moving it to '{target}'");
				}
			}

			string ToStringOrNull (object? o)
			{
				if (o == null) {
					return "'null'";
				}

				return o.ToString ();
			}
		}

		ICollection<string> GetAssemblyPaths (AssemblyDefinition asm)
		{
			if (!assemblyPaths.TryGetValue (asm.Name.Name, out HashSet<string> paths)) {
				throw new InvalidOperationException ($"Unable to determine file path for assembly '{asm.Name.Name}'");
			}

			return paths;
		}

		MethodDefinition GetUnmanagedCallersOnlyAttributeConstructor (DirectoryAssemblyResolver resolver)
		{
			AssemblyDefinition asm = resolver.Resolve ("System.Runtime.InteropServices");
			TypeDefinition unmanagedCallersOnlyAttribute = null;
			foreach (ModuleDefinition md in asm.Modules) {
				foreach (ExportedType et in md.ExportedTypes) {
					if (!et.IsForwarder) {
						continue;
					}

					if (String.Compare ("System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute", et.FullName, StringComparison.Ordinal) != 0) {
						continue;
					}

					unmanagedCallersOnlyAttribute = et.Resolve ();
					break;
				}
			}

			if (unmanagedCallersOnlyAttribute == null) {
				throw new InvalidOperationException ("Unable to find the System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute type");
			}

			foreach (MethodDefinition md in unmanagedCallersOnlyAttribute.Methods) {
				if (!md.IsConstructor) {
					continue;
				}

				return md;
			}

			throw new InvalidOperationException ("Unable to find the System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute type constructor");
		}

		CustomAttribute CreateImportedUnmanagedCallersOnlyAttribute (AssemblyDefinition targetAssembly, MethodDefinition unmanagedCallersOnlyAtributeCtor)
		{
			return new CustomAttribute (targetAssembly.MainModule.ImportReference (unmanagedCallersOnlyAtributeCtor));
		}
	}
}
#endif
