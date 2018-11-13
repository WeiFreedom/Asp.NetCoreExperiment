﻿using App.Metrics;
using App.Metrics.Health;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AppMetricsDemo01
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }


        public void ConfigureServices(IServiceCollection services)
        {

            #region Metrics监控配置
            string IsOpen = Configuration.GetSection("InfluxDB:IsOpen").Value.ToLower();
            if (IsOpen == "true")
            {
                string database = Configuration.GetSection("InfluxDB")["DataBaseName"];
                string InfluxDBConStr = Configuration.GetSection("InfluxDB")["ConnectionString"];
                string app = Configuration.GetSection("InfluxDB")["app"];
                string env = Configuration.GetSection("InfluxDB")["env"];
                string username = Configuration.GetSection("InfluxDB")["username"];
                string password = Configuration.GetSection("InfluxDB")["password"];
                var uri = new Uri(InfluxDBConStr);

                var metrics = AppMetrics.CreateDefaultBuilder()
                .Configuration.Configure(
                options =>
                {
                    options.AddAppTag(app);
                    options.AddEnvTag(env);
                })
                .Report.ToInfluxDb(
                options =>
                {
                    options.InfluxDb.BaseUri = uri;
                    options.InfluxDb.Database = database;
                    options.InfluxDb.UserName = username;
                    options.InfluxDb.Password = password;
                    options.InfluxDb.CreateDataBaseIfNotExists = true;
                    options.HttpPolicy.BackoffPeriod = TimeSpan.FromSeconds(30);
                    options.HttpPolicy.FailuresBeforeBackoff = 5;
                    options.HttpPolicy.Timeout = TimeSpan.FromSeconds(10);
                    options.FlushInterval = TimeSpan.FromSeconds(5);
                })
                .Build();


                services.AddMetrics(metrics);
                services.AddMetricsReportScheduler();
                services.AddMetricsTrackingMiddleware();
                services.AddMetricsEndpoints((opt) =>
                {
                    opt.MetricsEndpointEnabled = true;
                    opt.EnvironmentInfoEndpointEnabled = true;
                    opt.MetricsTextEndpointEnabled = true;

                });


                var healthMetrics = AppMetricsHealth.CreateDefaultBuilder()
                    .HealthChecks.RegisterFromAssembly(services)

                    .HealthChecks.AddCheck(new ABC("abc"))
                    .HealthChecks.AddChecks(
                    new HealthCheck[] {
                        new HealthCheck("DatabaseConnected",()=> new ValueTask<HealthCheckResult>(HealthCheckResult.Healthy("Database Connection OK"))) }    
                    )
                    .Configuration.Configure(options =>
                    {
                        options.Enabled = true;
                    })
                    .BuildAndAddTo(services);

                services.AddHealth(healthMetrics);
                services.AddHealthEndpoints();

            }

            #endregion
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }


        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();
            #region 使用中间件Metrics
            string IsOpen = Configuration.GetSection("InfluxDB")["IsOpen"].ToLower();
            if (IsOpen == "true")
            {
                app.UseMetricsAllMiddleware();
                app.UseMetricsAllEndpoints(); ;
                app.UseHealthAllEndpoints();
            }
            #endregion
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }

    public class ABC : HealthCheck
    {
        public ABC(string name = "ABC") : base(name)
        {
        }

        protected override ValueTask<HealthCheckResult> CheckAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return new ValueTask<HealthCheckResult>(result: HealthCheckResult.Unhealthy());

        }

    }
}
