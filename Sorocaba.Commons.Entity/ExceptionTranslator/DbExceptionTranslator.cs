using Microsoft.Data.SqlClient;
using Sorocaba.Commons.Entity.Validation.Rules;
using Sorocaba.Commons.Foundation.Exceptions;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sorocaba.Commons.Entity.ExceptionTranslator {
    public class DbExceptionTranslator : IExceptionTranslator {

        public bool TranslateException(Exception exception, out string message, out Exception newException) {

            message = exception.Message;
            newException = exception;

            if (exception is ProviderIncompatibleException) {
                message = Strings.DbConnectionError;
                return true;
            }

            if (exception is EntityCommandExecutionException) {
                message = Strings.DbCommandExecutionError;
                return true;
            }

            if (exception is DbEntityValidationException) {
                ValidationRuleViolations violations = new ValidationRuleViolations();
                violations.Add((exception as DbEntityValidationException).EntityValidationErrors);
                message = Strings.DbValidationError;
                newException = new DbWrappedException(message, exception, violations);
                return true;
            }

            if (exception is DbUpdateException || exception is SqlException) {
                message = Strings.DbUpdateError;
                newException = ExceptionUtils.GetInnerException(exception);
                foreach (var regex in Strings.ConstraintRegexes) {
                    if (regex.IsMatch(newException.Message)) {
                        string constraintName = regex.Matches(newException.Message)[0].Groups["cName"].ToString();
                        string constraintMessage = GetConstraintMessage(constraintName);
                        if (constraintMessage != null) {
                            message = constraintMessage;
                        }
                    }
                }
                return true;
            }

            return false;
        }

        private static string GetConstraintMessage(string constraintName) {
            return ConfigurationManager.AppSettings[constraintName];
        }
    }
}
