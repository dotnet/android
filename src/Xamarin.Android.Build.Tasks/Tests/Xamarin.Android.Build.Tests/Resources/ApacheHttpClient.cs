using System;
using Org.Apache.Http;
using Org.Apache.Http.Client;
using Org.Apache.Http.Client.Methods;
using Org.Apache.Http.Conn;
using Org.Apache.Http.Params;
using Org.Apache.Http.Protocol;

class ApacheHttpClient : Java.Lang.Object, IHttpClient
{
	public IClientConnectionManager ConnectionManager => throw new NotImplementedException();

	public IHttpParams Params => throw new NotImplementedException();

	public IHttpResponse Execute(IHttpUriRequest request) => throw new NotImplementedException();

	public Java.Lang.Object Execute(IHttpUriRequest request, IResponseHandler responseHandler) => throw new NotImplementedException();

	public Java.Lang.Object Execute(IHttpUriRequest request, IResponseHandler responseHandler, IHttpContext context) => throw new NotImplementedException();

	public IHttpResponse Execute(IHttpUriRequest request, IHttpContext context) => throw new NotImplementedException();

	public IHttpResponse Execute(HttpHost target, IHttpRequest request) => throw new NotImplementedException();

	public Java.Lang.Object Execute(HttpHost target, IHttpRequest request, IResponseHandler responseHandler) => throw new NotImplementedException();

	public Java.Lang.Object Execute(HttpHost target, IHttpRequest request, IResponseHandler responseHandler, IHttpContext context) => throw new NotImplementedException();

	public IHttpResponse Execute(HttpHost target, IHttpRequest request, IHttpContext context) => throw new NotImplementedException();
}
