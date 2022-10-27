using LinqKit;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sorocaba.Commons.Entity.Pagination {
    public static class ParametersParser {

        public static PaginationParameters FromRequest(HttpRequest request)  {
            try {
                List<KeyValuePair<string, string>> requestParameters = new List<KeyValuePair<string, string>>();

                request.Query.ForEach(f => requestParameters.Add(new KeyValuePair<string, string>(f.Key, f.Value)));
                Func<string, string> GetParameter = (key) => requestParameters.Where(p => p.Key == key).FirstOrDefault().Value;

                int page;
                if (GetParameter("page") == null || !Int32.TryParse(GetParameter("page"), out page)) {
                    page = 1;
                }
                
                int itemsPerPage;
                if (GetParameter("items_per_page") == null || !Int32.TryParse(GetParameter("items_per_page"), out itemsPerPage)) {
                    itemsPerPage = 10;
                }

                bool showAllItems;
                if (GetParameter("show_all_items") == null || !bool.TryParse(GetParameter("show_all_items"), out showAllItems)) {
                    showAllItems = false;
                }

                List<Sorter> sorters = new List<Sorter>();
                if (GetParameter("sort_fields") != null) {
                    string[] sortFields = GetParameter("sort_fields").Split(',');
                    foreach (string sortField in sortFields) {
                        if (!Regex.IsMatch(sortField, @"^\w+(\.\w+)*:(asc|desc)+$")) {
                            throw new PaginationException(Strings.InvalidSorterFormat);
                        }
                        string name = sortField.Substring(0, sortField.IndexOf(":"));
                        string value = sortField.Substring(sortField.IndexOf(":") + 1);
                        sorters.Add(new Sorter(name, value));
                    }
                }

                List<Filter> filters = new List<Filter>();
                if (GetParameter("filter_fields") != null) {
                    foreach (string filterExpression in GetParameter("filter_fields").Split(',')) {
                        filters.Add(Filter.CreateFilter(filterExpression));
                    }
                }

                List<Filter> fullFilters = new List<Filter>();
                if (GetParameter("fullsearch_fields") != null) {
                    string fullValue = GetParameter("fullsearch_value");
                    if (string.IsNullOrWhiteSpace(fullValue)) {
                        throw new PaginationException(Strings.FullSearchValueRequired);
                    }
                    foreach (string fullField in GetParameter("fullsearch_fields").Split(',')) {
                        fullFilters.Add(Filter.CreateSimpleFilter(fullField, fullValue));
                    }
                }

                return new PaginationParameters {
                    Page = page,
                    ItemsPerPage = itemsPerPage,
                    ShowAllItems = showAllItems,
                    Sorters = sorters,
                    Filters = filters,
                    FullFilters = fullFilters
                };

            } catch (Exception e) {
                throw (e is PaginationException) ? e : new PaginationException(Strings.InvalidPaginationParameters);
            }
        }
    }
}
