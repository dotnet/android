using System;
namespace Xamarin.AndroidTools.AnnotationSupport
{
	// Looks for @RequiresPermission, store them and use for auditing user code to find missing permissions.
	public class PermissionManagedTypeFinderExtension : ManagedTypeFinderExtension
	{
		public PermissionManagedTypeFinderExtension (ManagedTypeFinder m)
			: base (m)
		{
		}

		public override void ProcessAnnotation (AnnotatedItem item)
		{
			if (item.ManagedInfo.Type.Name == null)
				return;

			foreach (var a in item.Annotations) {
				if (a.Values == null || a.Name != "RequiresPermission")
                                        continue;
				for (int i = 0; i < a.Values.Count; i++) {
					var v = a.Values [i];
					if (v.Name != "value")
						continue;
					var ext = item.GetExtension<RequiresPermissionExtension> ();
					if (ext == null) {
						ext = new RequiresPermissionExtension ();
						item.SetExtension (ext);
					}
					// value is quoted by "", so chop them out.
					string val = v.Val.Substring (1, v.Val.Length - 2);
					if (!ext.Values.Contains (val))
						ext.Values.Add (val);
				}
			}
		}
	}
}

