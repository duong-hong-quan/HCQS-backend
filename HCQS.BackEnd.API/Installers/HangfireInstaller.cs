﻿using Hangfire;
using HCQS.BackEnd.DAL.Data;
using HCQS.BackEnd.DAL.Models;
using HCQS.BackEnd.Service.Implementations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HCQS.BackEnd.API.Installers
{
    public class HangfireInstaller : IInstaller
    {
        public void InstallService(IServiceCollection services, IConfiguration configuration)
        {
            services.AddHangfire(x => x.UseSqlServerStorage(configuration["ConnectionStrings:Host"]));
            services.AddHangfireServer();
            services.AddScoped<WorkerService>();
        }
    }
}