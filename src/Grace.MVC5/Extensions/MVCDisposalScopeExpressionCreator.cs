using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Grace.DependencyInjection;
using Grace.DependencyInjection.Impl;
using Grace.DependencyInjection.Impl.Expressions;

namespace Grace.MVC.Extensions
{
    /// <summary>
    /// Disposal expression creator that uses mvc 
    /// </summary>
    public class MVCDisposalScopeExpressionCreator : IDisposalScopeExpressionCreator
    {
        private static string _disposalScopeKey = Grace.Utilities.UniqueStringId.Generate() + "-Mvc";

        private MethodInfo _addMethod;
        private MethodInfo _addMethodWithCleanup;

        public IActivationExpressionResult CreateExpression(IInjectionScope scope, IActivationExpressionRequest request,
            TypeActivationConfiguration activationConfiguration, IActivationExpressionResult result)
        {
            var closedActionType = typeof(Action<>).MakeGenericType(activationConfiguration.ActivationType);

            object disposalDelegate = null;

            if (closedActionType == activationConfiguration.DisposalDelegate?.GetType())
            {
                disposalDelegate = activationConfiguration.DisposalDelegate;
            }

            MethodInfo closedGeneric;
            Expression[] parameterExpressions;

            var resultExpression = result.Expression;

            if (resultExpression.Type != activationConfiguration.ActivationType)
            {
                resultExpression = Expression.Convert(resultExpression, activationConfiguration.ActivationType);
            }

            if (disposalDelegate != null)
            {
                closedGeneric = AddMethodWithCleanup.MakeGenericMethod(activationConfiguration.ActivationType);
                parameterExpressions = new[] { request.DisposalScopeExpression, resultExpression, Expression.Convert(Expression.Constant(disposalDelegate), closedActionType) };
            }
            else
            {
                closedGeneric = AddMethod.MakeGenericMethod(activationConfiguration.ActivationType);
                parameterExpressions = new[] { request.DisposalScopeExpression, resultExpression };
            }

            var disposalCall = Expression.Call(closedGeneric, parameterExpressions);

            var disposalResult = request.Services.Compiler.CreateNewResult(request, disposalCall);

            disposalResult.AddExpressionResult(result);

            return disposalResult;
        }


        /// <summary>
        /// Get disposal scope from http context
        /// </summary>
        /// <param name="locatorScope"></param>
        /// <returns></returns>
        public static IDisposalScope GetDisposalScopeFromHttpContext(IDisposalScope locatorScope)
        {
            if (HttpContext.Current == null || 
                !(locatorScope is DependencyInjectionContainer))
            {
                return locatorScope;
            }

            var disposalScope = HttpContext.Current.Items[_disposalScopeKey] as IDisposalScope;

            if (disposalScope != null)
            {
                return disposalScope;
            }

            disposalScope = new DisposalScope();

            HttpContext.Current.Items[_disposalScopeKey] = disposalScope;

            return disposalScope;
        }

        /// <summary>
        /// Dispose scope in http context
        /// </summary>
        public static void DisposeScopeInHttpContext()
        {
            var disposalScope = HttpContext.Current.Items[_disposalScopeKey] as IDisposalScope;

            disposalScope?.Dispose();
        }


        /// <summary>
        /// Method info for add method on IDisposalScope
        /// </summary>
        protected virtual MethodInfo AddMethod => _addMethod ??
                                          (_addMethod = GetType().GetTypeInfo().DeclaredMethods.First(m => m.Name == "AddDisposable" ));


        /// <summary>
        /// Method info for add method on IDisposalScope with cleanup delegate
        /// </summary>
        protected virtual MethodInfo AddMethodWithCleanup => _addMethodWithCleanup ??
                                                     (_addMethodWithCleanup = GetType().GetTypeInfo().DeclaredMethods.First(m => m.Name == "AddDisposableWithCleanup"));


        private static T AddDisposable<T>(IDisposalScope scope, T value) where T : IDisposalScope
        {

            return GetDisposalScopeFromHttpContext(scope).AddDisposable(value);
        }

        private static T AddDisposableWithCleanup<T>(IDisposalScope scope, T value, Action<T> cleanupAction) where T : IDisposalScope
        {
            return GetDisposalScopeFromHttpContext(scope).AddDisposable(value, cleanupAction);
        }
    }
}
