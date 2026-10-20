// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.ActionFilters
{
    using System;
    using System.Globalization;
    using System.Reflection;
    using System.Threading.Tasks;

    using DotNetNuke.Entities.Modules.Actions;
    using DotNetNuke.Framework;
    using DotNetNuke.Web.Mvc.Framework.Controllers;

    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>Populates a module's actions by invoking a <c>Get{Action}Actions</c> method on the controller or a specified type.</summary>
    public class ModuleActionItemsAttribute : ActionFilterAttribute
    {
        private const string MethodNameTemplate = "Get{0}Actions";

        /// <summary>Gets or sets the type on which to invoke the actions method.</summary>
        public Type Type { get; set; }

        /// <summary>Gets or sets the name of the method to invoke.</summary>
        public string MethodName { get; set; }

        /// <inheritdoc />
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var controller = context.Controller as IDnnController;
            var result = this.InvokeMethod(context, controller);
            if (result is ModuleActionCollection moduleActions)
            {
                controller.ModuleActions = moduleActions;
            }
            else if (result is Task<ModuleActionCollection> taskResult)
            {
                controller.ModuleActions = await taskResult;
            }

            await next();
        }

        private static MethodInfo GetMethod(Type type, string methodName)
        {
            var method = type.GetMethod(methodName);

            if (method == null)
            {
                throw new NotImplementedException($"The expected method to get the module actions cannot be found. Type: {type.FullName}, Method: {methodName}");
            }

            var returnType = method.ReturnType;
            if (returnType == typeof(ModuleActionCollection) || returnType == typeof(Task<ModuleActionCollection>))
            {
                return method;
            }

            throw new InvalidOperationException("The method must return an object of type ModuleActionCollection");
        }

        private object InvokeMethod(ActionExecutingContext filterContext, IDnnController controller)
        {
            if (controller == null)
            {
                throw new InvalidOperationException("This attribute can only be applied to Controllers that implement IDnnController");
            }

            Type type;
            object instance;

            if (this.Type == null)
            {
                type = controller.GetType();
                instance = controller;
            }
            else
            {
                type = this.Type;
                instance = Reflection.CreateInstance(type);
            }

            string methodName;
            if (string.IsNullOrEmpty(this.MethodName))
            {
                var actionName = (filterContext.ActionDescriptor as ControllerActionDescriptor)?.ActionName;
                methodName = string.Format(CultureInfo.InvariantCulture, MethodNameTemplate, actionName);
            }
            else
            {
                methodName = this.MethodName;
            }

            var method = GetMethod(type, methodName);

            return method.Invoke(instance, null);
        }
    }
}
