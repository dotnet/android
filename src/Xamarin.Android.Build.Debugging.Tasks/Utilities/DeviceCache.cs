using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Helper class for managing the device property cache XML file.
	/// </summary>
	public static class DeviceCache
	{
		/// <summary>
		/// Updates or adds a device entry in the cache document.
		/// </summary>
		/// <param name="doc">The XDocument to update, or null to create a new one.</param>
		/// <param name="deviceId">The device ID.</param>
		/// <param name="resultingAbi">The device's ABI.</param>
		/// <param name="toolsAbi">The tools ABI.</param>
		/// <param name="sdkVersion">The SDK version.</param>
		/// <param name="longOutput">The long output string from adb.</param>
		/// <returns>The updated or newly created XDocument.</returns>
		public static XDocument Update (XDocument doc, string deviceId, string resultingAbi, string toolsAbi, int sdkVersion, string longOutput)
		{
			XElement devices;
			if (doc == null) {
				devices = new XElement ("Devices");
				doc = new XDocument (
					new XDeclaration ("1.0", "UTF-8", null),
					devices);
			}
			else {
				devices = doc.Element ("Devices");
			}

			var deviceElement = devices
					.Elements ("Device")
					.FirstOrDefault (a => a.Attribute ("id")?.Value == deviceId)
				??
					new XElement ("Device", new XAttribute ("id", deviceId));

			if (deviceElement.Parent == null) {
				devices.Add (deviceElement);
			}

			deviceElement.SetElementValue ("ResultingAbi", resultingAbi);
			deviceElement.SetElementValue ("ToolsAbi", toolsAbi);
			deviceElement.SetElementValue ("SdkVersion", sdkVersion);
			deviceElement.SetElementValue ("LongOutput", longOutput);

			return doc;
		}

		/// <summary>
		/// Tries to get cached device properties from the document.
		/// </summary>
		/// <param name="doc">The XDocument to search.</param>
		/// <param name="deviceId">The device ID to look up.</param>
		/// <param name="longOutput">The expected long output to validate the cache entry.</param>
		/// <param name="resultingAbi">The cached ABI if found and valid.</param>
		/// <param name="toolsAbi">The cached tools ABI if found and valid.</param>
		/// <param name="sdkVersion">The cached SDK version if found and valid.</param>
		/// <param name="log">Optional logger for debug messages.</param>
		/// <returns>True if a valid cache entry was found, false otherwise.</returns>
		public static bool TryGet (XDocument doc, string deviceId, string longOutput, out string resultingAbi, out string toolsAbi, out int sdkVersion, TaskLoggingHelper log = null)
		{
			resultingAbi = null;
			toolsAbi = null;
			sdkVersion = 0;

			if (doc == null)
				return false;

			var element = doc.Elements ("Devices").Elements ("Device")
				.FirstOrDefault (a => a.Attribute ("id")?.Value == deviceId);

			if (element == null)
				return false;

			string cachedLongOutput = element.Element ("LongOutput")?.Value ?? string.Empty;
			if (string.Compare (cachedLongOutput, longOutput, System.StringComparison.OrdinalIgnoreCase) != 0) {
				// The device LongOutput changed. So lets find the device again.
				// This can happen if the emulator is different but has the same id or
				// if the device changed usb ports.
				log?.LogMessage ($"Device LongOutput has changed. Cached '{cachedLongOutput}' does not match current '{longOutput}'. Refreshing device cache.");
				return false;
			}

			resultingAbi = element.Element ("ResultingAbi")?.Value;
			toolsAbi = element.Element ("ToolsAbi")?.Value ?? string.Empty;

			if (!int.TryParse (element.Element ("SdkVersion")?.Value, out sdkVersion))
				return false;

			return !string.IsNullOrEmpty (resultingAbi);
		}
	}
}
