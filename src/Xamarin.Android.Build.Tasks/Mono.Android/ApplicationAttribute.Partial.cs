using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Mono.Cecil;

using MonoDroid.Utils;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.TypeNameMappings;

using Xamarin.Android.Manifest;

using Android.Content.PM;

namespace Android.App {

	partial class ApplicationAttribute {

		string _BackupAgent;
		bool _BackupInForeground;
		string _Banner;
		bool _FullBackupOnly;
		string _Logo;
		string _ManageSpaceActivity;
		string _NetworkSecurityConfig;
		string _RequiredAccountType;
		string _RestrictedAccountType;
		bool _HardwareAccelerated;
		bool _ExtractNativeLibs;
		bool _FullBackupContent;
		bool _LargeHeap;
		UiOptions _UiOptions;
		bool _SupportsRtl;
		bool _UsesCleartextTraffic;
		bool _VMSafeMode;
		bool _ResizeableActivity;
		ICustomAttributeProvider provider;
		string _RoundIcon;

		static ManifestDocumentElement<ApplicationAttribute> mapping = new ManifestDocumentElement<ApplicationAttribute> ("application") {
			{
			  "AllowBackup",
			  "allowBackup",
			  self          => self.AllowBackup,
			  (self, value) => self.AllowBackup   = (bool) value
			}, {
			  "AllowClearUserData",
			  "allowClearUserData",
			  self          => self.AllowClearUserData,
			  (self, value) => self.AllowClearUserData  = (bool) value
			}, {
			  "AllowTaskReparenting",
			  "allowTaskReparenting",
			  self          => self.AllowTaskReparenting,
			  (self, value) => self.AllowTaskReparenting  = (bool) value
			}, {
			  "BackupAgent",
			  "backupAgent",
			  (self, value) => self._BackupAgent  = (string) value,
				(self, p, r, cache)  => {
					var typeDef = ManifestDocumentElement.ResolveType (self._BackupAgent, p, r);

					if (!typeDef.IsSubclassOf ("Android.App.Backup.BackupAgent", cache))
						throw new InvalidOperationException (
								string.Format ("The Type '{0}', referenced by the Android.App.ApplicationAttribute.BackupAgent property, must be a subclass of the type Android.App.Backup.BackupAgent.",
									typeDef.FullName));

					return ManifestDocumentElement.ToString (typeDef, cache);
			  }
			}, {
			  "BackupInForeground",
			  "backupInForeground",
			  self          => self._BackupInForeground,
			  (self, value) => self._BackupInForeground = (bool) value
			}, {
			  "Banner",
			  "banner",
			  self          => self._Banner,
			  (self, value) => self._Banner  = (string) value
			}, {
			  "Debuggable",
			  "debuggable",
			  self          => self.Debuggable,
			  (self, value) => self.Debuggable  = (bool) value
			}, {
			  "Description",
			  "description",
			  self          => self.Description,
			  (self, value) => self.Description = (string) value
			}, {
			  "DirectBootAware",
			  "directBootAware",
			  self          => self.DirectBootAware,
			  (self, value) => self.DirectBootAware = (bool) value
			}, {
			  "Enabled",
			  "enabled",
			  self          => self.Enabled,
			  (self, value) => self.Enabled = (bool) value
			}, {
			  "ExtractNativeLibs",
			  "extractNativeLibs",
			  self          => self._ExtractNativeLibs,
			  (self, value) => self._ExtractNativeLibs  = (bool) value
			}, {
			  "FullBackupContent",
			  "fullBackupContent",
			  self          => self._FullBackupContent,
			  (self, value) => self._FullBackupContent  = (bool) value
			}, {
			  "FullBackupOnly",
			  "fullBackupOnly",
			  self          => self._FullBackupOnly,
			  (self, value) => self._FullBackupOnly = (bool) value
			}, {
			  "HardwareAccelerated",
			  "hardwareAccelerated",
			  self          => self._HardwareAccelerated,
			  (self, value) => self._HardwareAccelerated  = (bool) value
			}, {
			  "HasCode",
			  "hasCode",
			  self          => self.HasCode,
			  (self, value) => self.HasCode = (bool) value
			}, {
			  "Icon",
			  "icon",
			  self          => self.Icon,
			  (self, value) => self.Icon  = (string) value
			}, {
			  "KillAfterRestore",
			  "killAfterRestore",
			  self          => self.KillAfterRestore,
			  (self, value) => self.KillAfterRestore  = (bool) value
			}, {
			  "Label",
			  "label",
			  self          => self.Label,
			  (self, value) => self.Label = (string) value
			}, {
			  "LargeHeap",
			  "largeHeap",
			  self          => self._LargeHeap,
			  (self, value) => self._LargeHeap = (bool) value
			}, {
			  "Logo",
			  "logo",
			  self          => self._Logo,
			  (self, value) => self._Logo  = (string) value
			}, {
			  "ManageSpaceActivity",
			  "manageSpaceActivity",
			  self          => self._ManageSpaceActivity,
			  (self, value) => self._ManageSpaceActivity  = (string) value,
			  typeof (Type)
			}, {
			  "Name",
			  "name",
			  (self, value) => self.Name  = (string) value,
			  ToNameAttribute
			}, {
			  "NetworkSecurityConfig",
			  "networkSecurityConfig",
			  self          => self._NetworkSecurityConfig,
			  (self, value) => self._NetworkSecurityConfig = (string) value
			}, {
			  "Permission",
			  "permission",
			  self          => self.Permission,
			  (self, value) => self.Permission  = (string) value
			}, {
			  "Persistent",
			  "persistent",
			  self          => self.Persistent,
			  (self, value) => self.Persistent  = (bool) value
			}, {
			  "Process",
			  "process",
			  self          => self.Process,
			  (self, value) => self.Process = (string) value
			}, {
			  "ResizeableActivity",
			  "resizeableActivity",
			  self          => self._ResizeableActivity,
			  (self, value) => self._ResizeableActivity = (bool) value
			}, {
			  "RequiredAccountType",
			  "requiredAccountType",
			  self          => self._RequiredAccountType,
			  (self, value) => self._RequiredAccountType  = (string) value
			}, {
			  "RestoreAnyVersion",
			  "restoreAnyVersion",
			  self          => self.RestoreAnyVersion,
			  (self, value) => self.RestoreAnyVersion = (bool) value
			}, {
			  "RestrictedAccountType",
			  "restrictedAccountType",
			  self          => self._RestrictedAccountType,
			  (self, value) => self._RestrictedAccountType  = (string) value
			}, {
			  "RoundIcon",
			  "roundIcon",
			  self          => self._RoundIcon,
			  (self, value) => self._RoundIcon  = (string) value
			}, {
			  "SupportsRtl",
			  "supportsRtl",
			  self          => self._SupportsRtl,
			  (self, value) => self._SupportsRtl = (bool) value
			}, {
			  "TaskAffinity",
			  "taskAffinity",
			  self          => self.TaskAffinity,
			  (self, value) => self.TaskAffinity  = (string) value
			}, {
			  "Theme",
			  "theme",
			  self          => self.Theme,
			  (self, value) => self.Theme = (string) value
			}, {
			  "UiOptions",
			  "uiOptions",
			  self          => self._UiOptions,
			  (self, value) => self._UiOptions  = (UiOptions) value
			}, {
			  "UsesCleartextTraffic",
			  "usesCleartextTraffic",
			  self          => self._UsesCleartextTraffic,
			  (self, value) => self._UsesCleartextTraffic = (bool) value
			}, {
			  "VMSafeMode",
			  "vmSafeMode",
			  self          => self._VMSafeMode,
			  (self, value) => self._VMSafeMode = (bool) value
			},
		};

		ICollection<string> specified;

		public static ApplicationAttribute FromCustomAttributeProvider (ICustomAttributeProvider provider, TypeDefinitionCache cache)
		{
			CustomAttribute attr = provider.GetCustomAttributes ("Android.App.ApplicationAttribute")
				.SingleOrDefault ();
			if (attr == null)
				return null;
			ApplicationAttribute self = new ApplicationAttribute () {
				provider  = provider,
			};
			self.specified = mapping.Load (self, attr, cache);
			if (provider is TypeDefinition) {
				self.specified.Add ("Name");
			}
			return self;
		}

		internal XElement ToElement (IAssemblyResolver resolver, string packageName, TypeDefinitionCache cache)
		{
			return mapping.ToElement (this, specified, packageName, cache, provider, resolver);
		}

		static string ToNameAttribute (ApplicationAttribute self, ICustomAttributeProvider provider, IAssemblyResolver resolver, TypeDefinitionCache cache)
		{
			var type = self.provider as TypeDefinition;
			if (string.IsNullOrEmpty (self.Name) && type != null)
				return JavaNativeTypeManager.ToJniName (type, cache).Replace ('/', '.');

			return self.Name;
		}
	}
}
