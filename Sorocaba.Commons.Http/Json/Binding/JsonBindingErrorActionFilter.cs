using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Sorocaba.Commons.Http;

namespace Sorocaba.Commons.Http.Json.Binding {
    public class JsonBindingErrorActionFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext actionContext)
        {
            if (HttpContextHelper.Current.Items["JSON_BINDING_ERROR"] != null)
            {
                throw new JsonBindingException((Exception)HttpContextHelper.Current.Items["JSON_BINDING_ERROR"]);
            }
        }
    }
}
