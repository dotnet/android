using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using Xamarin.Installer.Common;

namespace Xamarin.Installer.AndroidSDK.Manager
{
	public class ConfigManager
	{
		const string AppName = "AndroidSDKManager";
		const string PropertiesRootNode = "SDKManagerProperties";
		const string PropertyNode = "Property";

		Dictionary<string, string> properties;

		/// <summary>
		/// SDK Manager configuration folder path
		/// </summary>
		/// <value></value>
		public static string ConfigFolder {
			get {
				string configFolder;
				if (Platform.IsMac) {
					var home = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
					configFolder = Path.Combine (home, "Library", "Preferences");
				} else {
					configFolder = Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData);
					if (Platform.IsWindows)
						configFolder = Path.Combine (configFolder, "Config");
				}
				return Path.Combine (configFolder, AppName);
			}
		}

		string filePath;
		string FilePath {
			get {
				if (filePath == null)
					filePath = Path.Combine (ConfigFolder, "properties.xml");

				return filePath;
			}
		}

		internal ConfigManager ()
		{
			LoadProperties ();
		}

		internal void SetProperty (string key, string value)
		{
			if (properties.ContainsKey (key)) {
				if (String.Compare (properties [key], value, StringComparison.Ordinal) == 0)
					return;
				properties.Remove (key);
			}

			properties.Add (key, value);
		}

		internal string GetProperty (string key, string defaultValue)
		{
			bool success = properties.TryGetValue (key, out string value);
			return success ? value : defaultValue;
		}

		void LoadProperties ()
		{
			properties = new Dictionary<string, string> (StringComparer.Ordinal);
			if (!File.Exists (FilePath))
				return;

			try
			{
				var element = XElement.Load (FilePath);
				foreach (var node in element.Descendants (PropertyNode)) {
					var key = node.Attribute ("key").Value;
					var value = node.Attribute ("value").Value;
					if (String.IsNullOrEmpty (key))
						continue;

					properties.Add (key, value);
				}
			}
			catch (XmlException ex)
			{
				// https://devdiv.visualstudio.com/DevDiv/_workitems/edit/723620
				// Overwrite the broken config with an empty xml (we write default property values there later)
				Logger.Warning ($"Failed to read the config, resetting it.\nError details: {ex}");
				SaveProperties ();
			}
		}

		
		internal void SaveProperties ()
		{
			var filePath = FilePath;
			if (!File.Exists (filePath)) {
				var dir = Path.GetDirectoryName (filePath);
				if (!Directory.Exists (dir))
					Directory.CreateDirectory (dir);
			}
			XmlWriter xmlWriter = XmlWriter.Create(FilePath);

			xmlWriter.WriteStartDocument();
			xmlWriter.WriteStartElement(PropertiesRootNode);
			foreach (var keyValue in properties) {
				xmlWriter.WriteStartElement(PropertyNode);
				xmlWriter.WriteAttributeString ("key", keyValue.Key);
				xmlWriter.WriteAttributeString ("value", keyValue.Value);
			}
			xmlWriter.WriteEndElement();
			xmlWriter.Close();
		}
	}
}