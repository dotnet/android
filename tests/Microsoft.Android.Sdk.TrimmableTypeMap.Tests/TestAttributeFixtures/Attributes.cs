using System;

namespace Java.Interop
{
	public interface IJniNameProviderAttribute
	{
		string Name { get; }
	}
}

namespace Android.Runtime
{
	[AttributeUsage (AttributeTargets.Class)]
	public sealed class AnnotationAttribute : Attribute
	{
		public string JavaName { get; }

		public AnnotationAttribute (string javaName) => JavaName = javaName;
	}

	[AttributeUsage (
		AttributeTargets.Class | AttributeTargets.Constructor | AttributeTargets.Field |
		AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property,
		AllowMultiple = false)]
	public sealed class RegisterAttribute : Attribute, Java.Interop.IJniNameProviderAttribute
	{
		public string Name { get; }
		public string? Signature { get; set; }
		public string? Connector { get; set; }
		public bool DoNotGenerateAcw { get; set; }
		public int ApiSince { get; set; }

		public RegisterAttribute (string name) => Name = name;

		public RegisterAttribute (string name, string signature, string connector)
		{
			Name = name;
			Signature = signature;
			Connector = connector;
		}
	}
}

namespace Android.Webkit
{
	[Android.Runtime.Annotation ("android.webkit.JavascriptInterface")]
	[AttributeUsage (AttributeTargets.Method)]
	public sealed class JavascriptInterfaceAttribute : Attribute
	{
	}
}

namespace MyApp
{
	[Android.Runtime.Annotation ("com.example.Custom")]
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Constructor | AttributeTargets.Method)]
	public sealed class JavaAnnotationAttribute : Attribute
	{
		[Android.Runtime.Register ("text")]
		public string? Text { get; set; }

		public bool Enabled { get; set; }
	}
}
