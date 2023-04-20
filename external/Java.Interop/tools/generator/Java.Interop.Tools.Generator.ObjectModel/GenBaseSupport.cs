using System;

namespace MonoDroid.Generation
{
	public class GenBaseSupport
	{
		public string AnnotatedVisibility { get; set; }
		public bool IsAcw { get; set; }
		public bool IsDeprecated { get; set; }
		public string DeprecatedComment { get; set; }
		public int? DeprecatedSince { get; set; }
		public bool IsGeneratable { get; set; }
		public bool IsGeneric { get; set; }
		public bool IsObfuscated { get; set; }
		public string FullName { get; set; }
		public string Name { get; set; }
		public string Namespace { get; set; }
		public string JavaSimpleName { get; set; }
		public string PackageName { get; set; }
		public string TypeNamePrefix { get; set; } = string.Empty;
		public string Visibility { get; set; }
		public GenericParameterDefinitionList TypeParameters { get; set; }

		public virtual bool OnValidate (CodeGenerationOptions opt)
		{
			// See com.google.inject.internal.util package for this case.
			// Some Java compiler-generated internals are named as $foobar (dollar prefixed).
			// Since our jar2xml replaces all '$' with '.', it results in ".." namespace.
			return !FullName.Contains ("..");
		}
	}
}
