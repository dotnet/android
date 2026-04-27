#nullable enable
using System;

namespace Java.Interop {

	[AttributeUsage (AttributeTargets.Method, AllowMultiple=false)]
	public sealed class JavaCallableAttribute : Attribute {

		public JavaCallableAttribute ()
		{
		}

		public JavaCallableAttribute (string? name)
		{
			Name = name;
		}

		public  string?     Name        {get; private set;}
		public  string?     Signature   {get; set;}
	}

	[AttributeUsage (AttributeTargets.Constructor, AllowMultiple=false)]
	public sealed class JavaCallableConstructorAttribute : Attribute {

		public JavaCallableConstructorAttribute ()
		{
		}

		public  string?     SuperConstructorExpression  {get; set;}
		public  string?     Signature                   {get; set;}
	}
}
