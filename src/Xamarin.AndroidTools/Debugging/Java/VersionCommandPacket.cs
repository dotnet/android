
namespace Xamarin.AndroidTools.Debugging.Java
{
	internal class VersionCommandPacket : CommandPacket
	{
		public VersionCommandPacket()
		{
			CommandSet = 1;
			Command = 1;
		}
	}
}
