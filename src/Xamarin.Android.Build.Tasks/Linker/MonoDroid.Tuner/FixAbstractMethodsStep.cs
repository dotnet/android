using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Mono.Cecil;

using Java.Interop.Tools.Cecil;

using Mono.Linker;
using Mono.Linker.Steps;

using Mono.Tuner;
#if ILLINK
using Microsoft.Android.Sdk.ILLink;
using Resources = Microsoft.Android.Sdk.ILLink.Properties.Resources;
#else   // !ILLINK
using Resources = Xamarin.Android.Tasks.Properties.Resources;
#endif  // !ILLINK

namespace MonoDroid.Tuner
{
	/// <summary>
	/// NOTE: this step is subclassed so it can be called directly from Xamarin.Android.Build.Tasks
	/// </summary>
	public class FixAbstractMethodsStep : BaseStep
	{
#if ILLINK
		IMetadataResolver cache => Context;
#else   // !ILLINK
		readonly IMetadataResolver cache;

		public FixAbstractMethodsStep (IMetadataResolver cache)
		{
			this.cache = cache;
		}
#endif  // !ILLINK

		bool CheckShouldProcessAssembly (AssemblyDefinition assembly)
		{
			if (!Annotations.HasAction (assembly))
				Annotations.SetAction (assembly, AssemblyAction.Skip);

			if (IsProductOrSdkAssembly (assembly))
				return false;

			CheckAppDomainUsage (assembly, (string msg) =>
#if ILLINK
				Context.LogMessage (MessageContainer.CreateCustomWarningMessage (Context, msg, 6200, new MessageOrigin (), WarnVersion.ILLink5))
#else   // !ILLINK
				Context.LogMessage (MessageImportance.High, "warning XA2000: " + msg)
#endif  // !ILLINK
			);

			return assembly.MainModule.HasTypeReference ("Java.Lang.Object");
		}

		void UpdateAssemblyAction (AssemblyDefinition assembly)
		{
			if (Annotations.GetAction (assembly) == AssemblyAction.Copy)
				Annotations.SetAction (assembly, AssemblyAction.Save);
		}

#if ILLINK
		readonly List<AssemblyDefinition> assemblies = new ();

		protected override void ProcessAssembly (AssemblyDefinition assembly)
		{
			assemblies.Add (assembly);
		}

		protected override void EndProcess ()
		{
			foreach (var assembly in GetReferencedAssemblies().ToList()) {
				ProcessAssembly_Actual(assembly);
			}

			IEnumerable<AssemblyDefinition> GetReferencedAssemblies ()
			{
				var loaded = new HashSet<AssemblyDefinition> (assemblies);
				var toProcess = new Queue<AssemblyDefinition> (assemblies);

				while (toProcess.Count > 0) {
					var assembly = toProcess.Dequeue ();
					foreach (var reference in ResolveReferences (assembly)) {
						if (!loaded.Add (reference))
							continue;
						yield return reference;
						toProcess.Enqueue (reference);
					}
				}
			}

			ICollection<AssemblyDefinition> ResolveReferences (AssemblyDefinition assembly)
			{
				List<AssemblyDefinition> references = new List<AssemblyDefinition> ();
				if (assembly == null)
					return references;

				foreach (AssemblyNameReference reference in assembly.MainModule.AssemblyReferences) {
					AssemblyDefinition? definition = Context.Resolve (reference);
					if (definition != null)
						references.Add (definition);
				}

				return references;
			}
		}

		protected void ProcessType (TypeDefinition type)
		{
			var assembly = type.Module.Assembly;
			if (!CheckShouldProcessAssembly (assembly))
				return;

			if (!FixAbstractMethods (type))
				return;

			UpdateAssemblyAction (assembly);
			MarkAbstractMethodErrorType ();
		}
#endif  // ILLINK

#if ILLINK
		void ProcessAssembly_Actual (AssemblyDefinition assembly)
#else  // !ILLINK
		protected override void ProcessAssembly (AssemblyDefinition assembly)
#endif  // !ILLINK
		{
			if (!CheckShouldProcessAssembly (assembly))
				return;

			if (FixAbstractMethods (assembly)) {
#if !ILLINK
				Context.SafeReadSymbols (assembly);
#endif  // !ILLINK
				UpdateAssemblyAction (assembly);
				MarkAbstractMethodErrorType ();
			}
		}

		internal bool FixAbstractMethods (AssemblyDefinition assembly)
		{
			bool changed = false;
			foreach (var type in assembly.MainModule.Types) {
				changed |= FixAbstractMethodsNested (type);
			}
			return changed;

			bool FixAbstractMethodsNested (TypeDefinition type)
			{
				bool changed = FixAbstractMethods (type);
				foreach (var nested in type.NestedTypes) {
					changed |= FixAbstractMethodsNested (nested);
				}
				return changed;
			}
		}

		readonly HashSet<string> warnedAssemblies = new (StringComparer.Ordinal);

		internal void CheckAppDomainUsage (AssemblyDefinition assembly, Action<string> warn)
		{
			if (!warnedAssemblies.Add (assembly.Name.Name))
				return;
			if (!assembly.MainModule.HasTypeReference ("System.AppDomain"))
				return;

			foreach (var mr in assembly.MainModule.GetMemberReferences ()) {
				if (mr.ToString ().Contains ("System.AppDomain System.AppDomain::CreateDomain")) {
					warn (string.Format (CultureInfo.CurrentCulture, Resources.XA2000, assembly));
					break;
				}
			}
		}

		bool IsProductOrSdkAssembly (AssemblyDefinition assembly) =>
			IsProductOrSdkAssembly (assembly.Name.Name);

		public bool IsProductOrSdkAssembly (string assemblyName) =>
			Profile.IsSdkAssembly (assemblyName) || Profile.IsProductAssembly (assemblyName);

		bool MightNeedFix (TypeDefinition type)
		{
			return !type.IsAbstract && type.IsSubclassOf ("Java.Lang.Object", cache);
		}

		bool CompareTypes (TypeReference iType, TypeReference tType)
		{
			if (iType.IsGenericParameter)
				return true;

			if (iType.IsArray) {
				if (!tType.IsArray)
					return false;
				return CompareTypes (iType.GetElementType (), tType.GetElementType ());
			}

			if (iType.IsByReference) {
				if (!tType.IsByReference)
					return false;
				return CompareTypes (iType.GetElementType (), tType.GetElementType ());
			}

			if (iType.Name != tType.Name)
				return false;

			if (iType.Namespace != tType.Namespace)
				return false;

			TypeDefinition iTypeDef = cache.Resolve (iType);
			if (iTypeDef == null)
				return false;

			TypeDefinition tTypeDef = cache.Resolve (tType);
			if (tTypeDef == null)
				return false;

			if (iTypeDef.Module.FullyQualifiedName != tTypeDef.Module.FullyQualifiedName)
				return false;

			if (iType is Mono.Cecil.GenericInstanceType && tType is Mono.Cecil.GenericInstanceType) {
				GenericInstanceType iGType = iType as GenericInstanceType;
				GenericInstanceType tGType = tType as GenericInstanceType;

				if (iGType.GenericArguments.Count != tGType.GenericArguments.Count)
					return false;
				for (int i = 0; i < iGType.GenericArguments.Count; i++) {
					if (iGType.GenericArguments [i].IsGenericParameter)
						continue;
					if (!CompareTypes (iGType.GenericArguments [i], tGType.GenericArguments [i]))
						return false;
				}
			}

			return true;
		}

		bool IsInOverrides (MethodDefinition iMethod, MethodDefinition tMethod)
		{
			if (!tMethod.HasOverrides)
				return false;

			foreach (var o in tMethod.Overrides)
				if (o != null && iMethod.Name == o.Name && iMethod == cache.Resolve (o))
					return true;

			return false;
		}

		bool HaveSameSignature (TypeReference iface, MethodDefinition iMethod, MethodDefinition tMethod)
		{
			if (IsInOverrides (iMethod, tMethod))
				return true;

			if (iMethod.Name != tMethod.Name)
				return false;

			if (!CompareTypes (iMethod.MethodReturnType.ReturnType, tMethod.MethodReturnType.ReturnType))
				return false;

			if (iMethod.Parameters.Count != tMethod.Parameters.Count || iMethod.GenericParameters.Count != tMethod.GenericParameters.Count)
				return false;

			if (iMethod.HasParameters) {
				List<ParameterDefinition> m1p = new List<ParameterDefinition> (iMethod.Parameters);
				List<ParameterDefinition> m2p = new List<ParameterDefinition> (tMethod.Parameters);

				for (int i = 0; i < m1p.Count; i++) {
					if (!CompareTypes (m1p [i].ParameterType,  m2p [i].ParameterType))
						return false;
				}
			}

			if (iMethod.HasGenericParameters) {
				List<GenericParameter> m1p = new List<GenericParameter> (iMethod.GenericParameters);
				List<GenericParameter> m2p = new List<GenericParameter> (tMethod.GenericParameters);

				for (int i = 0; i < m1p.Count; i++)
					if (!CompareTypes (m1p [i], m2p [i]))
						return false;
			}

			return true;
		}

		bool FixAbstractMethods (TypeDefinition type)
		{
			if (!MightNeedFix (type))
				return false;

			if (!type.HasInterfaces)
				return false;

			bool rv = false;
			List<MethodDefinition> typeMethods = new List<MethodDefinition> (type.Methods);
			foreach (var baseType in type.GetBaseTypes (cache))
				typeMethods.AddRange (baseType.Methods);

			foreach (var ifaceInfo in type.Interfaces) {
				var iface    = ifaceInfo.InterfaceType;
				var ifaceDef = cache.Resolve (iface);
				if (ifaceDef == null) {
					LogMessage ($"Unable to unresolve interface: {iface.FullName}");
					continue;
				}
				if (ifaceDef.HasGenericParameters)
					continue;

				foreach (var iMethod in ifaceDef.Methods.Where (m => m.IsAbstract)) {
					bool exists = false;

					foreach (var tMethod in typeMethods) {
						if (HaveSameSignature (iface, iMethod, tMethod)) {
							exists = true;
							break;
						}
					}

					if (!exists) {
						AddNewExceptionMethod (type, iMethod);
						rv = true;
					}
				}
			}

			return rv;
		}

		TypeReference TryImportType (TypeDefinition declaringType, TypeReference type)
		{
			if (type.IsGenericParameter)
				return type;

			return declaringType.Module.Import (type);
		}

		void AddNewExceptionMethod (TypeDefinition type, MethodDefinition method)
		{
			var newMethod = new MethodDefinition (method.Name, (method.Attributes | MethodAttributes.Final) & ~MethodAttributes.Abstract, TryImportType (type, method.ReturnType));

			foreach (var paramater in method.Parameters)
				newMethod.Parameters.Add (new ParameterDefinition (paramater.Name, paramater.Attributes, TryImportType (type, paramater.ParameterType)));

			var ilP = newMethod.Body.GetILProcessor ();

			ilP.Append (ilP.Create (Mono.Cecil.Cil.OpCodes.Newobj, type.Module.Import (AbstractMethodErrorConstructor)));
			ilP.Append (ilP.Create (Mono.Cecil.Cil.OpCodes.Throw));

			type.Methods.Add (newMethod);

			LogMessage ($"Added method: {method} to type: {type.FullName} scope: {type.Scope}");
		}

		MethodDefinition abstractMethodErrorConstructor;

		MethodDefinition AbstractMethodErrorConstructor {
			get {
				if (abstractMethodErrorConstructor != null)
					return abstractMethodErrorConstructor;

				var assembly = GetMonoAndroidAssembly ();
				if (assembly != null) { 
					var errorException = assembly.MainModule.GetType ("Java.Lang.AbstractMethodError");
					if (errorException != null) {
						foreach (var method in errorException.Methods) {
							if (method.Name == ".ctor" && !method.HasParameters) {
								abstractMethodErrorConstructor = method;
								break;
							}
						}
					}
				}

				if (abstractMethodErrorConstructor == null)
					throw new Exception ("Unable to find Java.Lang.AbstractMethodError constructor in Mono.Android assembly");

				return abstractMethodErrorConstructor;
			}
		}

		bool markedAbstractMethodErrorType;

		void MarkAbstractMethodErrorType ()
		{
			if (markedAbstractMethodErrorType)
				return;
			markedAbstractMethodErrorType = true;


			var td = AbstractMethodErrorConstructor.DeclaringType;
			Annotations.Mark (td);
			Annotations.SetPreserve (td, TypePreserve.Nothing);
			Annotations.AddPreservedMethod (td, AbstractMethodErrorConstructor);
		}

		public virtual void LogMessage (string message)
		{
			Context.LogMessage (message);
		}

		protected virtual AssemblyDefinition GetMonoAndroidAssembly ()
		{
#if !ILLINK
			foreach (var assembly in Context.GetAssemblies ()) {
				if (assembly.Name.Name == "Mono.Android")
					return assembly;
			}
			return null;
#else   // ILLINK
			return Context.GetLoadedAssembly ("Mono.Android");
#endif  // ILLINK
		}
	}
}
