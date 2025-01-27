﻿using HCQS.BackEnd.Common.ConfigurationModel;
using HCQS.BackEnd.Service.Contracts;
using HCQS.BackEnd.Service.Implementations;
using StackExchange.Redis;

namespace HCQS.BackEnd.API.Installers
{
    public class CacheInstaller : IInstaller
    {
        public void InstallService(IServiceCollection services, IConfiguration configuration)
        {
            var redisConfiguration = new RedisConfiguration();
            configuration.GetSection("RedisConfiguration").Bind(redisConfiguration);

            services.AddSingleton(redisConfiguration);
            if (!redisConfiguration.Enabled)
            {
                return;
            }
            else
            {
                services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfiguration.ConnectionString));
                services.AddStackExchangeRedisCache(option => option.Configuration = redisConfiguration.ConnectionString);
                services.AddSingleton<IResponseCacheService, ResponseCacheService>();
            }
        }
    }
}