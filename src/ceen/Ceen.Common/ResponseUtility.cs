namespace Ceen
{
    /// <summary>
    /// Extension methods for providing responses from within
    /// an <see cref="IHttpResponse" /> instance
    /// </summary>
    public static class ResponseUtility
    {
        /// <summary>
        /// Sets the status code and optional non-default message on the context's response instance
        /// </summary>
        /// <param name="context">The context instance to set the code on</param>
        /// <param name="code">The status code to set</param>
        /// <param name="message">The optional status message; will use a default message if this is not set</param>
        /// <returns><c>true<c/></returns>
        public static bool SetResponseStatus(this IHttpContext context, HttpStatusCode code, string message = null)
            => context.Response.SetStatus(code, message);

        /// <summary>
        /// Sets the status code and optional non-default message on the response instance
        /// </summary>
        /// <param name="response">The response instance to set the code on</param>
        /// <param name="code">The status code to set</param>
        /// <param name="message">The optional status message; will use a default message if this is not set</param>
        /// <returns><c>true<c/></returns>
        public static bool SetStatus(this IHttpResponse response, HttpStatusCode code, string message = null)
        {
            response.StatusCode = code;
            if (string.IsNullOrWhiteSpace(message))
                message = HttpStatusMessages.DefaultMessage(code);
            response.StatusMessage = message;
            return true;
        }

        /// <summary>
        /// Sets the status code to OK with an optional non-default message on the context's response instance
        /// </summary>
        /// <param name="context">The context instance to set the status on</param>
        /// <param name="message">The optional status message; will use a default message if this is not set</param>
        /// <returns><c>true<c/></returns>
        public static bool SetResponseOK(this IHttpContext context, string message = null)
            => context.Response.SetStatus(HttpStatusCode.OK, message);

        /// <summary>
        /// Sets the status code to BadRequest with an optional non-default message on the context's response instance
        /// </summary>
        /// <param name="context">The context instance to set the status on</param>
        /// <param name="message">The optional status message; will use a default message if this is not set</param>
        /// <returns><c>true<c/></returns>
        public static bool SetResponseBadRequest(this IHttpContext context, string message = null)
            => context.Response.SetStatus(HttpStatusCode.BadRequest, message);

        /// <summary>
        /// Sets the status code to Forbidden with an optional non-default message on the context's response instance
        /// </summary>
        /// <param name="context">The context instance to set the status on</param>
        /// <param name="message">The optional status message; will use a default message if this is not set</param>
        /// <returns><c>true<c/></returns>
        public static bool SetResponseForbidden(this IHttpContext context, string message = null)
            => context.Response.SetStatus(HttpStatusCode.Forbidden, message);

        /// <summary>
        /// Sets the status code to Unauthorized with an optional non-default message on the context's response instance
        /// </summary>
        /// <param name="context">The context instance to set the status on</param>
        /// <param name="message">The optional status message; will use a default message if this is not set</param>
        /// <returns><c>true<c/></returns>
        public static bool SetResponseUnauthorized(this IHttpContext context, string message = null)
            => context.Response.SetStatus(HttpStatusCode.Unauthorized, message);

    }
}