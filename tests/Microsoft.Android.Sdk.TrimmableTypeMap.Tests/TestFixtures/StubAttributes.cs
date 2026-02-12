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

	public enum JniHandleOwnership
	{
		DoNotTransfer = 0,
		TransferLocalRef = 1,
		TransferGlobalRef = 2,
	}
}

namespace Android.App
{
	[AttributeUsage (AttributeTargets.Class)]
	public sealed class ActivityAttribute : Attribute, Java.Interop.IJniNameProviderAttribute
	{
		public bool MainLauncher { get; set; }
		public string? Label { get; set; }
		public string? Icon { get; set; }
		public string? Name { get; set; }
		string Java.Interop.IJniNameProviderAttribute.Name => Name ?? "";
	}

	[AttributeUsage (AttributeTargets.Class)]
	public sealed class ServiceAttribute : Attribute, Java.Interop.IJniNameProviderAttribute
	{
		public string? Name { get; set; }
		string Java.Interop.IJniNameProviderAttribute.Name => Name ?? "";
	}

	[AttributeUsage (AttributeTargets.Class)]
	public sealed class InstrumentationAttribute : Attribute, Java.Interop.IJniNameProviderAttribute
	{
		public string? Name { get; set; }
		string Java.Interop.IJniNameProviderAttribute.Name => Name ?? "";
	}

	[AttributeUsage (AttributeTargets.Class)]
	public sealed class ApplicationAttribute : Attribute, Java.Interop.IJniNameProviderAttribute
	{
		public Type? BackupAgent { get; set; }
		public Type? ManageSpaceActivity { get; set; }
		public string? Name { get; set; }
		string Java.Interop.IJniNameProviderAttribute.Name => Name ?? "";
	}
}

namespace Android.Content
{
	[AttributeUsage (AttributeTargets.Class)]
	public sealed class BroadcastReceiverAttribute : Attribute, Java.Interop.IJniNameProviderAttribute
	{
		public string? Name { get; set; }
		string Java.Interop.IJniNameProviderAttribute.Name => Name ?? "";
	}

	[AttributeUsage (AttributeTargets.Class)]
	public sealed class ContentProviderAttribute : Attribute, Java.Interop.IJniNameProviderAttribute
	{
		public string []? Authorities { get; set; }
		public string? Name { get; set; }
		string Java.Interop.IJniNameProviderAttribute.Name => Name ?? "";

		public ContentProviderAttribute (string [] authorities) => Authorities = authorities;
	}
}

namespace Java.Interop
{
	[AttributeUsage (AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = false)]
	public sealed class ExportAttribute : Attribute
	{
		public string? Name { get; set; }

		public string[]? ThrownNames { get; set; }

		public string? SuperArgumentsString { get; set; }

		public ExportAttribute () { }
		public ExportAttribute (string name) => Name = name;
	}
}

namespace MyApp
{
	[AttributeUsage (AttributeTargets.Class)]
	public sealed class CustomJniNameAttribute : Attribute, Java.Interop.IJniNameProviderAttribute
	{
		public string Name { get; }
		public CustomJniNameAttribute (string name) => Name = name;
	}
}
