﻿using HCQS.BackEnd.Common.ConfigurationModel;
using HCQS.BackEnd.Service.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text;

namespace HCQS.BackEnd.API.Attributes
{
    public class CacheAttribute : Attribute, IAsyncActionFilter
    {
        private readonly int _timeToLiveSeconds;

        public CacheAttribute(int timeToLiveSeconds)
        {
            _timeToLiveSeconds = timeToLiveSeconds;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var cacheConfiguration = context.HttpContext.RequestServices.GetRequiredService<RedisConfiguration>();
            if (!cacheConfiguration.Enabled)
            {
                await next();
                return;
            }
            var cacheService = context.HttpContext.RequestServices.GetRequiredService<IResponseCacheService>();
            var cacheKey = GenerateCacheKeyFromRequest(context.HttpContext.Request);
            var cacheResponse = await cacheService.GetCacheResponseAsync(cacheKey);
            if (!string.IsNullOrEmpty(cacheResponse))
            {
                var contentResult = new ContentResult { Content = cacheResponse, ContentType = "application/json", StatusCode = 200 };
                context.Result = contentResult;
                return;
            }

            var excutedContext = await next();
            if (excutedContext.Result is OkObjectResult okObjectResult)
            {
                await cacheService.SetCacheResponseAsync(cacheKey, okObjectResult.Value, TimeSpan.FromSeconds(_timeToLiveSeconds));
            }
        }

        private static string GenerateCacheKeyFromRequest(HttpRequest request)
        {
            var keyBuilder = new StringBuilder();
            keyBuilder.Append($"{request.Path}");
            foreach (var (key, value) in request.Query.OrderBy(x => x.Key))
            {
                keyBuilder.Append($"{key}-{value}");
            }
            return keyBuilder.ToString();
        }
    }
}