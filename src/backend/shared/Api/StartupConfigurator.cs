using Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api
{
    public abstract class StartupConfigurator(WebApplicationBuilder appBuilder)
    {
        protected IServiceCollection Services => appBuilder.Services;
        protected IConfiguration Configuration => appBuilder.Configuration;

        public void Configure()
        {
            ConfigureDependencies();

            ConfigureBase();

            ConfigureMessageBus();
        }

        public virtual void ConfigureApplication(WebApplication app)
        {
            app.UseExceptionHandler();

            if (app.Environment.IsDevelopment())
            {
                // app.UseDeveloperExceptionPage();
                 app.UseSwagger();
            }

            app.UseHttpsRedirection();

            app.ConfigureHealthCheck();
        }

        protected virtual void ConfigureDependencies()
        {
        }

        public abstract void ConfigureEndPoints(WebApplication app);

        private void ConfigureBase()
        {
            Services.AddExceptionHandling();

            Services.AddApiServices();

            Services.AddHealthChecks();
        }


        #region Protected
        protected virtual void ConfigureMessageBus() { }


        #endregion
    }
}
