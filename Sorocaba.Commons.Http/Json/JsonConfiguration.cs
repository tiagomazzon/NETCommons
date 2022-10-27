using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Sorocaba.Commons.Http.Json.Binding;
using Sorocaba.Commons.Http.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sorocaba.Commons.Http;
using Microsoft.AspNetCore.Mvc;

namespace Sorocaba.Commons.Http.Json {
    public class JsonConfiguration {

        public static void ApplyConfiguration(MvcNewtonsoftJsonOptions configuration) {

            var settings = configuration.SerializerSettings;

            settings.Converters.Add(new JsonTrimmingConverter());

            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            settings.NullValueHandling = NullValueHandling.Ignore;
            settings.Formatting = Formatting.None;
            settings.DateFormatString = "dd/MM/yyyy HH:mm:ss";
            settings.Error = (sender, a) => {
                if (!HttpContextHelper.Current.Items.Any(w => (string)w.Value == "JSON_BINDING_ERROR"))
                {
                    HttpContextHelper.Current.Items.Add("JSON_BINDING_ERROR", a.ErrorContext.Error);
                }
            };
        }

        public static void ApplyFilters(MvcOptions configuration)
        {
            configuration.Filters.Add(new JsonBindingErrorActionFilter());
        }
    }
}
