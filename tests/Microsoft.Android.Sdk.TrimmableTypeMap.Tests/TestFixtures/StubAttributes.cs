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

namespace Java.Interop
{
	public struct JniObjectReference
	{
		public IntPtr Handle;
	}

	public enum JniObjectReferenceOptions
	{
		None = 0,
		Copy = 1,
		CopyAndDispose = 2,
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

	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = true)]
	public sealed class UsesFeatureAttribute : Attribute
	{
		public UsesFeatureAttribute () { }
		public UsesFeatureAttribute (string name) => Name = name;

		// Name has a private setter — only settable via ctor (matches the real attribute)
		public string? Name { get; private set; }
		public int GLESVersion { get; set; }
		public bool Required { get; set; } = true;
	}

	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = true)]
	public sealed class UsesPermissionAttribute : Attribute
	{
		public UsesPermissionAttribute () { }
		public UsesPermissionAttribute (string name) => Name = name;

		public string? Name { get; set; }
		public int MaxSdkVersion { get; set; }
		public string? UsesPermissionFlags { get; set; }
	}

	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = true)]
	public sealed class UsesLibraryAttribute : Attribute
	{
		public UsesLibraryAttribute () { }
		public UsesLibraryAttribute (string name) => Name = name;
		public UsesLibraryAttribute (string name, bool required)
		{
			Name = name;
			Required = required;
		}

		public string? Name { get; set; }
		public bool Required { get; set; } = true;
	}

	[AttributeUsage (AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true)]
	public sealed class MetaDataAttribute : Attribute
	{
		public MetaDataAttribute (string name) => Name = name;

		public string Name { get; }
		public string? Value { get; set; }
		public string? Resource { get; set; }
	}

	[AttributeUsage (AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true)]
	public sealed class IntentFilterAttribute : Attribute
	{
		public IntentFilterAttribute (string [] actions) => Actions = actions;

		public string [] Actions { get; }
		public string []? Categories { get; set; }
	}

	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = true)]
	public sealed class SupportsGLTextureAttribute : Attribute
	{
		public SupportsGLTextureAttribute (string name) => Name = name;

		public string Name { get; private set; }
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
	[AttributeUsage (AttributeTargets.Method, AllowMultiple = false)]
	public sealed class ExportAttribute : Attribute
	{
		public string? Name { get; set; }
		public string[]? ThrownNames { get; set; }

		public ExportAttribute () { }
		public ExportAttribute (string name) => Name = name;
	}

	[AttributeUsage (AttributeTargets.Method, AllowMultiple = false)]
	public sealed class ExportFieldAttribute : Attribute
	{
		public string Name { get; set; }

		public ExportFieldAttribute (string name) => Name = name;
	}

	[AttributeUsage (AttributeTargets.Constructor, AllowMultiple = false)]
	public sealed class JniConstructorSignatureAttribute : Attribute
	{
		public string MemberSignature { get; }

		public JniConstructorSignatureAttribute (string memberSignature) => MemberSignature = memberSignature;
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
