// Minimal stub attributes mirroring the real Mono.Android attributes.
// These exist solely so the test fixture assembly can have types
// with the same attribute shapes the scanner expects.

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

		public RegisterAttribute (string name)
		{
			Name = name;
		}

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
	public sealed class ActivityAttribute : Attribute
	{
		public bool MainLauncher { get; set; }
		public string? Label { get; set; }
		public string? Icon { get; set; }
		public string? Name { get; set; }
		public string? Theme { get; set; }
		public string? ParentActivity { get; set; }
		public bool Exported { get; set; }
		public string? Permission { get; set; }
		public string? Process { get; set; }
		public bool Enabled { get; set; } = true;
		public string? ConfigurationChanges { get; set; }
		public string? LaunchMode { get; set; }
		public string? ScreenOrientation { get; set; }
		public string? WindowSoftInputMode { get; set; }
	}

	[AttributeUsage (AttributeTargets.Class)]
	public sealed class ServiceAttribute : Attribute
	{
		public string? Name { get; set; }
		public bool Exported { get; set; }
		public bool Enabled { get; set; } = true;
		public string? Permission { get; set; }
		public string? Process { get; set; }
		public bool IsolatedProcess { get; set; }
		public string? ForegroundServiceType { get; set; }
	}

	[AttributeUsage (AttributeTargets.Class)]
	public sealed class InstrumentationAttribute : Attribute
	{
		public string? Name { get; set; }
		public string? TargetPackage { get; set; }
		public bool FunctionalTest { get; set; }
		public bool HandleProfiling { get; set; }
		public string? Label { get; set; }
	}

	[AttributeUsage (AttributeTargets.Class)]
	public sealed class ApplicationAttribute : Attribute
	{
		public Type? BackupAgent { get; set; }
		public Type? ManageSpaceActivity { get; set; }
		public string? Name { get; set; }
		public string? Theme { get; set; }
		public string? Label { get; set; }
		public string? Icon { get; set; }
		public bool Debuggable { get; set; }
		public bool AllowBackup { get; set; }
		public bool SupportsRtl { get; set; }
	}

	[AttributeUsage (AttributeTargets.Class, AllowMultiple = true)]
	public sealed class IntentFilterAttribute : Attribute
	{
		public string []? Actions { get; }
		public string []? Categories { get; set; }
		public string? DataScheme { get; set; }
		public string? DataHost { get; set; }
		public string? DataPathPrefix { get; set; }
		public int Priority { get; set; }
		public bool AutoVerify { get; set; }

		public IntentFilterAttribute (string [] actions)
		{
			Actions = actions;
		}
	}

	[AttributeUsage (AttributeTargets.Class, AllowMultiple = true)]
	public sealed class MetaDataAttribute : Attribute
	{
		public string Name { get; }
		public string? Value { get; set; }
		public string? Resource { get; set; }

		public MetaDataAttribute (string name)
		{
			Name = name;
		}
	}

	[AttributeUsage (AttributeTargets.Class)]
	public sealed class LayoutAttribute : Attribute
	{
		public string? DefaultWidth { get; set; }
		public string? DefaultHeight { get; set; }
		public string? Gravity { get; set; }
		public string? MinWidth { get; set; }
		public string? MinHeight { get; set; }
	}

	[AttributeUsage (AttributeTargets.Class, AllowMultiple = true)]
	public sealed class PropertyAttribute : Attribute
	{
		public string Name { get; }
		public string? Value { get; set; }

		public PropertyAttribute (string name)
		{
			Name = name;
		}
	}
}

namespace Android.Content
{
	[AttributeUsage (AttributeTargets.Class)]
	public sealed class BroadcastReceiverAttribute : Attribute
	{
		public string? Name { get; set; }
		public bool Exported { get; set; }
		public bool Enabled { get; set; } = true;
		public string? Permission { get; set; }
		public string? Process { get; set; }
	}

	[AttributeUsage (AttributeTargets.Class)]
	public sealed class ContentProviderAttribute : Attribute
	{
		public string []? Authorities { get; set; }
		public string? Name { get; set; }
		public bool Exported { get; set; }
		public bool Enabled { get; set; } = true;
		public string? Permission { get; set; }
		public bool GrantUriPermissions { get; set; }
		public int InitOrder { get; set; }

		public ContentProviderAttribute (string [] authorities)
		{
			Authorities = authorities;
		}
	}

	[AttributeUsage (AttributeTargets.Class, AllowMultiple = true)]
	public sealed class GrantUriPermissionAttribute : Attribute
	{
		public string? Path { get; set; }
		public string? PathPattern { get; set; }
		public string? PathPrefix { get; set; }
	}
}

namespace Java.Interop
{
	[AttributeUsage (AttributeTargets.Method, AllowMultiple = false)]
	public sealed class ExportAttribute : Attribute
	{
		public string? Name { get; set; }

		public string[]? ThrownNames { get; set; }

		public string? SuperArgumentsString { get; set; }

		public ExportAttribute ()
		{
		}

		public ExportAttribute (string name)
		{
			Name = name;
		}
	}
}
