namespace Org.Apache.Http.Impl.Cookie
{
	public partial class BasicClientCookie
	{
		public void SetComment (string comment)
		{
			Comment = comment;
		}
		
		public void SetDomain (string domain)
		{
			Domain = domain;
		}
		
		public void SetExpiryDate (Java.Util.Date date)
		{
			ExpiryDate = date;
		}
		
		public void SetPath (string path)
		{
			Path = path;
		}
		
		public void SetValue (string value)
		{
			Value = value;
		}
		
		public void SetVersion (int version)
		{
			Version = version;
		}
	}
}
