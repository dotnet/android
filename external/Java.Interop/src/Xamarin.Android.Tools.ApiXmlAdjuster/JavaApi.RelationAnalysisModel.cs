using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	// TypeResolver extensibility
	
	public partial class JavaClass
	{
		public  JavaTypeReference?      ResolvedExtends             { get; set; }
	}
	
	public partial class JavaImplements
	{
		public  JavaTypeReference?      ResolvedName                { get; set; }
	}
	
	public partial class JavaField
	{
		public  JavaTypeReference?      ResolvedType                { get; set; }
	}
	
	public partial class JavaMethod
	{
		public  JavaTypeReference?      ResolvedReturnType          { get; set; }
	}
	
	public partial class JavaParameter
	{
		public  JavaTypeReference?      ResolvedType                { get; set; }
	}
	
	public partial class JavaGenericConstraint
	{
		public  JavaTypeReference?      ResolvedType                { get; set; }
	}
	
	// GenericInheritanceMapper extensibility
	
	public partial class JavaClass
	{
		public IDictionary<JavaTypeReference,JavaTypeReference>?
		                                GenericInheritanceMapping   { get; set; }
	}
	
	// OverrideMarker extensibility
	
	public partial class JavaMethod
	{
		public  JavaMethodReference?    BaseMethod                  { get; set; }
		public  IList<JavaInterface>?   ImplementedInterfaces       { get; set; }
	}
	
	public partial class JavaMethodReference
	{
		public JavaMethodReference (JavaMethod candidate)
		{
			this.Method = candidate;
		}

		public  JavaMethod?             Method                      { get; set; }
	}
	
	public partial class JavaParameter
	{
		public  string?                 InstantiatedGenericArgumentName { get; set; }
	}
}

