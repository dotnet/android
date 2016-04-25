// Copyright (C) 2011, Xamarin Inc.
// Copyright (C) 2010, Novell Inc.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace Xamarin.Android.Tasks
{
	public class RemoveRegisterAttribute : Task
	{
		const string RegisterAttribute = "Android.Runtime.RegisterAttribute";

		[Required]
		public ITaskItem[] ShrunkFrameworkAssemblies { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("RemoveRegisterAttribute Task");
			Log.LogDebugTaskItems ("  ShrunkFrameworkAssemblies:", ShrunkFrameworkAssemblies);

			// Find Mono.Android.dll
			var mono_android = ShrunkFrameworkAssemblies.First (f => Path.GetFileNameWithoutExtension (f.ItemSpec) == "Mono.Android").ItemSpec;
			var assembly = AssemblyDefinition.ReadAssembly (mono_android);

			// Strip out [Register] attributes
			foreach (TypeDefinition type in assembly.MainModule.Types)
				ProcessType (type);

			assembly.Write (mono_android);
			
			return true;
		}

		private static void ProcessType (TypeDefinition type)
		{
			if (type.HasFields)
				foreach (FieldDefinition field in type.Fields)
					ProcessAttributeProvider (field);

			if (type.HasMethods)
				foreach (MethodDefinition method in type.Methods)
					ProcessAttributeProvider (method);
		}

		private static void ProcessAttributeProvider (Mono.Cecil.ICustomAttributeProvider provider)
		{
			if (!provider.HasCustomAttributes)
				return;

			for (int i = 0; i < provider.CustomAttributes.Count; i++) {
				if (!IsRegisterAttribute (provider.CustomAttributes [i]))
					continue;

				provider.CustomAttributes.RemoveAt (i--);
			}
		}

		private static bool IsRegisterAttribute (CustomAttribute attribute)
		{
			return attribute.Constructor.DeclaringType.FullName == RegisterAttribute;
		}
	}
}

