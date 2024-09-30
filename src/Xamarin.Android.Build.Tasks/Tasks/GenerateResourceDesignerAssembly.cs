// Copyright (C) 2011 Xamarin, Inc. All rights reserved.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Java.Interop.Tools.Cecil;
using Brutal.Dev.StrongNameSigner;
using MonoDroid.Tuner;

namespace Xamarin.Android.Tasks
{
	public class GenerateResourceDesignerAssembly : AndroidTask
	{
		public override string TaskPrefix => "GRDA";

		[Required]
		public ITaskItem RTxtFile { get; set; }

		public ITaskItem ResourceMap { get; set; }

		[Required]
		public bool IsApplication { get; set; }

		[Required]
		public bool DesignTimeBuild { get; set; }

		[Required]
		public ITaskItem OutputFile { get; set; }

		[Required]
		public string TargetFrameworkVersion { get; set; }

		[Required]
		public string TargetFrameworkIdentifier { get; set; }

		[Required]
		public string ProjectDir { get; set; }

		[Required]
		public ITaskItem[] Resources { get; set; }

		[Required]
		public string ResourceDirectory { get; set; }
		public string CaseMapFile { get; set; }
		public ITaskItem[] AdditionalResourceDirectories { get; set; }
		public ITaskItem[] FrameworkDirectories { get; set; }
		public bool Deterministic { get; set; }
		public string AssemblyName { get; set; }
		TypeReference intArray;
		TypeReference intRef;
		TypeReference objectRef;
		Dictionary<string, string> resource_fixup = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);

		public override bool RunTask ()
		{
			using (var res = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: false)) {
				Run(res);
			}
			return !Log.HasLoggedErrors;
		}

		bool Run (DirectoryAssemblyResolver res)
		{
			var cache = new TypeDefinitionCache ();

			foreach (var dir in FrameworkDirectories) {
				if (Directory.Exists (dir.ItemSpec))
					res.SearchDirectories.Add (dir.ItemSpec);
			}
			// ResourceDirectory may be a relative path, and
			// we need to compare it to absolute paths
			ResourceDirectory = Path.GetFullPath (ResourceDirectory);

			string assemblyName = Path.GetFileNameWithoutExtension (OutputFile.ItemSpec);

			resource_fixup = MonoAndroidHelper.LoadMapFile (BuildEngine4, Path.GetFullPath (CaseMapFile), StringComparer.OrdinalIgnoreCase);
			// Generate an assembly which contains all the values in the provided
			// R.txt file.
			var mp = new ModuleParameters ();
			mp.AssemblyResolver = res;
			mp.Kind = ModuleKind.Dll;
			var assembly = AssemblyDefinition.CreateAssembly (
				new AssemblyNameDefinition (assemblyName, new Version (1, 0)),
				assemblyName,
				mp);

			var module = assembly.MainModule;

			module.AssemblyReferences.Clear ();
			var netstandardAsm = AssemblyNameReference.Parse ("netstandard, Version=2.1.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
			module.AssemblyReferences.Add(netstandardAsm);
			var netstandardDef = module.AssemblyResolver.Resolve(netstandardAsm);

			if (!IsApplication) {
				MethodReference referenceAssemblyConstructor = ImportCustomAttributeConstructor (cache, "System.Runtime.CompilerServices.ReferenceAssemblyAttribute", module, netstandardDef.MainModule);
				module.Assembly.CustomAttributes.Add (new CustomAttribute (referenceAssemblyConstructor));
			} else {
				// Add the InternalsVisibleToAttribute so the app can access ResourceConstant
				if (!string.IsNullOrEmpty (AssemblyName)) {
					MethodReference internalsVisibleToAttributeConstructor = ImportCustomAttributeConstructor (cache, "System.Runtime.CompilerServices.InternalsVisibleToAttribute", module, netstandardDef.MainModule, argCount: 1);
					var ar = new CustomAttribute (internalsVisibleToAttributeConstructor);
					ar.ConstructorArguments.Add (new CustomAttributeArgument (module.TypeSystem.String, AssemblyName));
					module.Assembly.CustomAttributes.Add (ar);
				}
			}

			MethodReference targetFrameworkConstructor = ImportCustomAttributeConstructor (cache, "System.Runtime.Versioning.TargetFrameworkAttribute", module, netstandardDef.MainModule, argCount: 1);

			var attr = new CustomAttribute (targetFrameworkConstructor);
			attr.ConstructorArguments.Add (new CustomAttributeArgument (module.TypeSystem.String, $".NETStandard,Version=v2.1"));
			attr.Properties.Add (new CustomAttributeNamedArgument ("FrameworkDisplayName", new CustomAttributeArgument (module.TypeSystem.String, "")));
			module.Assembly.CustomAttributes.Add (attr);

			MethodReference editorBrowserConstructor = ImportCustomAttributeConstructor (cache, "System.ComponentModel.EditorBrowsableAttribute", module, netstandardDef.MainModule, argCount: 1);
			TypeReference e = ImportType ("System.ComponentModel.EditorBrowsableState", module, netstandardDef.MainModule);
			var editorBrowserAttr = new CustomAttribute (editorBrowserConstructor);
			editorBrowserAttr.ConstructorArguments.Add (new CustomAttributeArgument (e, System.ComponentModel.EditorBrowsableState.Never));
	
			MethodReference generatedCodeConstructor = ImportCustomAttributeConstructor (cache, "System.CodeDom.Compiler.GeneratedCodeAttribute", module, netstandardDef.MainModule, argCount: 2);
			var generatedCodeAttr = new CustomAttribute (generatedCodeConstructor);
			generatedCodeAttr.ConstructorArguments.Add (new CustomAttributeArgument (module.TypeSystem.String, nameof(GenerateResourceDesignerAssembly)));
			var version = typeof(GenerateResourceDesignerAssembly).Assembly.GetName().Version;
			generatedCodeAttr.ConstructorArguments.Add (new CustomAttributeArgument (module.TypeSystem.String, version.ToString ()));

			var att = TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.Public | TypeAttributes.BeforeFieldInit;

			intArray = new ArrayType (module.TypeSystem.Int32);
			intRef = module.TypeSystem.Int32;
			objectRef = module.TypeSystem.Object;

			// The Property Based class.
			var resourceDesigner = new TypeDefinition (
				FixLegacyResourceDesignerStep.DesignerAssemblyNamespace,
				"Resource",
				att,
				objectRef
			);
			CreateCtor (cache, resourceDesigner, module);
			resourceDesigner.CustomAttributes.Add (editorBrowserAttr);
			resourceDesigner.CustomAttributes.Add (generatedCodeAttr);
			module.Types.Add (resourceDesigner);
			TypeDefinition constDesigner = null;
			if (IsApplication) {
				// The Constant based class
				TypeAttributes attrib = string.IsNullOrEmpty (AssemblyName) ? TypeAttributes.Public : TypeAttributes.Public;
				constDesigner = new TypeDefinition (
					FixLegacyResourceDesignerStep.DesignerAssemblyNamespace,
					"ResourceConstant",
					attrib | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit,
					objectRef
				);
				CreateCtor (cache, constDesigner, module);
				constDesigner.CustomAttributes.Add (editorBrowserAttr);
				constDesigner.CustomAttributes.Add (generatedCodeAttr);
				module.Types.Add (constDesigner);
			}

			DateTime lastWriteTimeUtc = DateTime.MinValue;
			if (File.Exists (OutputFile.ItemSpec))
				lastWriteTimeUtc = File.GetLastWriteTimeUtc (OutputFile.ItemSpec);

			if (File.Exists (RTxtFile.ItemSpec)) {
				if (File.GetLastWriteTimeUtc (RTxtFile.ItemSpec) < lastWriteTimeUtc) {
					Log.LogDebugMessage ($"{RTxtFile.ItemSpec} has not changed since {OutputFile.ItemSpec} was generated.");
					return !Log.HasLoggedErrors;
				}
				var parser = new RtxtParser ();
				var resources = parser.Parse (RTxtFile.ItemSpec, Log, resource_fixup);
				foreach (var r in resources) {
					switch (r.Type) {
						case RType.Integer:
							if (IsApplication)
								CreateIntField (cache, r.ResourceTypeName, r.Identifier, r.Id, constDesigner, module);
							CreateIntProperty (cache, r.ResourceTypeName, r.Identifier, r.Id, resourceDesigner, module);
							break;
						case RType.Array:
							if (IsApplication)
								CreateIntArrayField (cache, r.ResourceTypeName, r.Identifier, r.Ids, constDesigner, module);
							CreateIntArrayProperty (cache, r.ResourceTypeName, r.Identifier, r.Ids, resourceDesigner, module);
							break;
					}
				}
			}
			// Add a return to each of the static constructor
			foreach(var c in staticConstructors) {
				var il = c.Value.Body.GetILProcessor ();
				il.Emit(OpCodes.Ret);
			}
			StrongNameAssembly (assembly.Name);
			var wp = new WriterParameters () {
				DeterministicMvid = Deterministic,
			};
			var s = MemoryStreamPool.Shared.Rent ();
			try {
				assembly.Write (s, wp);
				s.Position = 0;
				if (Files.CopyIfStreamChanged (s, OutputFile.ItemSpec)) {
					Log.LogDebugMessage ($"Updated '{OutputFile.ItemSpec}'.");
				} else {
					Log.LogDebugMessage ($"'{OutputFile.ItemSpec}' was up to date.");
				}
			} finally {
				MemoryStreamPool.Shared.Return (s);
			}
			return !Log.HasLoggedErrors;
		}

		MethodReference ImportCustomAttributeConstructor (TypeDefinitionCache cache, string type, ModuleDefinition module, ModuleDefinition sourceModule = null, int argCount = 0)
		{
			var tr = module.ImportReference ((sourceModule ?? module).ExportedTypes.First(x => x.FullName == type).Resolve ());
			var tv = cache.Resolve (tr);
			return module.ImportReference (tv.Methods.First(x => x.IsConstructor && (x.Parameters?.Count ?? 0) == argCount));
		}

		TypeReference ImportType (string type, ModuleDefinition module, ModuleDefinition sourceModule = null)
		{
			return module.ImportReference ((sourceModule ?? module).ExportedTypes.First(x => x.FullName == type).Resolve ());
		}

		void CreateIntProperty (TypeDefinitionCache cache, string resourceClass, string propertyName, int value, TypeDefinition resourceDesigner, ModuleDefinition module,
			MethodAttributes attributes = MethodAttributes.Public, TypeAttributes typeAttributes = TypeAttributes.NestedPublic)
		{
			TypeDefinition nestedType = CreateResourceClass (cache, resourceDesigner, resourceClass, module, typeAttributes);
			PropertyDefinition p = CreateProperty (propertyName, value, module, attributes);
			nestedType.Properties.Add (p);
			nestedType.Methods.Insert (Math.Max(0, nestedType.Methods.Count () - 1), p.GetMethod);
		}

		void CreateIntField (TypeDefinitionCache cache, string resourceClass, string fieldName, int value, TypeDefinition resourceDesigner, ModuleDefinition module,
			FieldAttributes attributes = FieldAttributes.Public, TypeAttributes typeAttributes = TypeAttributes.NestedPublic)
		{
			TypeDefinition nestedType = CreateResourceClass (cache, resourceDesigner, resourceClass, module, typeAttributes);
			FieldDefinition p = CreateField (fieldName, value, module, attributes);
			nestedType.Fields.Add (p);
		}

		void CreateIntArrayProperty (TypeDefinitionCache cache, string resourceClass, string propertyName, int[] values, TypeDefinition resourceDesigner, ModuleDefinition module,
			MethodAttributes attributes = MethodAttributes.Public, TypeAttributes typeAttributes = TypeAttributes.NestedPublic)
		{
			TypeDefinition nestedType = CreateResourceClass (cache, resourceDesigner, resourceClass, module, typeAttributes);
			PropertyDefinition p = CreateArrayProperty (propertyName, values, module, attributes);
			nestedType.Properties.Add (p);
			nestedType.Methods.Insert (Math.Max(0, nestedType.Methods.Count () - 1), p.GetMethod);
		}

		void CreateIntArrayField (TypeDefinitionCache cache, string resourceClass, string fieldName, int[] values, TypeDefinition resourceDesigner, ModuleDefinition module,
			FieldAttributes attributes = FieldAttributes.Public, TypeAttributes typeAttributes = TypeAttributes.NestedPublic)
		{
			TypeDefinition nestedType = CreateResourceClass (cache, resourceDesigner, resourceClass, module, typeAttributes);
			FieldDefinition p = CreateArrayField (fieldName, values, module, attributes);
			nestedType.Fields.Add (p);
			MethodDefinition ctor = GetOrCreateStaticCtor (nestedType, module);
			ILProcessor il = ctor.Body.GetILProcessor ();
			il.Emit (OpCodes.Ldc_I4, values.Length); // store array size
			il.Emit (OpCodes.Newarr, intRef); //create a new  array
			il.Emit (OpCodes.Stsfld, p);
			int index = 0;
			foreach (int value in values) {
				il.Emit (OpCodes.Ldsfld, p);
				il.Emit (OpCodes.Ldc_I4, index++); // index
				il.Emit (OpCodes.Ldc_I4, value); // value
				il.Emit (OpCodes.Stelem_I4);
			}
		}

		Dictionary<string, TypeDefinition> resourceClasses = new Dictionary<string, TypeDefinition> (StringComparer.OrdinalIgnoreCase);
		Dictionary<string, MethodDefinition> staticConstructors = new Dictionary<string, MethodDefinition> ();

		void CreateCtor (TypeDefinitionCache cache, TypeDefinition type, ModuleDefinition module)
		{
			var ctor = new MethodDefinition (".ctor", MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public, module.TypeSystem.Void);
			var ctoril = ctor.Body.GetILProcessor ();
			ctoril.Emit (OpCodes.Ldarg_0);
			var o = cache.Resolve (module.TypeSystem.Object);
			ctoril.Emit (OpCodes.Call, module.ImportReference (o.Methods.First (x => x.IsConstructor)));
			ctoril.Emit (OpCodes.Ret);
			type.Methods.Add (ctor);
		}

		MethodDefinition GetOrCreateStaticCtor (TypeDefinition type, ModuleDefinition module)
		{
			string key = type.FullName + ".cctor";
			if (staticConstructors.ContainsKey (key))
				return staticConstructors[key];
			var ctor = new MethodDefinition (".cctor", MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Static, module.TypeSystem.Void);
			type.Methods.Add (ctor);
			type.IsBeforeFieldInit = false;
			staticConstructors.Add (key, ctor);
			return ctor;
		}

		TypeDefinition CreateResourceClass (TypeDefinitionCache cache, TypeDefinition resourceDesigner, string className, ModuleDefinition module, TypeAttributes attributes = TypeAttributes.NestedPublic)
		{
			string name = ResourceParser.GetNestedTypeName (className);
			string key = resourceDesigner.Name + name;
			if (resourceClasses.ContainsKey (key)) {
				return resourceClasses[key];
			}
			var resourceClass = new TypeDefinition (string.Empty, name, attributes | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed, objectRef);
			CreateCtor (cache, resourceClass, module);
			resourceDesigner.NestedTypes.Add (resourceClass);
			resourceClasses[key] = resourceClass;
			return resourceClass;
		}

		PropertyDefinition CreateProperty (string propertyName, int value, ModuleDefinition module, MethodAttributes attributes = MethodAttributes.Public)
		{
			var p = new PropertyDefinition (propertyName, PropertyAttributes.None, intRef);
			var getter = new MethodDefinition ($"get_{propertyName}", attributes | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Static, intRef);
			p.GetMethod = getter;
			p.SetMethod = null;
			var il = p.GetMethod.Body.GetILProcessor ();
			il.Emit (OpCodes.Ldc_I4, value);
			il.Emit (OpCodes.Ret);
			return p;
		}

		FieldDefinition CreateField (string fieldName, int value, ModuleDefinition module, FieldAttributes attributes = FieldAttributes.Public)
		{
			var f = new FieldDefinition (fieldName, attributes | FieldAttributes.Literal | FieldAttributes.Static | FieldAttributes.HasDefault, intRef);
			f.Constant = value;
			return f;
		}

		FieldDefinition CreateArrayField (string fieldName, int[] values, ModuleDefinition module, FieldAttributes attributes = FieldAttributes.Public)
		{
			var f = new FieldDefinition (fieldName, attributes | FieldAttributes.Static | FieldAttributes.HasDefault, intArray);
			f.Constant = values;
			return f;
		}

		PropertyDefinition CreateArrayProperty (string propertyName, int[] values, ModuleDefinition module, MethodAttributes attributes = MethodAttributes.Public)
		{
			var p = new PropertyDefinition (propertyName, PropertyAttributes.None, intArray);
			var getter = new MethodDefinition ($"get_{propertyName}", attributes | MethodAttributes.Static, intArray);
			p.GetMethod = getter;
			p.SetMethod = null;
			var il = p.GetMethod.Body.GetILProcessor ();
			il.Emit (OpCodes.Ldc_I4, values.Length);
			il.Emit (OpCodes.Newarr, intRef);
			int index = 0;
			foreach (int value in values) {
				il.Emit (OpCodes.Dup);
				il.Emit (OpCodes.Ldc_I4, index++);
				il.Emit (OpCodes.Ldc_I4, value);
				il.Emit (OpCodes.Stelem_I4);
			}
			il.Emit (OpCodes.Ret);
			return p;
		}

		void StrongNameAssembly (AssemblyNameDefinition name)
		{
			using (Stream stream = typeof (GenerateResourceDesignerAssembly).Assembly.GetManifestResourceStream ("Resource.Designer.snk")) {
				byte[] publicKey = new byte[stream.Length];
				_ = stream.Read (publicKey, 0, publicKey.Length);
				name.HashAlgorithm = AssemblyHashAlgorithm.SHA1;
				name.PublicKey = SigningHelper.GetPublicKey (publicKey);
				name.HasPublicKey = true;
				name.Attributes |= AssemblyAttributes.PublicKey;
			}
		}
	}
}
