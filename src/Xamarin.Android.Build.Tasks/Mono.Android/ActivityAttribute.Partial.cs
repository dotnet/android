using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Mono.Cecil;

using Java.Interop.Tools.Cecil;

using Xamarin.Android.Manifest;

using Android.Content.PM;
using Android.Views;

namespace Android.App {

	partial class ActivityAttribute {

		bool _AllowEmbedded;
		bool _HardwareAccelerated;
		string _ParentActivity;
		LayoutDirection _LayoutDirection;
		UiOptions _UiOptions;
		bool _Immersive;
		bool _ResizeableActivity;
		bool _SupportsPictureInPicture;
		string _RoundIcon;

		static ManifestDocumentElement<ActivityAttribute> mapping = new ManifestDocumentElement<ActivityAttribute> ("activity") {
			{
			  "AllowEmbedded",
			  "allowEmbedded",
			  self          => self._AllowEmbedded,
			  (self, value) => self._AllowEmbedded  = (bool) value
			}, {
			  "AllowTaskReparenting",
			  "allowTaskReparenting",
			  self          => self.AllowTaskReparenting,
			  (self, value) => self.AllowTaskReparenting  = (bool) value
			}, {
			  "AlwaysRetainTaskState",
			  "alwaysRetainTaskState",
			  self          => self.AlwaysRetainTaskState,
			  (self, value) => self.AlwaysRetainTaskState = (bool) value
			}, {
			  "ClearTaskOnLaunch",
			  "clearTaskOnLaunch",
			  self          => self.ClearTaskOnLaunch,
			  (self, value) => self.ClearTaskOnLaunch = (bool) value
			}, {
			  "ConfigurationChanges",
			  "configChanges",
			  self          => self.ConfigurationChanges,
			  (self, value) => self.ConfigurationChanges  = (ConfigChanges) value
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
			  "ExcludeFromRecents",
			  "excludeFromRecents",
			  self          => self.ExcludeFromRecents,
			  (self, value) => self.ExcludeFromRecents  = (bool) value
			}, {
			  "Exported",
			  "exported",
			  self          => self.Exported,
			  (self, value) => self.Exported  = (bool) value
			}, {
			  "FinishOnTaskLaunch",
			  "finishOnTaskLaunch",
			  self          => self.FinishOnTaskLaunch,
			  (self, value) => self.FinishOnTaskLaunch  = (bool) value
			}, {
			  "HardwareAccelerated",
			  "hardwareAccelerated",
			  self          => self._HardwareAccelerated,
			  (self, value) => self._HardwareAccelerated  = (bool) value
			}, {
			  "Icon",
			  "icon",
			  self          => self.Icon,
			  (self, value) => self.Icon  = (string) value
			}, {
			  "Immersive",
			  "immersive",
			  self          => self._Immersive,
			  (self, value) => self._Immersive = (bool) value
			}, {
			  "Label",
			  "label",
			  self          => self.Label,
			  (self, value) => self.Label = (string) value
			}, {
			  "LaunchMode",
			  "launchMode",
			  self          => self.LaunchMode,
			  (self, value) => self.LaunchMode  = (LaunchMode) value
			}, {
			  "LayoutDirection",
			  "layoutDirection",
			  self          => self._LayoutDirection,
			  (self, value) => self._LayoutDirection  = (LayoutDirection) value
			}, {
			  "MainLauncher",
			  null,
			  null,
			  (self, value) => self.MainLauncher  = (bool) value
			}, {
			  "MultiProcess",
			  "multiprocess",
			  self          => self.MultiProcess,
			  (self, value) => self.MultiProcess  = (bool) value
			}, {
			  "Name",
			  "name",
			  self          => self.Name,
			  (self, value) => self.Name  = (string) value
			}, {
			  "NoHistory",
			  "noHistory",
			  self          => self.NoHistory,
			  (self, value) => self.NoHistory = (bool) value
			}, {
			  "ParentActivity",
			  "parentActivityName",
			  self          => self._ParentActivity,
			  (self, value) => self._ParentActivity = (string) value,
			  typeof (Type)
			}, {
			  "Permission",
			  "permission",
			  self          => self.Permission,
			  (self, value) => self.Permission  = (string) value
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
			  "RoundIcon",
			  "roundIcon",
			  self          => self._RoundIcon,
			  (self, value) => self._RoundIcon  = (string) value
			}, {
			  "SupportsPictureInPicture",
			  "supportsPictureInPicture",
			  self          => self._SupportsPictureInPicture,
			  (self, value) => self._SupportsPictureInPicture = (bool) value
			}, {
			  "ScreenOrientation",
			  "screenOrientation",
			  self          => self.ScreenOrientation,
			  (self, value) => self.ScreenOrientation = (ScreenOrientation) value
			}, {
			  "StateNotNeeded",
			  "stateNotNeeded",
			  self          => self.StateNotNeeded,
			  (self, value) => self.StateNotNeeded  = (bool) value
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
			  (self, value) => self._UiOptions = (UiOptions) value
			}, {
			  "WindowSoftInputMode",
			  "windowSoftInputMode",
			  self          => self.WindowSoftInputMode,
			  (self, value) => self.WindowSoftInputMode = (SoftInput) value
			},
		};

		TypeDefinition type;
		ICollection<string> specified;

		public static ActivityAttribute FromTypeDefinition (TypeDefinition type)
		{
			CustomAttribute attr = type.GetCustomAttributes ("Android.App.ActivityAttribute")
				.SingleOrDefault ();
			if (attr == null)
				return null;
			ActivityAttribute self = new ActivityAttribute () {
				type = type,
			};
			self.specified = mapping.Load (self, attr);
			return self;
		}

		internal XElement ToElement (IAssemblyResolver resolver, string packageName, int targetSdkVersion)
		{
			return mapping.ToElement (this, specified, packageName, type, resolver, targetSdkVersion);
		}
	}
}
