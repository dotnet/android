using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Mono.Cecil;

using Java.Interop.Tools.Cecil;

using Xamarin.Android.Manifest;

using Android.App;
using Android.Content.PM;
using Android.Views;

namespace Android.App {

	partial class ActivityAttribute {

		bool _AllowEmbedded;
		string _ColorMode;
		bool _HardwareAccelerated;
		float _MaxAspectRatio;
		string _ParentActivity;
		LayoutDirection _LayoutDirection;
		UiOptions _UiOptions;
		bool _Immersive;
		bool _ResizeableActivity;
		bool _ShowForAllUsers;
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
              "AutoRemoveFromRecents",
              "autoRemoveFromRecents",
              self          => self.AutoRemoveFromRecents,
              (self, value) => self.AutoRemoveFromRecents = (bool) value
            }, {
              "Banner",
              "banner",
              self          => self.Banner,
              (self, value) => self.Banner = (string) value
            }, {
			  "ClearTaskOnLaunch",
			  "clearTaskOnLaunch",
			  self          => self.ClearTaskOnLaunch,
			  (self, value) => self.ClearTaskOnLaunch = (bool) value
			}, {
			  "ColorMode",
			  "colorMode",
			  self          => self._ColorMode,
			  (self, value) => self._ColorMode = (string) value
			}, {
			  "ConfigurationChanges",
			  "configChanges",
			  self          => self.ConfigurationChanges,
			  (self, value) => self.ConfigurationChanges  = (ConfigChanges) value
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
              "DocumentLaunchMode",
              "documentLaunchMode",
              self          => self.DocumentLaunchMode,
              (self, value) => self.DocumentLaunchMode = (DocumentLaunchMode) value
            }, {
              "Enabled",
			  "enabled",
			  self          => self.Enabled,
			  (self, value) => self.Enabled = (bool) value
			}, {
              "EnableVrMode",
              "enableVrMode",
              self          => self.EnableVrMode,
              (self, value) => self.EnableVrMode = (string) value
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
              "FinishOnCloseSystemDialogs",
              "finishOnCloseSystemDialogs",
              self          => self.FinishOnCloseSystemDialogs,
              (self, value) => self.FinishOnCloseSystemDialogs  = (bool) value
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
              "LockTaskMode",
              "lockTaskMode",
              self          => self.LockTaskMode,
              (self, value) => self.LockTaskMode = (LockTaskMode) value
            }, {
              "Logo",
              "logo",
              self          => self.Logo,
              (self, value) => self.Logo = (string) value
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
			  "MaxAspectRatio",
			  "maxAspectRatio",
			  self          => self._MaxAspectRatio,
			  (self, value) => self._MaxAspectRatio = (float) value
			}, {
              "MaxRecents",
              "maxRecents",
              self          => self.MaxRecents,
              (self, value) => self.MaxRecents = (int) value
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
              "PersistableMode",
              "persistableMode",
              self          => self.PersistableMode,
              (self, value) => self.PersistableMode  = (ActivityPersistableMode) value
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
              "RecreateOnConfigChanges",
              "recreateOnConfigChanges",
              self          => self.RecreateOnConfigChanges,
              (self, value) => self.RecreateOnConfigChanges = (bool) value
            }, {
              "RelinquishTaskIdentity",
              "relinquishTaskIdentity",
              self          => self.RelinquishTaskIdentity,
              (self, value) => self.RelinquishTaskIdentity = (bool) value
            }, {
			  "ResizeableActivity",
			  "resizeableActivity",
			  self          => self._ResizeableActivity,
			  (self, value) => self._ResizeableActivity = (bool) value
			}, {
              "ResumeWhilePausing",
              "resumeWhilePausing",
              self          => self.ResumeWhilePausing,
              (self, value) => self.ResumeWhilePausing = (bool) value
            }, {
              "RotationAnimation",
              "rotationAnimation",
              self          => self.RotationAnimation,
              (self, value) => self.RotationAnimation = (string) value
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
              "SingleUser",
              "singleUser",
              self          => self.SingleUser,
              (self, value) => self.SingleUser = (bool) value
            }, {
			  "ShowForAllUsers",
			  "showForAllUsers",
			  self          => self._ShowForAllUsers,
			  (self, value) => self._ShowForAllUsers = (bool) value
			}, {
              "ShowOnLockScreen",
              "showOnLockScreen",
              self          => self.ShowOnLockScreen,
              (self, value) => self.ShowOnLockScreen = (bool) value
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
              "VisibleToInstantApps",
              "visibleToInstantApps",
              self          => self.VisibleToInstantApps,
              (self, value) => self.VisibleToInstantApps = (bool) value
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
