using System;
using System.Linq;

using Mono.Linker;
using Mono.Linker.Steps;

using Mono.Tuner;
using Mobile.Tuner;

using Mono.Cecil;

namespace MonoTouch.Tuner {

	public class PreserveCode : IStep {

		LinkContext context;

		public void Process (LinkContext context)
		{
			this.context = context;

			PreserveDictionaryConstructor ();
			PreserveQueryableEnumerable ();
			PreserveResourceSet ();
		}

		void PreserveDictionaryConstructor ()
		{
			AssemblyDefinition corlib;
			if (!context.TryGetLinkedAssembly ("mscorlib", out corlib))
				return;

			var dictionary = corlib.MainModule.Types.FirstOrDefault (t => t.FullName == "System.Collections.Generic.Dictionary`2");
			if (dictionary == null || !dictionary.Methods.Any (m => m.IsConstructor))
				return;

			foreach (MethodDefinition ctor in dictionary.Methods.Where (m => m.IsConstructor))
				if (ctor.HasParameters && ctor.Parameters [0].ParameterType.FullName == "System.Int32")
					context.Annotations.AddPreservedMethod (dictionary, ctor);
		}

		void PreserveQueryableEnumerable ()
		{
			AssemblyDefinition core;
			if (!context.TryGetLinkedAssembly ("System.Core", out core))
				return;

			var enumerable = core.MainModule.Types.FirstOrDefault (t => t.FullName == "System.Linq.Enumerable");

			var queryable_enumerable = core.MainModule.Types.FirstOrDefault (t => t.FullName == "System.Linq.QueryableEnumerable`1");

			if (enumerable != null && queryable_enumerable != null)
				foreach (MethodDefinition method in enumerable.Methods)
					context.Annotations.AddPreservedMethod (queryable_enumerable, method);

			if (queryable_enumerable != null)
				foreach (MethodDefinition method in queryable_enumerable.Methods.Where (m => m.IsConstructor))
					context.Annotations.AddPreservedMethod (queryable_enumerable, method);
		}

		void PreserveResourceSet ()
		{
			AssemblyDefinition corlib;
			if (!context.TryGetLinkedAssembly ("mscorlib", out corlib))
				return;

			var resource_set = corlib.MainModule.Types.FirstOrDefault (t => t.FullName == "System.Resources.RuntimeResourceSet");
			if (resource_set == null || !resource_set.Methods.Any (m => m.IsConstructor))
				return;

			foreach (MethodDefinition ctor in resource_set.Methods.Where (m => m.IsConstructor))
				context.Annotations.AddPreservedMethod (resource_set, ctor);
		}
	}
}

