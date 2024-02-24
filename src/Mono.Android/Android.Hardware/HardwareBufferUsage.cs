namespace Android.Hardware;

[System.Flags]
[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
public enum HardwareBufferUsage : long
{
	None = 0,

	// Metadata.xml XPath field reference: path="/api/package[@name='android.hardware']/class[@name='HardwareBuffer']/field[@name='USAGE_COMPOSER_OVERLAY']"
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android33.0")]
	UsageComposerOverlay = 2048,

	// Metadata.xml XPath field reference: path="/api/package[@name='android.hardware']/class[@name='HardwareBuffer']/field[@name='USAGE_CPU_READ_OFTEN']"
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
	UsageCpuReadOften = 3,

	// Metadata.xml XPath field reference: path="/api/package[@name='android.hardware']/class[@name='HardwareBuffer']/field[@name='USAGE_CPU_READ_RARELY']"
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
	UsageCpuReadRarely = 2,

	// Metadata.xml XPath field reference: path="/api/package[@name='android.hardware']/class[@name='HardwareBuffer']/field[@name='USAGE_CPU_WRITE_OFTEN']"
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
	UsageCpuWriteOften = 48,

	// Metadata.xml XPath field reference: path="/api/package[@name='android.hardware']/class[@name='HardwareBuffer']/field[@name='USAGE_CPU_WRITE_RARELY']"
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
	UsageCpuWriteRarely = 32,

	// Metadata.xml XPath field reference: path="/api/package[@name='android.hardware']/class[@name='HardwareBuffer']/field[@name='USAGE_FRONT_BUFFER']"
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android33.0")]
	UsageFrontBuffer = 4294967296,

	// Metadata.xml XPath field reference: path="/api/package[@name='android.hardware']/class[@name='HardwareBuffer']/field[@name='USAGE_GPU_COLOR_OUTPUT']"
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
	UsageGpuColorOutput = 512,

	// Metadata.xml XPath field reference: path="/api/package[@name='android.hardware']/class[@name='HardwareBuffer']/field[@name='USAGE_GPU_CUBE_MAP']"
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android28.0")]
	UsageGpuCubeMap = 33554432,

	// Metadata.xml XPath field reference: path="/api/package[@name='android.hardware']/class[@name='HardwareBuffer']/field[@name='USAGE_GPU_DATA_BUFFER']"
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
	UsageGpuDataBuffer = 16777216,

	// Metadata.xml XPath field reference: path="/api/package[@name='android.hardware']/class[@name='HardwareBuffer']/field[@name='USAGE_GPU_MIPMAP_COMPLETE']"
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android28.0")]
	UsageGpuMipmapComplete = 67108864,

	// Metadata.xml XPath field reference: path="/api/package[@name='android.hardware']/class[@name='HardwareBuffer']/field[@name='USAGE_GPU_SAMPLED_IMAGE']"
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
	UsageGpuSampledImage = 256,

	// Metadata.xml XPath field reference: path="/api/package[@name='android.hardware']/class[@name='HardwareBuffer']/field[@name='USAGE_PROTECTED_CONTENT']"
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
	UsageProtectedContent = 16384,

	// Metadata.xml XPath field reference: path="/api/package[@name='android.hardware']/class[@name='HardwareBuffer']/field[@name='USAGE_SENSOR_DIRECT_DATA']"
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
	UsageSensorDirectData = 8388608,

	// Metadata.xml XPath field reference: path="/api/package[@name='android.hardware']/class[@name='HardwareBuffer']/field[@name='USAGE_VIDEO_ENCODE']"
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
	UsageVideoEncode = 65536
}
