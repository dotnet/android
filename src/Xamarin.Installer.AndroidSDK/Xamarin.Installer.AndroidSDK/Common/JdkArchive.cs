using System;

namespace Xamarin.Installer.AndroidSDK.Common
{
    public class JdkArchive : Archive, IEquatable<JdkArchive>
    {
        public JdkArchive(string hostOS) : base(hostOS)
        {
        }

        public string PayloadFileName { get; set; }

        public override bool IsValidForSystem()
        {
            return IsPlatformValid() && IsHostArchValid();
        }

        public override bool Equals(Object obj)
        {
            return Equals(obj as JdkArchive);
        }

        public bool Equals(JdkArchive other)
        {
            return base.Equals(other);
        }

		public override int GetHashCode()
		{
			return this.HostOS.GetHashCode();
		}
	}
}
