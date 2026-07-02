using System;
using System.Reflection;

namespace Java.Interop {

	[Serializable]
	[AttributeUsage (AttributeTargets.Method | AttributeTargets.Constructor, 
			AllowMultiple=false, 
			Inherited=false)]
#if !JCW_ONLY_TYPE_NAMES
	public
#endif  // !JCW_ONLY_TYPE_NAMES
	partial class ExportAttribute : BaseExportAttribute {

		public ExportAttribute ()
		{
		}
		
		public ExportAttribute (string name)
		{
			Name = name;
		}

		public string?                Name                    {get; private set;}
		public string?                SuperArgumentsString    {get; set;}
		public Type []?               Throws                  {get; set;}
		internal string []?           ThrownNames             {get; set;} // msbuild internal use

		static MethodInfo? dynamic_callback_gen;

		internal override Delegate CreateDynamicCallback (MethodInfo method)
		{
			// We're loading the Mono.Android.Export assembly dynamically to avoid problems with circular dependencies.
			dynamic_callback_gen ??= Type.GetType ("Java.Interop.DynamicCallbackCodeGenerator, Mono.Android.Export")?.GetMethod ("Create")
				?? throw new InvalidOperationException ("To use methods marked with ExportAttribute, Mono.Android.Export.dll needs to be referenced in the application");

			return (Delegate?)dynamic_callback_gen.Invoke (null, [method])
				?? throw new InvalidOperationException (FormattableString.Invariant ($"Unable to create dynamic callback for method '{method.Name}' on type '{method.DeclaringType?.FullName}'"));
		}
	}

#if !JCW_ONLY_TYPE_NAMES
	public
#endif  // !JCW_ONLY_TYPE_NAMES
	abstract class BaseExportAttribute : Attribute
	{
		internal abstract Delegate CreateDynamicCallback (MethodInfo method);
	}
}


