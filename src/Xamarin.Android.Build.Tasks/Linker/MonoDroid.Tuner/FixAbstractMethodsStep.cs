#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using Java.Interop.Tools.Cecil;
using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using Mono.Tuner;
using Xamarin.Android.Tools;
using Xamarin.Android.Tasks;
using Resources = Xamarin.Android.Tasks.Properties.Resources;

namespace MonoDroid.Tuner
{
	public class FixAbstractMethodsStep : BaseStep, IAssemblyModifierPipelineStep
	{
		readonly IMetadataResolver _injectedCache;
		readonly Func<AssemblyDefinition> _injectedGetMonoAndroidAssembly;
		readonly Action<string> _injectedLogMessage;

		/// <summary>
		/// Used by LinkAssembliesNoShrink and tests. Call <see cref="BaseStep.Initialize(LinkContext)"/> before use.
		/// </summary>
		public FixAbstractMethodsStep () { }

		/// <summary>
		/// Used by <see cref="PostTrimmingFixAbstractMethodsStep"/> when no <see cref="LinkContext"/> is available.
		/// </summary>
		internal FixAbstractMethodsStep (
			IMetadataResolver cache,
			Func<AssemblyDefinition> getMonoAndroidAssembly,
			Action<string> logMessage)
		{
			_injectedCache = cache;
			_injectedGetMonoAndroidAssembly = getMonoAndroidAssembly;
			_injectedLogMessage = logMessage;
		}

		IMetadataResolver TypeCache => _injectedCache ?? Context;

		public void ProcessAssembly (AssemblyDefinition assembly, StepContext context)
		{
			// Only run this step on non-main user Android assemblies
			if (context.IsMainAssembly || !context.IsAndroidUserAssembly)
				return;

			if (IsReadyToRunAssembly (assembly))
				return;

			context.IsAssemblyModified |= FixAbstractMethods (assembly);
		}

		internal bool FixAbstractMethods (AssemblyDefinition assembly)
		{
			bool changed = false;
			foreach (var type in assembly.MainModule.Types) {
				if (MightNeedFix (type))
					changed |= FixAbstractMethods (type);
			}
			return changed;
		}

		readonly HashSet<string> warnedAssemblies = new (StringComparer.Ordinal);
		readonly HashSet<string> warnedReadyToRunAssemblies = new (StringComparer.Ordinal);

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

		bool IsReadyToRunAssembly (AssemblyDefinition assembly)
		{
			if (assembly?.MainModule == null)
				return false;

			string fileName = assembly.MainModule.FileName;
			if (fileName.IsNullOrEmpty () || !File.Exists (fileName))
				return false;

			try {
				using (var stream = File.OpenRead (fileName))
				using (var pe = new PEReader (stream)) {
					bool isReadyToRun = pe.PEHeaders.CorHeader?.ManagedNativeHeaderDirectory.Size > 0;
					if (!isReadyToRun)
						return false;

					if (warnedReadyToRunAssemblies.Add (assembly.Name.Name)) {
						LogMessage ($"Skipping FixAbstractMethodsStep for ReadyToRun assembly '{assembly.Name.Name}'.");
					}

					return true;
				}
			} catch (IOException) {
				return false;
			} catch (UnauthorizedAccessException) {
				return false;
			} catch (BadImageFormatException) {
				return false;
			}
		}

		bool MightNeedFix (TypeDefinition type)
		{
			return !type.IsAbstract && type.IsSubclassOf ("Java.Lang.Object", TypeCache);
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

			TypeDefinition iTypeDef = TypeCache.Resolve (iType);
			if (iTypeDef == null)
				return false;

			TypeDefinition tTypeDef = TypeCache.Resolve (tType);
			if (tTypeDef == null)
				return false;

			if (iTypeDef.Module.FileName != tTypeDef.Module.FileName)
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
				if (o != null && iMethod.Name == o.Name && iMethod == TypeCache.Resolve (o))
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
			if (!type.HasInterfaces)
				return false;

			bool rv = false;
			List<MethodDefinition> typeMethods = new List<MethodDefinition> (type.Methods);
			foreach (var baseType in type.GetBaseTypes (TypeCache))
				typeMethods.AddRange (baseType.Methods);

			foreach (var ifaceInfo in type.Interfaces) {
				var iface    = ifaceInfo.InterfaceType;
				var ifaceDef = TypeCache.Resolve (iface);
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

			return declaringType.Module.ImportReference (type);
		}

		void AddNewExceptionMethod (TypeDefinition type, MethodDefinition method)
		{
			var newMethod = new MethodDefinition (method.Name, (method.Attributes | MethodAttributes.Final) & ~MethodAttributes.Abstract, TryImportType (type, method.ReturnType));

			foreach (var paramater in method.Parameters)
				newMethod.Parameters.Add (new ParameterDefinition (paramater.Name, paramater.Attributes, TryImportType (type, paramater.ParameterType)));

			var ilP = newMethod.Body.GetILProcessor ();

			ilP.Append (ilP.Create (Mono.Cecil.Cil.OpCodes.Newobj, type.Module.ImportReference (AbstractMethodErrorConstructor)));
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

		public override void LogMessage (string message)
		{
			if (_injectedLogMessage != null) {
				_injectedLogMessage (message);
				return;
			}
			base.LogMessage (message);
		}

		AssemblyDefinition GetMonoAndroidAssembly ()
		{
			if (_injectedGetMonoAndroidAssembly != null)
				return _injectedGetMonoAndroidAssembly ();
			return Context.Resolver.GetAssembly ("Mono.Android.dll");
		}
	}
}
