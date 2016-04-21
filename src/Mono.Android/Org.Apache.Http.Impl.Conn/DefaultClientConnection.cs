#if FEATURE_NEED_API_MERGE
namespace Org.Apache.Http.Impl.Conn
{
	public partial class DefaultClientConnection
	{
		Java.Net.Socket Org.Apache.Http.Conn.IOperatedClientConnection.Socket {
			get { return base.Socket; }
		}
	}
}
#endif  // FEATURE_NEED_API_MERGE
