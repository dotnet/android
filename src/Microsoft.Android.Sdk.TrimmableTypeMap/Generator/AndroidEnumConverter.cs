#nullable enable

using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Converts Android enum integer values to their XML attribute string representations.
/// Ported from ManifestDocumentElement.cs.
/// </summary>
static class AndroidEnumConverter
{
	public static string? LaunchModeToString (int value) => value switch {
		1 => "singleTop",
		2 => "singleTask",
		3 => "singleInstance",
		4 => "singleInstancePerTask",
		_ => null,
	};

	public static string? ScreenOrientationToString (int value) => value switch {
		0 => "landscape",
		1 => "portrait",
		3 => "sensor",
		4 => "nosensor",
		5 => "user",
		6 => "behind",
		7 => "reverseLandscape",
		8 => "reversePortrait",
		9 => "sensorLandscape",
		10 => "sensorPortrait",
		11 => "fullSensor",
		12 => "userLandscape",
		13 => "userPortrait",
		14 => "fullUser",
		15 => "locked",
		-1 => "unspecified",
		_ => null,
	};

	public static string? ConfigChangesToString (int value)
	{
		var parts = new List<string> ();
		if ((value & 0x0001) != 0) parts.Add ("mcc");
		if ((value & 0x0002) != 0) parts.Add ("mnc");
		if ((value & 0x0004) != 0) parts.Add ("locale");
		if ((value & 0x0008) != 0) parts.Add ("touchscreen");
		if ((value & 0x0010) != 0) parts.Add ("keyboard");
		if ((value & 0x0020) != 0) parts.Add ("keyboardHidden");
		if ((value & 0x0040) != 0) parts.Add ("navigation");
		if ((value & 0x0080) != 0) parts.Add ("orientation");
		if ((value & 0x0100) != 0) parts.Add ("screenLayout");
		if ((value & 0x0200) != 0) parts.Add ("uiMode");
		if ((value & 0x0400) != 0) parts.Add ("screenSize");
		if ((value & 0x0800) != 0) parts.Add ("smallestScreenSize");
		if ((value & 0x1000) != 0) parts.Add ("density");
		if ((value & 0x2000) != 0) parts.Add ("layoutDirection");
		if ((value & 0x4000) != 0) parts.Add ("colorMode");
		if ((value & 0x8000) != 0) parts.Add ("grammaticalGender");
		if ((value & 0x10000000) != 0) parts.Add ("fontWeightAdjustment");
		if ((value & 0x40000000) != 0) parts.Add ("fontScale");
		return parts.Count > 0 ? string.Join ("|", parts) : null;
	}

	public static string? SoftInputToString (int value)
	{
		var parts = new List<string> ();
		int state = value & 0x0f;
		int adjust = value & 0xf0;
		if (state == 1) parts.Add ("stateUnchanged");
		else if (state == 2) parts.Add ("stateHidden");
		else if (state == 3) parts.Add ("stateAlwaysHidden");
		else if (state == 4) parts.Add ("stateVisible");
		else if (state == 5) parts.Add ("stateAlwaysVisible");
		if (adjust == 0x10) parts.Add ("adjustResize");
		else if (adjust == 0x20) parts.Add ("adjustPan");
		else if (adjust == 0x30) parts.Add ("adjustNothing");
		return parts.Count > 0 ? string.Join ("|", parts) : null;
	}

	public static string? DocumentLaunchModeToString (int value) => value switch {
		1 => "intoExisting",
		2 => "always",
		3 => "never",
		_ => null,
	};

	public static string? UiOptionsToString (int value) => value switch {
		1 => "splitActionBarWhenNarrow",
		_ => null,
	};

	public static string? ForegroundServiceTypeToString (int value)
	{
		var parts = new List<string> ();
		if ((value & 0x00000001) != 0) parts.Add ("dataSync");
		if ((value & 0x00000002) != 0) parts.Add ("mediaPlayback");
		if ((value & 0x00000004) != 0) parts.Add ("phoneCall");
		if ((value & 0x00000008) != 0) parts.Add ("location");
		if ((value & 0x00000010) != 0) parts.Add ("connectedDevice");
		if ((value & 0x00000020) != 0) parts.Add ("mediaProjection");
		if ((value & 0x00000040) != 0) parts.Add ("camera");
		if ((value & 0x00000080) != 0) parts.Add ("microphone");
		if ((value & 0x00000100) != 0) parts.Add ("health");
		if ((value & 0x00000200) != 0) parts.Add ("remoteMessaging");
		if ((value & 0x00000400) != 0) parts.Add ("systemExempted");
		if ((value & 0x00000800) != 0) parts.Add ("shortService");
		if ((value & 0x40000000) != 0) parts.Add ("specialUse");
		return parts.Count > 0 ? string.Join ("|", parts) : null;
	}

	public static string? ProtectionToString (int value)
	{
		int baseValue = value & 0x0f;
		return baseValue switch {
			0 => "normal",
			1 => "dangerous",
			2 => "signature",
			3 => "signatureOrSystem",
			_ => null,
		};
	}

	public static string? ActivityPersistableModeToString (int value) => value switch {
		0 => "persistRootOnly",
		1 => "persistAcrossReboots",
		2 => "persistNever",
		_ => null,
	};
}
