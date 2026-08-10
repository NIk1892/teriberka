using Amazon.Runtime;
using Amazon.S3;
using Api;
using Application;
using Domain;
using Files.Application.Handlers;
using Files.Domain;
using Files.Infrastructure;
using Infrastructure;
using Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

using Constants = Api.Constants;


namespace Files
{
    public class Configurator(WebApplicationBuilder appBuilder) : StartupConfigurator(appBuilder)
    {
        protected override void ConfigureDependencies()
        {
            Services.AddMediator(options =>
            {
                options.ServiceLifetime = ServiceLifetime.Singleton;
                options.PipelineBehaviors = [typeof(ValidatorBehavior<,>)];
            });

            var s3Config = new AmazonS3Config
            {
                ServiceURL = Configuration["S3_ENDPOINT_URL"],
                ForcePathStyle = true
            };

            var credentials = new BasicAWSCredentials(
                Configuration["S3_ACCESS_KEY"],
                Configuration["S3_SECRET_KEY"]);

            Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(credentials, s3Config));

            Services.AddSingleton<IFileService, S3FileService>();

            Services.AddSingleton<FileSaveHandler>();
        }

        public override void ConfigureEndPoints(WebApplication app)
        {
            app.MediatePutFileFromForm<FileSaveRequest>("file", "save", Constants.UrlRestrictions.Admin);
        }

        protected override void ConfigureMessageBus()
        {

        }
    }
}
