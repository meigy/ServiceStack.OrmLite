﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq.Expressions;

namespace ServiceStack.OrmLite.Sqlite
{
    /// <summary>
    /// Description of SqliteExpressionVisitor.
    /// </summary>
    public class SqliteExpression<T> : SqlExpression<T>
    {
        public SqliteExpression(IOrmLiteDialectProvider dialectProvider) 
            : base(dialectProvider) {}

        protected override object VisitColumnAccessMethod(MethodCallExpression m)
        {
            List<Object> args = this.VisitExpressionList(m.Arguments);
            var quotedColName = Visit(m.Object);
            string statement;

            switch (m.Method.Name)
            {
                case "Substring":
                    var startIndex = Int32.Parse(args[0].ToString()) + 1;
                    if (args.Count == 2)
                    {
                        var length = Int32.Parse(args[1].ToString());
                        statement = string.Format("substr({0}, {1}, {2})", quotedColName, startIndex, length);
                    }
                    else
                        statement = string.Format("substr({0}, {1})", quotedColName, startIndex);
                    break;
                default:
                    return base.VisitColumnAccessMethod(m);
            }
            return new PartialSqlString(statement);
        }

        protected override object VisitSqlMethodCall(MethodCallExpression m)
        {
            List<Object> args = this.VisitExpressionList(m.Arguments);
            object quotedColName = args[0];
            args.RemoveAt(0);

            var statement = "";

            switch (m.Method.Name)
            {
                case "In":
                    var getter = CreateInExprGetterFn(m);
                    var inArgs = Sql.Flatten(getter() as IEnumerable);

                    var sIn = new StringBuilder();
                    foreach (var e in inArgs)
                    {
                        sIn.AppendFormat("{0}{1}",
                            sIn.Length > 0 ? "," : "",
                            base.DialectProvider.GetQuotedValue(e, e.GetType()));
                    }
                    statement = string.Format("{0} {1} ({2})", quotedColName, m.Method.Name, sIn);
                    break;
                case "InExpression":
                    var fn = CreateInExprGetterFn(m);
                    var sqlExpression = fn() as ISqlExpression;
                    var subSelect = sqlExpression.ToSelectStatement();

                    statement = string.Format("{0} {1} ({2})", quotedColName, "IN", subSelect);
                    break;
                case "Desc":
                    statement = string.Format("{0} DESC", quotedColName);
                    break;
                case "As":
                    statement = string.Format("{0} As {1}", quotedColName,
                        base.DialectProvider.GetQuotedColumnName(RemoveQuoteFromAlias(args[0].ToString())));
                    break;
                case "Sum":
                case "Count":
                case "Min":
                case "Max":
                case "Avg":
                    statement = string.Format("{0}({1}{2})",
                        m.Method.Name,
                        quotedColName,
                        args.Count == 1 ? string.Format(",{0}", args[0]) : "");
                    break;
                default:
                    throw new NotSupportedException();
            }

            return new PartialSqlString(statement);
        }
    }
}
