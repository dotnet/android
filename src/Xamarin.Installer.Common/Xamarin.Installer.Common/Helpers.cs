using Microsoft.Win32;
using System;
using System.IO;


namespace Xamarin.Installer.Common
{
	public static class Helpers
	{
		const string INSTALLER_MACHINE_ID_REGISTRY_KVALUE = "IMID";
		static bool onWindows;
		public static readonly string UserApplicationDataPath = Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData);
		public static readonly string XamarinBaseRegistryKValue = "SOFTWARE\\Xamarin\\MonoAndroid";

		static Helpers ()
		{
			onWindows = Environment.OSVersion.Platform != PlatformID.MacOSX && Environment.OSVersion.Platform != PlatformID.Unix;
		}

		public static Guid GetMachineID ()
		{
			if (onWindows)
				return WindowsGetMachineID ();
			else
				return UnixGetMachineID ();
		}

		static Guid UnixGetMachineID ()
		{
			string idPath = Path.Combine (UserApplicationDataPath, "Xamarin", ".machineid");
			Guid machineId;

			try {
				if (!File.Exists (idPath))
					return GenerateAndSaveId (idPath);

				string guid = File.ReadAllText (idPath).Trim ();
				if (String.IsNullOrEmpty (guid))
					return GenerateAndSaveId (idPath);
				else if (!Guid.TryParse (guid, out machineId))
					return GenerateAndSaveId (idPath);
				return machineId;
			} catch (Exception) {
				// ignore
				return Guid.NewGuid ();
			}
		}

		static Guid WindowsGetMachineID ()
		{
			string uuid;
			Guid machineId;
			if (!GetRegistryKeyValue (XamarinBaseRegistryKValue, INSTALLER_MACHINE_ID_REGISTRY_KVALUE, out uuid, RegistryHive.CurrentUser) || String.IsNullOrEmpty (uuid)) {
				machineId = Guid.NewGuid ();
				SetRegistryKeyValue (XamarinBaseRegistryKValue, INSTALLER_MACHINE_ID_REGISTRY_KVALUE, machineId.ToString (), RegistryValueKind.String, RegistryHive.CurrentUser);
			} else {
				if (!Guid.TryParse (uuid, out machineId) || machineId == Guid.Empty)
					machineId = Guid.NewGuid ();
			}

			return machineId;
		}

		static void SaveIdToFile (string idPath, Guid machineId)
		{
			string xamarinDir = Path.GetDirectoryName (idPath);
			if (!Directory.Exists (xamarinDir))
				Directory.CreateDirectory (xamarinDir);
			File.WriteAllText (idPath, machineId.ToString ());
		}

		static Guid GenerateAndSaveId (string idPath)
		{
			var machineId = Guid.NewGuid ();
			SaveIdToFile (idPath, machineId);
			return machineId;
		}

		public static bool GetRegistryKeyValue<T> (string subKeyPath, string keyName, out T result, RegistryHive hive = RegistryHive.LocalMachine, bool check64Node = true)
		{
			result = default (T);
			if (String.IsNullOrEmpty (subKeyPath))
				return false;

			string wow3264Value = null;
			if (check64Node && hive == RegistryHive.LocalMachine) {
				int offset = 0;
				if (subKeyPath [0] == '\\')
					offset++;
				if (subKeyPath.StartsWith ("SOFTWARE\\", StringComparison.OrdinalIgnoreCase) || subKeyPath.StartsWith ("\\SOFTWARE\\", StringComparison.OrdinalIgnoreCase)) {
					offset += 8;
					wow3264Value = "SOFTWARE\\Wow6432Node\\" + subKeyPath.Substring (offset + 1);
				}
			}

			if (!String.IsNullOrEmpty (wow3264Value) && GetRegistryKeyValueInternal(wow3264Value, keyName, out result, hive))
				return true;
			return GetRegistryKeyValueInternal(subKeyPath, keyName, out result, hive);
		}

		static bool GetRegistryKeyValueInternal<T> (string subKeyPath, string keyName, out T result, RegistryHive hive = RegistryHive.LocalMachine)
		{
			result = default (T);
			RegistryKey parent = HiveToKey (hive);
			using (RegistryKey rk = parent.OpenSubKey (subKeyPath, false)) {
				if (rk == null)
					return false;

				object o = rk.GetValue (keyName);
				if (o == null)
					return false;

				try {
					result = (T)Convert.ChangeType (o, typeof (T));
					return true;
				} catch {
					// ignore
				}

				return false;
			}
		}

		public static void SetRegistryKeyValue (string subKeyPath, string keyName, object value, RegistryValueKind kind = RegistryValueKind.String, RegistryHive hive = RegistryHive.CurrentUser)
		{
			if (String.IsNullOrEmpty (subKeyPath))
				throw new ArgumentException ("subKeyPath");
			if (String.IsNullOrEmpty (keyName))
				throw new ArgumentException ("keyName");

			RegistryKey parent = HiveToKey (hive);
			RegistryKey rk = null;
			try {
				rk = parent.OpenSubKey (subKeyPath, true);
				if (rk == null) {
					rk = parent.CreateSubKey (subKeyPath);
					if (rk == null)
						return;
				}

				rk.SetValue (keyName, value, kind);
			} finally {
				if (rk != null)
					rk.Dispose ();
			}
		}

		public static RegistryKey HiveToKey (RegistryHive hive)
		{
			switch (hive) {
			case RegistryHive.ClassesRoot:
				return Registry.ClassesRoot;

			case RegistryHive.CurrentConfig:
				return Registry.CurrentConfig;

			case RegistryHive.CurrentUser:
				return Registry.CurrentUser;

			case RegistryHive.LocalMachine:
				return Registry.LocalMachine;

			case RegistryHive.PerformanceData:
				return Registry.PerformanceData;

			case RegistryHive.Users:
				return Registry.Users;

			default:
				throw new InvalidOperationException ("Unknown registry hive");
			}
		}

	}
}
