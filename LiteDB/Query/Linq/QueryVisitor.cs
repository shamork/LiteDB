﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace LiteDB
{
    /// <summary>
    /// Class helper to create Queries based on Linq expressions
    /// </summary>
    internal class QueryVisitor<T>
    {
        private BsonMapper _mapper;
        private Type _type;

        public QueryVisitor(BsonMapper mapper)
        {
            _mapper = mapper;
            _type = typeof(T);
        }

        public Query Visit(Expression predicate)
        {
            var lambda = predicate as LambdaExpression;
            return VisitExpression(lambda.Body);
        }

        private Query VisitExpression(Expression expr)
        {
            if (expr.NodeType == ExpressionType.Equal) // == 
            {
                var bin = expr as BinaryExpression;
                return new QueryEquals(this.VisitMember(bin.Left), this.VisitValue(bin.Right)); 
            }
            else if (expr.NodeType == ExpressionType.NotEqual) // !=
            {
                var bin = expr as BinaryExpression;
                return Query.Not(this.VisitMember(bin.Left), this.VisitValue(bin.Right));
            }
            else if (expr.NodeType == ExpressionType.LessThan) // <
            {
                var bin = expr as BinaryExpression;
                return Query.LT(this.VisitMember(bin.Left), this.VisitValue(bin.Right));
            }
            else if (expr.NodeType == ExpressionType.LessThanOrEqual) // <=
            {
                var bin = expr as BinaryExpression;
                return Query.LTE(this.VisitMember(bin.Left), this.VisitValue(bin.Right));
            }
            else if (expr.NodeType == ExpressionType.GreaterThan) // >
            {
                var bin = expr as BinaryExpression;
                return Query.GT(this.VisitMember(bin.Left), this.VisitValue(bin.Right));
            }
            else if (expr.NodeType == ExpressionType.GreaterThanOrEqual) // >=
            {
                var bin = expr as BinaryExpression;
                return Query.GTE(this.VisitMember(bin.Left), this.VisitValue(bin.Right));
            }
            else if (expr is MethodCallExpression)
            {
                var met = expr as MethodCallExpression;
                var method = met.Method.Name;

                // StartsWith
                if (method == "StartsWith")
                {
                    var value = this.VisitValue(met.Arguments[0]);

                    return Query.StartsWith(this.VisitMember(met.Object), value);
                }
                // Equals
                else if (method == "Equals")
                {
                    var value = this.VisitValue(met.Arguments[0]);

                    return Query.EQ(this.VisitMember(met.Object), value);
                }
            }
            else if (expr is BinaryExpression && expr.NodeType == ExpressionType.AndAlso)
            {
                // AND
                var bin = expr as BinaryExpression;
                var left = this.VisitExpression(bin.Left);
                var right = this.VisitExpression(bin.Right);

                return Query.And(left, right);
            }
            else if (expr is BinaryExpression && expr.NodeType == ExpressionType.OrElse)
            {
                // OR
                var bin = expr as BinaryExpression;
                var left = this.VisitExpression(bin.Left);
                var right = this.VisitExpression(bin.Right);

                return Query.Or(left, right);
            }

            throw new NotImplementedException("Not implemented Linq expression");
        }

        private string VisitMember(Expression expr)
        {
            var member = expr as MemberExpression;
            var propInfo = member.Member as PropertyInfo;

            return this.GetBsonField(propInfo);
        }

        private BsonValue VisitValue(Expression expr)
        {
            // its a constant; Eg: "fixed string"
            if(expr is ConstantExpression)
            {
                var value = (expr as ConstantExpression).Value;

                return _mapper.Create(value);
            }

            // execute expression
            var objectMember = Expression.Convert(expr, typeof(object));
            var getterLambda = Expression.Lambda<Func<object>>(objectMember);
            var getter = getterLambda.Compile();

            return _mapper.Create(getter());
        }

        /// <summary>
        /// Get a Bson field from a simple Linq expression: x => x.CustomerName
        /// </summary>
        public string GetBsonField<TK, K>(Expression<Func<TK, K>> expr)
        {
            var member = expr.Body as MemberExpression;

            if (member == null)
            {
                throw new ArgumentException(string.Format("Expression '{0}' refers to a method, not a property.", expr.ToString()));
            }

            return this.GetBsonField(member.Member as PropertyInfo);
        }

        /// <summary>
        /// Get a bson string field based on class PropertyInfo using BsonMapper class
        /// </summary>
        private string GetBsonField(PropertyInfo propInfo)
        {
            if (propInfo == null)
            {
                throw new ArgumentException("Expression is not a property.");
            }

            // lets get mapping bettwen .NET class and BsonDocument
            var map = _mapper.GetPropertyMapper(_type);
            PropertyMapper prop;

            if (map.TryGetValue(propInfo.Name, out prop))
            {
                return prop.FieldName;
            }
            else
            {
                throw new LiteException(string.Format("Property '{0}' was not mapped into BsonDocument", propInfo.Name));
            }
        }
    }
}