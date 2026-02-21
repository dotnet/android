using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class GetAvailableAndroidDevicesTests : BaseTest
	{
		List<BuildErrorEventArgs> errors;
		List<BuildWarningEventArgs> warnings;
		List<BuildMessageEventArgs> messages;
		MockBuildEngine engine;

		[SetUp]
		public void Setup ()
		{
			engine = new MockBuildEngine (TestContext.Out, errors = [], warnings = [], messages = []);
		}

		/// <summary>
		/// Mock version of GetAvailableAndroidDevices that returns known AVD names
		/// </summary>
		class MockGetAvailableAndroidDevices : GetAvailableAndroidDevices
		{
			// Real AVD names from 'emulator -list-avds' can have mixed case, underscores, and dashes
			readonly Dictionary<string, string> AvdNames = new () {
				{ "emulator-5554", "pixel_7_-_api_35" },
				{ "emulator-5556", "Pixel_9_Pro_XL" }
			};

			public List<string> MockAvailableEmulators { get; set; } = [];

			protected override string? GetEmulatorAvdName (string serial)
			{
				if (AvdNames.TryGetValue (serial, out var name))
					return name;
				return null;
			}

			protected override List<string> GetAvailableEmulators ()
			{
				return MockAvailableEmulators;
			}
		}

		/// <summary>
		/// Helper method to invoke the private ParseAdbDevicesOutput method via reflection
		/// </summary>
		ITaskItem [] ParseAdbDevicesOutput (MockGetAvailableAndroidDevices task, List<string> lines)
		{
			var method = typeof (GetAvailableAndroidDevices).GetMethod ("ParseAdbDevicesOutput", BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.IsNotNull (method, "ParseAdbDevicesOutput method should exist");
			var result = (List<ITaskItem>) method.Invoke (task, [lines]);
			return result.ToArray ();
		}

		[Test]
		public void ParseRealWorldData ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			// Real data from adb devices -l
			var lines = new List<string> {
				"List of devices attached",
				"0A041FDD400327         device product:redfin model:Pixel_5 device:redfin transport_id:2",
				"emulator-5554          device product:sdk_gphone64_x86_64 model:sdk_gphone64_x86_64 device:emu64xa transport_id:1"
			};

			var devices = ParseAdbDevicesOutput (task, lines);

			Assert.AreEqual (2, devices.Length, "Should return two devices");

			// First device: Pixel 5 (physical device)
			var device1 = devices [0];
			Assert.AreEqual ("0A041FDD400327", device1.ItemSpec, "Device serial should match");
			Assert.AreEqual ("Device", device1.GetMetadata ("Type"), "Type should be Device");
			Assert.AreEqual ("Online", device1.GetMetadata ("Status"), "Status should be Online");
			Assert.AreEqual ("Pixel 5", device1.GetMetadata ("Description"), "Description should be 'Pixel 5'");
			Assert.AreEqual ("Pixel_5", device1.GetMetadata ("Model"), "Model metadata should be 'Pixel_5'");
			Assert.AreEqual ("redfin", device1.GetMetadata ("Product"), "Product metadata should be 'redfin'");
			Assert.AreEqual ("redfin", device1.GetMetadata ("Device"), "Device metadata should be 'redfin'");
			Assert.AreEqual ("2", device1.GetMetadata ("TransportId"), "TransportId should be '2'");

			// Second device: Emulator - should have AVD name replacement applied
			var device2 = devices [1];
			Assert.AreEqual ("emulator-5554", device2.ItemSpec, "Emulator serial should match");
			Assert.AreEqual ("Emulator", device2.GetMetadata ("Type"), "Type should be Emulator");
			Assert.AreEqual ("Online", device2.GetMetadata ("Status"), "Status should be Online");
			Assert.AreEqual ("Pixel 7 - API 35", device2.GetMetadata ("Description"), "Description should be replaced with AVD name");
			Assert.AreEqual ("pixel_7_-_api_35", device2.GetMetadata ("AvdName"), "AvdName metadata should be raw AVD name");
			Assert.AreEqual ("sdk_gphone64_x86_64", device2.GetMetadata ("Model"), "Model metadata should be 'sdk_gphone64_x86_64'");
			Assert.AreEqual ("sdk_gphone64_x86_64", device2.GetMetadata ("Product"), "Product metadata should be 'sdk_gphone64_x86_64'");
			Assert.AreEqual ("emu64xa", device2.GetMetadata ("Device"), "Device metadata should be 'emu64xa'");
			Assert.AreEqual ("1", device2.GetMetadata ("TransportId"), "TransportId should be '1'");
		}

		[Test]
		public void ParseEmptyOutput ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var lines = new List<string> {
				"List of devices attached",
				""
			};

			var devices = ParseAdbDevicesOutput (task, lines);

			Assert.AreEqual (0, devices.Length, "Should return no devices for empty list");
		}

		[Test]
		public void ParseSingleEmulator ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var lines = new List<string> {
				"List of devices attached",
				"emulator-5554          device product:sdk_gphone64_arm64 model:sdk_gphone64_arm64 device:emu64a transport_id:1"
			};

			var devices = ParseAdbDevicesOutput (task, lines);

			Assert.AreEqual (1, devices.Length, "Should return one device");
			var device = devices [0];
			Assert.AreEqual ("emulator-5554", device.ItemSpec, "Device serial should match");
			Assert.AreEqual ("Emulator", device.GetMetadata ("Type"), "Type should be Emulator");
			Assert.AreEqual ("Online", device.GetMetadata ("Status"), "Status should be Online");
			Assert.AreEqual ("Pixel 7 - API 35", device.GetMetadata ("Description"), "Description should be replaced with AVD name");
			Assert.AreEqual ("pixel_7_-_api_35", device.GetMetadata ("AvdName"), "AvdName should be raw AVD name");
			Assert.AreEqual ("sdk_gphone64_arm64", device.GetMetadata ("Model"), "Model metadata should be present");
			Assert.AreEqual ("1", device.GetMetadata ("TransportId"), "TransportId should be present");
		}

		[Test]
		public void ParseSinglePhysicalDevice ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var lines = new List<string> {
				"List of devices attached",
				"0A041FDD400327         device usb:1-1 product:raven model:Pixel_6_Pro device:raven transport_id:2"
			};

			var devices = ParseAdbDevicesOutput (task, lines);

			Assert.AreEqual (1, devices.Length, "Should return one device");
			var device = devices [0];
			Assert.AreEqual ("0A041FDD400327", device.ItemSpec, "Device serial should match");
			Assert.AreEqual ("Device", device.GetMetadata ("Type"), "Type should be Device");
			Assert.AreEqual ("Online", device.GetMetadata ("Status"), "Status should be Online");
			Assert.AreEqual ("Pixel 6 Pro", device.GetMetadata ("Description"), "Description should be cleaned up");
			Assert.AreEqual ("Pixel_6_Pro", device.GetMetadata ("Model"), "Model metadata should be present");
			Assert.AreEqual ("raven", device.GetMetadata ("Product"), "Product metadata should be present");
			Assert.AreEqual ("2", device.GetMetadata ("TransportId"), "TransportId should be present");
		}

		[Test]
		public void ParseMultipleDevices ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var lines = new List<string> {
				"List of devices attached",
				"emulator-5554          device product:sdk_gphone64_arm64 model:sdk_gphone64_arm64 device:emu64a transport_id:1",
				"emulator-5556          device product:sdk_gphone64_x86_64 model:sdk_gphone64_x86_64 device:emu64x transport_id:3",
				"0A041FDD400327         device usb:1-1 product:raven model:Pixel_6_Pro device:raven transport_id:2"
			};

			var devices = ParseAdbDevicesOutput (task, lines);

			Assert.AreEqual (3, devices.Length, "Should return three devices");

			Assert.AreEqual ("emulator-5554", devices [0].ItemSpec);
			Assert.AreEqual ("Emulator", devices [0].GetMetadata ("Type"));
			Assert.AreEqual ("Online", devices [0].GetMetadata ("Status"));
			Assert.AreEqual ("Pixel 7 - API 35", devices [0].GetMetadata ("Description"), "Emulator should have AVD name");

			Assert.AreEqual ("emulator-5556", devices [1].ItemSpec);
			Assert.AreEqual ("Emulator", devices [1].GetMetadata ("Type"));
			Assert.AreEqual ("Online", devices [1].GetMetadata ("Status"));
			Assert.AreEqual ("Pixel 9 Pro XL", devices [1].GetMetadata ("Description"), "Emulator should have AVD name");

			Assert.AreEqual ("0A041FDD400327", devices [2].ItemSpec);
			Assert.AreEqual ("Device", devices [2].GetMetadata ("Type"));
			Assert.AreEqual ("Online", devices [2].GetMetadata ("Status"));
			Assert.AreEqual ("Pixel 6 Pro", devices [2].GetMetadata ("Description"), "Physical device should use model name");
		}

		[Test]
		public void ParseOfflineDevice ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var lines = new List<string> {
				"List of devices attached",
				"emulator-5554          offline product:sdk_gphone64_arm64 model:sdk_gphone64_arm64 device:emu64a transport_id:1"
			};

			var devices = ParseAdbDevicesOutput (task, lines);

			Assert.AreEqual (1, devices.Length, "Should return one device");
			var device = devices [0];
			Assert.AreEqual ("emulator-5554", device.ItemSpec);
			Assert.AreEqual ("Offline", device.GetMetadata ("Status"), "Status should be Offline");
			Assert.AreEqual ("Pixel 7 - API 35", device.GetMetadata ("Description"), "Offline emulator should still get AVD name");
		}

		[Test]
		public void ParseUnauthorizedDevice ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var lines = new List<string> {
				"List of devices attached",
				"0A041FDD400327         unauthorized usb:1-1"
			};

			var devices = ParseAdbDevicesOutput (task, lines);

			Assert.AreEqual (1, devices.Length, "Should return one device");
			var device = devices [0];
			Assert.AreEqual ("0A041FDD400327", device.ItemSpec);
			Assert.AreEqual ("Unauthorized", device.GetMetadata ("Status"), "Status should be Unauthorized");
			Assert.AreEqual ("Device", device.GetMetadata ("Type"));
		}

		[Test]
		public void ParseNoPermissionsDevice ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var lines = new List<string> {
				"List of devices attached",
				"????????????????       no permissions usb:1-1"
			};

			var devices = ParseAdbDevicesOutput (task, lines);

			Assert.AreEqual (1, devices.Length, "Should return one device");
			var device = devices [0];
			Assert.AreEqual ("????????????????", device.ItemSpec);
			Assert.AreEqual ("NoPermissions", device.GetMetadata ("Status"), "Status should be NoPermissions");
		}

		[Test]
		public void ParseDeviceWithMinimalMetadata ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var lines = new List<string> {
				"List of devices attached",
				"ABC123                 device"
			};

			var devices = ParseAdbDevicesOutput (task, lines);

			Assert.AreEqual (1, devices.Length, "Should return one device");
			var device = devices [0];
			Assert.AreEqual ("ABC123", device.ItemSpec);
			Assert.AreEqual ("Device", device.GetMetadata ("Type"));
			Assert.AreEqual ("Online", device.GetMetadata ("Status"));
			Assert.AreEqual ("ABC123", device.GetMetadata ("Description"), "Description should fall back to serial");
		}

		[Test]
		public void ParseDeviceWithProductOnly ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var lines = new List<string> {
				"List of devices attached",
				"emulator-5554          device product:aosp_x86_64"
			};

			var devices = ParseAdbDevicesOutput (task, lines);

			Assert.AreEqual (1, devices.Length, "Should return one device");
			var device = devices [0];
			Assert.AreEqual ("Pixel 7 - API 35", device.GetMetadata ("Description"), "Emulator should get AVD name replacement");
			Assert.AreEqual ("aosp_x86_64", device.GetMetadata ("Product"));
		}

		[Test]
		public void ParseInvalidLines ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var lines = new List<string> {
				"List of devices attached",
				"",
				"   ",
				"Some random text",
				"* daemon not running; starting now at tcp:5037",
				"* daemon started successfully",
				"emulator-5554          device product:sdk_gphone64_arm64 model:sdk_gphone64_arm64 device:emu64a transport_id:1"
			};

			var devices = ParseAdbDevicesOutput (task, lines);

			Assert.AreEqual (1, devices.Length, "Should only return valid device lines");
			Assert.AreEqual ("emulator-5554", devices [0].ItemSpec);
			Assert.AreEqual ("Pixel 7 - API 35", devices [0].GetMetadata ("Description"), "Emulator should have AVD name");
		}

		[Test]
		public void ParseMixedDeviceStates ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var lines = new List<string> {
				"List of devices attached",
				"emulator-5554          device product:sdk_gphone64_arm64 model:Pixel_7 device:emu64a",
				"emulator-5556          offline",
				"0A041FDD400327         device usb:1-1 product:raven model:Pixel_6_Pro",
				"0B123456789ABC         unauthorized usb:1-2"
			};

			var devices = ParseAdbDevicesOutput (task, lines);

			Assert.AreEqual (4, devices.Length, "Should return all devices regardless of state");

			Assert.AreEqual ("Online", devices [0].GetMetadata ("Status"));
			Assert.AreEqual ("Pixel 7 - API 35", devices [0].GetMetadata ("Description"), "Emulator should have AVD name");

			Assert.AreEqual ("Offline", devices [1].GetMetadata ("Status"));
			Assert.AreEqual ("Pixel 9 Pro XL", devices [1].GetMetadata ("Description"), "Offline emulator should still get AVD name");

			Assert.AreEqual ("Online", devices [2].GetMetadata ("Status"));
			Assert.AreEqual ("Pixel 6 Pro", devices [2].GetMetadata ("Description"));

			Assert.AreEqual ("Unauthorized", devices [3].GetMetadata ("Status"));
		}

		[Test]
		public void ParseDeviceWithSpecialCharacters ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var lines = new List<string> {
				"List of devices attached",
				"192.168.1.100:5555     device product:sdk_gphone64_arm64 model:Remote_Device"
			};

			var devices = ParseAdbDevicesOutput (task, lines);

			Assert.AreEqual (1, devices.Length, "Should handle IP:port format devices");
			var device = devices [0];
			Assert.AreEqual ("192.168.1.100:5555", device.ItemSpec);
			Assert.AreEqual ("Device", device.GetMetadata ("Type"), "IP devices should be classified as Device not Emulator");
			Assert.AreEqual ("Remote Device", device.GetMetadata ("Description"));
		}

		[Test]
		public void DescriptionPriorityOrder ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			// Test that model takes priority over product
			var lines1 = new List<string> {
				"List of devices attached",
				"device1                device product:product_name model:model_name device:device_name"
			};
			var devices1 = ParseAdbDevicesOutput (task, lines1);
			Assert.AreEqual ("model name", devices1 [0].GetMetadata ("Description"), "Model should have highest priority");

			// Test that product takes priority over device name when model is missing
			var lines2 = new List<string> {
				"List of devices attached",
				"device2                device product:product_name device:device_name"
			};
			var devices2 = ParseAdbDevicesOutput (task, lines2);
			Assert.AreEqual ("product name", devices2 [0].GetMetadata ("Description"), "Product should have second priority");

			// Test that device name is used when model and product are missing
			var lines3 = new List<string> {
				"List of devices attached",
				"device3                device device:device_name"
			};
			var devices3 = ParseAdbDevicesOutput (task, lines3);
			Assert.AreEqual ("device name", devices3 [0].GetMetadata ("Description"), "Device should have third priority");
		}

		[Test]
		public void ParseAdbDaemonStarting ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			// Output when adb daemon is not running and starting up
			var lines = new List<string> {
				"* daemon not running; starting now at tcp:5037",
				"* daemon started successfully",
				"List of devices attached",
				"emulator-5554          device product:sdk_gphone64_arm64 model:sdk_gphone64_arm64 device:emu64a transport_id:1",
				"0A041FDD400327         device usb:1-1 product:raven model:Pixel_6_Pro device:raven transport_id:2"
			};

			var devices = ParseAdbDevicesOutput (task, lines);

			Assert.AreEqual (2, devices.Length, "Should parse devices even with daemon startup messages");

			// First device - emulator
			var emulator = devices [0];
			Assert.AreEqual ("emulator-5554", emulator.ItemSpec);
			Assert.AreEqual ("Pixel 7 - API 35", emulator.GetMetadata ("Description"), "Emulator should get AVD name");
			Assert.AreEqual ("Emulator", emulator.GetMetadata ("Type"));
			Assert.AreEqual ("Online", emulator.GetMetadata ("Status"));

			// Second device - physical device
			var physical = devices [1];
			Assert.AreEqual ("0A041FDD400327", physical.ItemSpec);
			Assert.AreEqual ("Pixel 6 Pro", physical.GetMetadata ("Description"));
			Assert.AreEqual ("Device", physical.GetMetadata ("Type"));
			Assert.AreEqual ("Online", physical.GetMetadata ("Status"));
		}

		[Test]
		public void FormatDisplayName_ReplacesUnderscoresWithSpaces ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var result = task.FormatDisplayName ("emulator-5554", "pixel_7_pro");

			Assert.AreEqual ("Pixel 7 Pro", result, "Should replace underscores with spaces");
		}

		[Test]
		public void FormatDisplayName_AppliesTitleCase ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var result = task.FormatDisplayName ("emulator-5554", "pixel 7 pro");

			Assert.AreEqual ("Pixel 7 Pro", result, "Should apply title case");
		}

		[Test]
		public void FormatDisplayName_ReplacesApiWithAPIUppercase ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var result = task.FormatDisplayName ("emulator-5554", "pixel_5_api_34");

			Assert.AreEqual ("Pixel 5 API 34", result, "Should replace 'Api' with 'API'");
		}

		[Test]
		public void FormatDisplayName_HandlesMultipleApiOccurrences ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var result = task.FormatDisplayName ("emulator-5554", "test_api_device_api_35");

			Assert.AreEqual ("Test API Device API 35", result, "Should replace all 'Api' occurrences with 'API'");
		}

		[Test]
		public void FormatDisplayName_HandlesMixedCaseInput ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var result = task.FormatDisplayName ("emulator-5554", "PiXeL_7_API_35");

			Assert.AreEqual ("Pixel 7 API 35", result, "Should normalize mixed case input");
		}

		[Test]
		public void FormatDisplayName_HandlesComplexNames ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var result = task.FormatDisplayName ("emulator-5554", "pixel_9_pro_xl_api_36");

			Assert.AreEqual ("Pixel 9 Pro Xl API 36", result, "Should format complex names correctly");
		}

		[Test]
		public void FormatDisplayName_PreservesNumbersAndSpecialChars ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var result = task.FormatDisplayName ("emulator-5554", "pixel_7-pro_api_35");

			Assert.AreEqual ("Pixel 7-Pro API 35", result, "Should preserve hyphens and numbers");
		}

		[Test]
		public void FormatDisplayName_HandlesEmptyString ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var result = task.FormatDisplayName ("emulator-5554", "");

			Assert.AreEqual ("", result, "Should handle empty string");
		}

		[Test]
		public void FormatDisplayName_HandlesSingleWord ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var result = task.FormatDisplayName ("emulator-5554", "pixel");

			Assert.AreEqual ("Pixel", result, "Should capitalize single word");
		}

		[Test]
		public void FormatDisplayName_DoesNotReplaceApiInsideWords ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var result = task.FormatDisplayName ("emulator-5554", "erapidevice");

			Assert.AreEqual ("Erapidevice", result, "Should not replace 'api' when it's part of a larger word");
		}

		[Test]
		public void MergeDevicesAndEmulators_NoEmulators_ReturnsAdbDevicesOnly ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var adbDevices = new List<ITaskItem> {
				CreateDeviceItem ("0A041FDD400327", "Pixel 5", "Device", "Online"),
			};
			var availableEmulators = new List<string> ();

			var result = task.MergeDevicesAndEmulators (adbDevices, availableEmulators);

			Assert.AreEqual (1, result.Count, "Should return only adb devices");
			Assert.AreEqual ("0A041FDD400327", result [0].ItemSpec);
		}

		[Test]
		public void MergeDevicesAndEmulators_NoRunningEmulators_AddsAllAvailableEmulators ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var adbDevices = new List<ITaskItem> {
				CreateDeviceItem ("0A041FDD400327", "Pixel 5", "Device", "Online"),
			};
			var availableEmulators = new List<string> { "pixel_7_api_35", "pixel_9_api_36" };

			var result = task.MergeDevicesAndEmulators (adbDevices, availableEmulators);

			Assert.AreEqual (3, result.Count, "Should return adb device + 2 available emulators");

			// First item: physical device (online, sorted first)
			Assert.AreEqual ("0A041FDD400327", result [0].ItemSpec);

			// Second item: non-running emulator (sorted alphabetically by description)
			Assert.AreEqual ("pixel_7_api_35", result [1].ItemSpec, "Non-running emulator ItemSpec should be AVD name");
			Assert.AreEqual ("Emulator", result [1].GetMetadata ("Type"));
			Assert.AreEqual ("NotRunning", result [1].GetMetadata ("Status"));
			Assert.AreEqual ("pixel_7_api_35", result [1].GetMetadata ("AvdName"));
			Assert.AreEqual ("Pixel 7 API 35 (Not Running)", result [1].GetMetadata ("Description"));

			// Third item: non-running emulator
			Assert.AreEqual ("pixel_9_api_36", result [2].ItemSpec);
			Assert.AreEqual ("NotRunning", result [2].GetMetadata ("Status"));
			Assert.AreEqual ("Pixel 9 API 36 (Not Running)", result [2].GetMetadata ("Description"));
		}

		[Test]
		public void MergeDevicesAndEmulators_RunningEmulator_NoDuplicate ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			// Emulator is running (has adb entry with AvdName metadata)
			var runningEmulator = CreateDeviceItem ("emulator-5554", "Pixel 7 API 35", "Emulator", "Online");
			runningEmulator.SetMetadata ("AvdName", "pixel_7_api_35");

			var adbDevices = new List<ITaskItem> { runningEmulator };
			var availableEmulators = new List<string> { "pixel_7_api_35" };

			var result = task.MergeDevicesAndEmulators (adbDevices, availableEmulators);

			Assert.AreEqual (1, result.Count, "Should not duplicate running emulator");
			Assert.AreEqual ("emulator-5554", result [0].ItemSpec, "Should keep the running emulator entry");
			Assert.AreEqual ("Online", result [0].GetMetadata ("Status"));
		}

		[Test]
		public void MergeDevicesAndEmulators_MixedRunningAndNotRunning ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			// One emulator is running
			var runningEmulator = CreateDeviceItem ("emulator-5554", "Pixel 7 API 35", "Emulator", "Online");
			runningEmulator.SetMetadata ("AvdName", "pixel_7_api_35");

			var physicalDevice = CreateDeviceItem ("0A041FDD400327", "Pixel 5", "Device", "Online");

			var adbDevices = new List<ITaskItem> { runningEmulator, physicalDevice };
			var availableEmulators = new List<string> { "pixel_7_api_35", "pixel_9_api_36", "nexus_5_api_30" };

			var result = task.MergeDevicesAndEmulators (adbDevices, availableEmulators);

			Assert.AreEqual (4, result.Count, "Should have: 1 running emulator + 1 device + 2 non-running emulators");

			// Online devices come first, sorted alphabetically by description
			Assert.AreEqual ("0A041FDD400327", result [0].ItemSpec);
			Assert.AreEqual ("Online", result [0].GetMetadata ("Status"));

			Assert.AreEqual ("emulator-5554", result [1].ItemSpec);
			Assert.AreEqual ("Online", result [1].GetMetadata ("Status"));

			// Non-running emulators come second, sorted alphabetically by description
			Assert.AreEqual ("nexus_5_api_30", result [2].ItemSpec);
			Assert.AreEqual ("NotRunning", result [2].GetMetadata ("Status"));
			Assert.AreEqual ("Nexus 5 API 30 (Not Running)", result [2].GetMetadata ("Description"));

			Assert.AreEqual ("pixel_9_api_36", result [3].ItemSpec);
			Assert.AreEqual ("NotRunning", result [3].GetMetadata ("Status"));
			Assert.AreEqual ("Pixel 9 API 36 (Not Running)", result [3].GetMetadata ("Description"));
		}

		[Test]
		public void MergeDevicesAndEmulators_CaseInsensitiveAvdNameMatching ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			// Running emulator with different case
			var runningEmulator = CreateDeviceItem ("emulator-5554", "Pixel 7 API 35", "Emulator", "Online");
			runningEmulator.SetMetadata ("AvdName", "Pixel_7_API_35");

			var adbDevices = new List<ITaskItem> { runningEmulator };
			var availableEmulators = new List<string> { "pixel_7_api_35" }; // lowercase

			var result = task.MergeDevicesAndEmulators (adbDevices, availableEmulators);

			Assert.AreEqual (1, result.Count, "Should match AVD names case-insensitively");
			Assert.AreEqual ("emulator-5554", result [0].ItemSpec);
		}

		[Test]
		public void MergeDevicesAndEmulators_EmptyAdbDevices_ReturnsAllAvailableEmulators ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var adbDevices = new List<ITaskItem> ();
			var availableEmulators = new List<string> { "pixel_7_api_35", "pixel_9_api_36" };

			var result = task.MergeDevicesAndEmulators (adbDevices, availableEmulators);

			Assert.AreEqual (2, result.Count, "Should return all available emulators");
			Assert.AreEqual ("pixel_7_api_35", result [0].ItemSpec);
			Assert.AreEqual ("Pixel 7 API 35 (Not Running)", result [0].GetMetadata ("Description"));
			Assert.AreEqual ("pixel_9_api_36", result [1].ItemSpec);
			Assert.AreEqual ("Pixel 9 API 36 (Not Running)", result [1].GetMetadata ("Description"));
		}

		[Test]
		public void MergeDevicesAndEmulators_AllEmulatorsRunning_NoDuplicates ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var emulator1 = CreateDeviceItem ("emulator-5554", "Pixel 7 API 35", "Emulator", "Online");
			emulator1.SetMetadata ("AvdName", "pixel_7_api_35");

			var emulator2 = CreateDeviceItem ("emulator-5556", "Pixel 9 API 36", "Emulator", "Online");
			emulator2.SetMetadata ("AvdName", "pixel_9_api_36");

			var adbDevices = new List<ITaskItem> { emulator1, emulator2 };
			var availableEmulators = new List<string> { "pixel_7_api_35", "pixel_9_api_36" };

			var result = task.MergeDevicesAndEmulators (adbDevices, availableEmulators);

			Assert.AreEqual (2, result.Count, "Should not add duplicates when all emulators are running");
			Assert.AreEqual ("Pixel 7 API 35", result [0].GetMetadata ("Description"), "First should be alphabetically first");
			Assert.AreEqual ("Pixel 9 API 36", result [1].GetMetadata ("Description"), "Second should be alphabetically second");
			Assert.IsTrue (result.TrueForAll (d => d.GetMetadata ("Status") == "Online"), "All should be Online (running)");
		}

		[Test]
		public void MergeDevicesAndEmulators_NonRunningEmulatorHasFormattedDescription ()
		{
			var task = new MockGetAvailableAndroidDevices {
				BuildEngine = engine,
			};

			var adbDevices = new List<ITaskItem> ();
			var availableEmulators = new List<string> { "pixel_7_pro_api_35" };

			var result = task.MergeDevicesAndEmulators (adbDevices, availableEmulators);

			Assert.AreEqual (1, result.Count);
			Assert.AreEqual ("Pixel 7 Pro API 35 (Not Running)", result [0].GetMetadata ("Description"), "Description should be formatted with (Not Running) suffix");
		}

		/// <summary>
		/// Helper method to create a device ITaskItem for testing
		/// </summary>
		static ITaskItem CreateDeviceItem (string serial, string description, string type, string status)
		{
			var item = new TaskItem (serial);
			item.SetMetadata ("Description", description);
			item.SetMetadata ("Type", type);
			item.SetMetadata ("Status", status);
			return item;
		}
	}
}
