﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschränkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Squidex.Domain.Apps.Entities;
using Squidex.Domain.Apps.Entities.Apps;
using Squidex.Domain.Apps.Entities.Schemas;
using Squidex.Domain.Apps.Entities.Schemas.Commands;
using Squidex.Infrastructure;
using Squidex.Infrastructure.Commands;

namespace Squidex.Web.CommandMiddlewares
{
    public sealed class EnrichWithSchemaIdCommandMiddleware : ICommandMiddleware
    {
        private readonly IAppProvider appProvider;
        private readonly IActionContextAccessor actionContextAccessor;

        public EnrichWithSchemaIdCommandMiddleware(IAppProvider appProvider, IActionContextAccessor actionContextAccessor)
        {
            Guard.NotNull(appProvider, nameof(appProvider));
            Guard.NotNull(actionContextAccessor, nameof(actionContextAccessor));

            this.appProvider = appProvider;

            this.actionContextAccessor = actionContextAccessor;
        }

        public async Task HandleAsync(CommandContext context, NextDelegate next)
        {
            if (actionContextAccessor.ActionContext == null)
            {
                await next(context);

                return;
            }

            if (context.Command is ISchemaCommand schemaCommand && schemaCommand.SchemaId == null)
            {
                var schemaId = await GetSchemaIdAsync(context);

                schemaCommand.SchemaId = schemaId!;
            }

            if (context.Command is SchemaCommand schemaSelfCommand && schemaSelfCommand.SchemaId == Guid.Empty)
            {
                var schemaId = await GetSchemaIdAsync(context);

                schemaSelfCommand.SchemaId = schemaId?.Id ?? Guid.Empty;
            }

            await next(context);
        }

        private async Task<NamedId<Guid>?> GetSchemaIdAsync(CommandContext context)
        {
            NamedId<Guid>? appId = null;

            if (context.Command is AppCommandBase appCommand)
            {
                appId = appCommand.AppId;
            }

            if (appId == null)
            {
                appId = actionContextAccessor.ActionContext.HttpContext.Context().App?.NamedId();
            }

            if (appId != null)
            {
                var routeValues = actionContextAccessor.ActionContext.RouteData.Values;

                if (routeValues.ContainsKey("name"))
                {
                    var schemaName = routeValues["name"].ToString()!;

                    ISchemaEntity? schema;

                    if (Guid.TryParse(schemaName, out var id))
                    {
                        schema = await appProvider.GetSchemaAsync(appId.Id, id);
                    }
                    else
                    {
                        schema = await appProvider.GetSchemaAsync(appId.Id, schemaName);
                    }

                    if (schema == null)
                    {
                        throw new DomainObjectNotFoundException(schemaName, typeof(ISchemaEntity));
                    }

                    return schema.NamedId();
                }
            }

            return null;
        }
    }
}