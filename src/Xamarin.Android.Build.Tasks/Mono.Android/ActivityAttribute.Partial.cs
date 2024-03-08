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
		bool _AutoRemoveFromRecents;
		string _Banner;
		string _ColorMode;
		DocumentLaunchMode _DocumentLaunchMode;
		bool _HardwareAccelerated;
		bool _Immersive;
		LayoutDirection _LayoutDirection;
		string _LockTaskMode;
		string _Logo;
		float _MaxAspectRatio;
		int _MaxRecents;
		string _ParentActivity;
		ActivityPersistableMode _PersistableMode;
		ConfigChanges _RecreateOnConfigChanges;
		bool _RelinquishTaskIdentity;
		bool _ResizeableActivity;
		bool _ResumeWhilePausing;
		WindowRotationAnimation _RotationAnimation;
		string _RoundIcon;
		bool _ShowForAllUsers;
		bool _ShowOnLockScreen;
		bool _ShowWhenLocked;
		bool _SingleUser;
		bool _SupportsPictureInPicture;
		bool _TurnScreenOn;
		UiOptions _UiOptions;
		bool _VisibleToInstantApps;

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
			  self          => self._AutoRemoveFromRecents,
			  (self, value) => self._AutoRemoveFromRecents = (bool) value
			}, {
			  "Banner",
			  "banner",
			  self          => self._Banner,
			  (self, value) => self._Banner = (string) value
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
			  // TODO: Not currently documented at: https://developer.android.com/guide/topics/manifest/activity-element
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
			  self          => self._DocumentLaunchMode,
			  (self, value) => self._DocumentLaunchMode = (DocumentLaunchMode) value
			}, {
			  "Enabled",
			  "enabled",
			  self          => self.Enabled,
			  (self, value) => self.Enabled = (bool) value
			}, {
			  // TODO: Not currently documented at: https://developer.android.com/guide/topics/manifest/activity-element
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
			  // TODO: Not currently documented at: https://developer.android.com/guide/topics/manifest/activity-element
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
			  // TODO: Not currently documented at: https://developer.android.com/guide/topics/manifest/activity-element
			  "LayoutDirection",
			  "layoutDirection",
			  self          => self._LayoutDirection,
			  (self, value) => self._LayoutDirection  = (LayoutDirection) value
			}, {
			  // TODO: Not currently documented at: https://developer.android.com/guide/topics/manifest/activity-element
			  "LockTaskMode",
			  "lockTaskMode",
			  self          => self._LockTaskMode,
			  (self, value) => self._LockTaskMode = (string) value
			}, {
			  "Logo",
			  "logo",
			  self          => self._Logo,
			  (self, value) => self._Logo = (string) value
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
			  self          => self._MaxRecents,
			  (self, value) => self._MaxRecents = (int) value
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
			  "PersistableMode",
			  "persistableMode",
			  self          => self._PersistableMode,
			  (self, value) => self._PersistableMode  = (ActivityPersistableMode) value
			}, {
			  "Process",
			  "process",
			  self          => self.Process,
			  (self, value) => self.Process = (string) value
			}, {
			  // TODO: Not currently documented at: https://developer.android.com/guide/topics/manifest/activity-element
			  "RecreateOnConfigChanges",
			  "recreateOnConfigChanges",
			  self          => self._RecreateOnConfigChanges,
			  (self, value) => self._RecreateOnConfigChanges = (ConfigChanges) value
			}, {
			  "RelinquishTaskIdentity",
			  "relinquishTaskIdentity",
			  self          => self._RelinquishTaskIdentity,
			  (self, value) => self._RelinquishTaskIdentity = (bool) value
			}, {
			  "ResizeableActivity",
			  "resizeableActivity",
			  self          => self._ResizeableActivity,
			  (self, value) => self._ResizeableActivity = (bool) value
			}, {
			  "ResumeWhilePausing",
			  "resumeWhilePausing",
			  self          => self._ResumeWhilePausing,
			  (self, value) => self._ResumeWhilePausing = (bool) value
			}, {
			  // TODO: Not currently documented at: https://developer.android.com/guide/topics/manifest/activity-element
			  "RotationAnimation",
			  "rotationAnimation",
			  self          => self._RotationAnimation,
			  (self, value) => self._RotationAnimation = (WindowRotationAnimation) value
			}, {
			  "RoundIcon",
			  "roundIcon",
			  self          => self._RoundIcon,
			  (self, value) => self._RoundIcon  = (string) value
			}, {
			  "ScreenOrientation",
			  "screenOrientation",
			  self          => self.ScreenOrientation,
			  (self, value) => self.ScreenOrientation = (ScreenOrientation) value
			}, {
			  "ShowForAllUsers",
			  "showForAllUsers",
			  self          => self._ShowForAllUsers,
			  (self, value) => self._ShowForAllUsers = (bool) value
			}, {
			  "ShowOnLockScreen",
			  "showOnLockScreen",
			  self          => self._ShowOnLockScreen,
			  (self, value) => self._ShowOnLockScreen = (bool) value
			}, {
			  // TODO: Not currently documented at: https://developer.android.com/guide/topics/manifest/activity-element
			  "ShowWhenLocked",
			  "showWhenLocked",
			  self          => self._ShowWhenLocked,
			  (self, value) => self._ShowWhenLocked = (bool) value
			}, {
			  // TODO: Not currently documented at: https://developer.android.com/guide/topics/manifest/activity-element
			  "SingleUser",
			  "singleUser",
			  self          => self._SingleUser,
			  (self, value) => self._SingleUser = (bool) value
			}, {
			  "StateNotNeeded",
			  "stateNotNeeded",
			  self          => self.StateNotNeeded,
			  (self, value) => self.StateNotNeeded  = (bool) value
			}, {
			  "SupportsPictureInPicture",
			  "supportsPictureInPicture",
			  self          => self._SupportsPictureInPicture,
			  (self, value) => self._SupportsPictureInPicture = (bool) value
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
			  // TODO: Not currently documented at: https://developer.android.com/guide/topics/manifest/activity-element
			  "TurnScreenOn",
			  "turnScreenOn",
			  self          => self._TurnScreenOn,
			  (self, value) => self._TurnScreenOn = (bool) value
			}, {
			  "UiOptions",
			  "uiOptions",
			  self          => self._UiOptions,
			  (self, value) => self._UiOptions = (UiOptions) value
			}, {
			  // TODO: Not currently documented at: https://developer.android.com/guide/topics/manifest/activity-element
			  "VisibleToInstantApps",
			  "visibleToInstantApps",
			  self          => self._VisibleToInstantApps,
			  (self, value) => self._VisibleToInstantApps = (bool) value
			}, {
			  "WindowSoftInputMode",
			  "windowSoftInputMode",
			  self          => self.WindowSoftInputMode,
			  (self, value) => self.WindowSoftInputMode = (SoftInput) value
			},
		};

		TypeDefinition type;
		ICollection<string> specified;

		public static ActivityAttribute FromTypeDefinition (TypeDefinition type, TypeDefinitionCache cache)
		{
			CustomAttribute attr = type.GetCustomAttributes ("Android.App.ActivityAttribute")
				.SingleOrDefault ();
			if (attr == null)
				return null;
			ActivityAttribute self = new ActivityAttribute () {
				type = type,
			};
			self.specified = mapping.Load (self, attr, cache);
			return self;
		}

		internal XElement ToElement (IAssemblyResolver resolver, string packageName, TypeDefinitionCache cache, int targetSdkVersion)
		{
			return mapping.ToElement (this, specified, packageName, cache, type, resolver, targetSdkVersion);
		}
	}
}
