using LinqKit;
using Microsoft.AspNetCore.Http;
using Sorocaba.Commons.Entity.Reflection;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.SqlServer;
using System.Linq;
using System.Linq.Dynamic;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Sorocaba.Commons.Entity.Pagination {
    public static class PaginationEngine {

        public static PaginatedResult<T> Paginate<T>(IQueryable<T> query, HttpRequest request) where T : class {
            return Paginate<T>(query, ParametersParser.FromRequest(request));
        }

        public static PaginatedResult<T> Paginate<T>(IQueryable<T> query, PaginationParameters parameters) where T : class {

            ICollection<Sorter> sorters = parameters.Sorters;
            if (sorters != null && sorters.Count > 0) {
                StringBuilder sortString = new StringBuilder();
                foreach (var sorter in sorters) {
                    if (sortString.Length > 0) {
                        sortString.Append(", ");
                    }
                    sortString.Append(sorter.FieldName);
                    sortString.Append(' ');
                    sortString.Append(sorter.SortOrder);
                }
                query = query.OrderBy(sortString.ToString(), null);
            }

            ICollection<Filter> filters = parameters.Filters;
            if (filters != null && filters.Count > 0) {
                foreach (var filter in filters) {
                    var filterExpression = ParseFilter<T>(filter);
                    query = query.Where(filterExpression);
                }
            }

            ICollection<Filter> fullFilters = parameters.FullFilters;
            if (fullFilters != null && fullFilters.Count > 0) {
                var finalPredicate = PredicateBuilder.False<T>();
                foreach (var fullFilter in fullFilters) {
                    var filterExpression = ParseFilter<T>(fullFilter, true);
                    if (filterExpression != null) {
                        finalPredicate = finalPredicate.Or(filterExpression);
                    }
                }
                query = query.Where(finalPredicate.Expand());
            }

            int page = parameters.Page;
            int itemsPerPage = parameters.ItemsPerPage;
            long itemCount = query.Count();
            int pageCount = (int) Math.Ceiling((double) itemCount / (double) itemsPerPage);
            if (page > pageCount) {
                page = pageCount;
            }
            if (page == 0) {
                page = 1;
            }
            int itemOffset = ((page - 1) * itemsPerPage) + 1;
            itemOffset = (itemOffset <= 0) ? 0 : itemOffset;

            if (parameters.ShowAllItems) {
                page = 1;
                pageCount = 1;
                itemsPerPage = (int) itemCount;
                itemOffset = 0;
            }

            List<T> itemList = query.Skip((page - 1) * itemsPerPage).Take(itemsPerPage).ToList();

            return new PaginatedResult<T> {
                ItemCount = itemCount,
                PageCount = pageCount,
                CurrentPage = page,
                ItemOffset = itemOffset,
                ItemList = itemList
            };
        }

        private static Expression<Func<T, bool>> ParseFilter<T>(Filter filter, bool nullOnConvertError = false) where T : class {

            string fName = filter.FieldName;
            string fOperator = filter.Operator;
            object fValue = filter.FieldValue;

            Type entity = typeof(T);

            bool propHasCollections;
            PropertyInfo propInfo = TypeUtils.GetPropertyInfo<T>(fName, out propHasCollections);
            Type propType = (propInfo != null) ? propInfo.PropertyType : null;

            if (propInfo == null) {
                Exception(Strings.EntityPropertyInvalid(fName));
            }

            if (propHasCollections) {
                Exception(Strings.CollectionPropertyNotSupported(fName));
            }

            if (!propType.IsValueType && propType != typeof(string)) {
                Exception(Strings.PropertyMustBeValueType(fName));
            }

            object oldFValue = fValue;
            if (!TypeUtils.TryConvert(propType, fValue, out fValue)) {
                if (nullOnConvertError) {
                    return null;
                } else {
                    Exception(Strings.CouldNotConvertValueToPropertyType(oldFValue.ToString(), propType.Name, fName));
                }
            }


            ParameterExpression entityExpression;
            Expression propertyExpression = TypeUtils.GetPropertyExpression<T>(fName, out entityExpression);
            ConstantExpression argumentExpression = Expression.Constant(fValue);

            if (fOperator == Operators.LIKE.Symbol || fOperator == Operators.LIKE_ALTERNATIVE.Symbol) {

                if (propType == (typeof(bool)) || propType == (typeof(bool?))) {
                    fOperator = Operators.EQUAL.Symbol;
                }

                else if (propType == (typeof(char)) || propType == (typeof(char?))) {
                    fOperator = Operators.EQUAL.Symbol;
                }

                else if (propType == (typeof(DateTime)) || propType == (typeof(DateTime?))) {

                    argumentExpression = Expression.Constant(((DateTime) fValue).ToString("dd/MM/yyyy"));

                    var dateConvertedProperty = Expression.Convert(propertyExpression, typeof(DateTime?));
                    var partConversionMethod = typeof(SqlFunctions).GetMethod("DatePart", new Type[] { typeof(string), typeof(DateTime?) });
                    var dayPart = Expression.Call(partConversionMethod, Expression.Constant("dd", typeof(string)), dateConvertedProperty);
                    var monthPart = Expression.Call(partConversionMethod, Expression.Constant("mm", typeof(string)), dateConvertedProperty);
                    var yearPart = Expression.Call(partConversionMethod, Expression.Constant("yyyy", typeof(string)), dateConvertedProperty);

                    var stringConversionMethod = typeof(SqlFunctions).GetMethod("StringConvert", new Type[] { typeof(decimal?), typeof(int?) });
                    var convertedDayPart = Expression.Call(stringConversionMethod, Expression.Convert(dayPart, typeof(decimal?)), Expression.Constant(2, typeof(int?)));
                    var convertedMonthPart = Expression.Call(stringConversionMethod, Expression.Convert(monthPart, typeof(decimal?)), Expression.Constant(2, typeof(int?)));
                    var convertedYearPart = Expression.Call(stringConversionMethod, Expression.Convert(yearPart, typeof(decimal?)), Expression.Constant(4, typeof(int?)));

                    var concatMethod = typeof(string).GetMethod("Concat", new Type[] { typeof(string[]) });
                    var rightMethod = typeof(DbFunctions).GetMethod("Right", new Type[] { typeof(string), typeof(long?) });
                    var trimMethod = typeof(string).GetMethod("Trim", new Type[] { });

                    convertedDayPart = Expression.Call(convertedDayPart, trimMethod);
                    convertedDayPart = Expression.Call(
                        concatMethod,
                        Expression.NewArrayInit(typeof(string),
                            Expression.Constant("0", typeof(string)), convertedDayPart
                        )
                    );
                    convertedDayPart = Expression.Call(rightMethod, convertedDayPart, Expression.Constant(2L, typeof(long?)));

                    convertedMonthPart = Expression.Call(convertedMonthPart, trimMethod);
                    convertedMonthPart = Expression.Call(
                        concatMethod,
                        Expression.NewArrayInit(typeof(string),
                            Expression.Constant("0", typeof(string)), convertedMonthPart
                        )
                    );
                    convertedMonthPart = Expression.Call(rightMethod, convertedMonthPart, Expression.Constant(2L, typeof(long?)));

                    propertyExpression = Expression.Call(
                        concatMethod,
                        Expression.NewArrayInit(typeof(string),
                            convertedDayPart, Expression.Constant("/", typeof(string)), convertedMonthPart, Expression.Constant("/", typeof(string)), convertedYearPart
                        )
                    );
                }
                
                else if(propType != typeof(string)) {
                    argumentExpression = Expression.Constant(fValue.ToString());
                    var convertiblePropertyType = typeof(decimal?);
                    var convertedProperty = Expression.Convert(propertyExpression, convertiblePropertyType);
                    var conversionMethod = typeof(SqlFunctions).GetMethod("StringConvert", new Type[] { convertiblePropertyType, typeof(int?) });
                    propertyExpression = Expression.Call(conversionMethod, convertedProperty, Expression.Constant(20, typeof(int?)));
                }
            }

            return Expression.Lambda<Func<T, bool>>(
                Operators.GetBySymbol(fOperator).Expression(propertyExpression, Expression.Convert(argumentExpression, propertyExpression.Type)),
                entityExpression
            );
        }

        private static void Exception(string message, params object[] args) {
            throw new PaginationException(message);
        }
    }
}
