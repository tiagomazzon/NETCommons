using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sorocaba.Commons.Foundation.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Sorocaba.Commons.Http.Exceptions.Translation {
    public class HttpExceptionFilterAttribute : ExceptionFilterAttribute {

        protected HttpExceptionTranslator ExceptionTranslator { get; set; }

        protected bool ShowStackTrace { get; set; }

        public HttpExceptionFilterAttribute(bool showStackTrace = false, params IExceptionTranslator[] customTranslators) {
            ExceptionTranslator = new HttpExceptionTranslator(customTranslators);
            ShowStackTrace = showStackTrace;
        }

        public override void OnException(ExceptionContext ctx) {

            var responseStatus = StatusCodes.Status500InternalServerError;
            HttpExceptionData responseData = new HttpExceptionData();

            ExceptionTranslator.TranslateException(ctx.Exception, ref responseStatus, ref responseData);

            if (!ShowStackTrace) {
                responseData.ErrorStackTrace = null;
            }

            ctx.HttpContext.Response.StatusCode = responseStatus;
            ctx.Result = new OkObjectResult(responseData);
        }
    }
}
