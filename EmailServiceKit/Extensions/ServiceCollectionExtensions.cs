using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEmailServiceKit(this IServiceCollection services, IConfiguration config)
    {
        // Register queue repository using connection string from config
        var conn = config.GetValue<string>("Email:Sqlite:ConnectionString") ?? "Data Source=emailqueue.db";
        services.AddSingleton<IEmailQueueRepository>(_ => new SqliteEmailQueueRepository(conn));

        // Register template renderer
        services.AddSingleton<ITemplateRenderer, RazorTemplateRenderer>();

        // Register senders (choose primary by config)
        var useGmail = config.GetValue<bool>("Email:Gmail:Enabled");
        if (useGmail)
        {
            // example create GoogleCredential from JSON in config or file.
            var credJson = config.GetValue<string>("Email:Gmail:CredentialJson") ?? string.Empty;
            var credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(credJson)
                .CreateScoped(Google.Apis.Gmail.v1.GmailService.Scope.GmailSend);
            services.AddSingleton<IEmailSender>(_ => new GmailApiEmailSender(credential));
        }
        else
        {
            var host = config.GetValue<string>("Email:Smtp:Host") ?? "localhost";
            var port = config.GetValue<int>("Email:Smtp:Port");
            var useSsl = config.GetValue<bool>("Email:Smtp:UseSsl");
            var user = config.GetValue<string>("Email:Smtp:Username");
            var pass = config.GetValue<string>("Email:Smtp:Password");
            services.AddSingleton<IEmailSender>(_ => new SmtpEmailSender(new SmtpOptions(host, port, useSsl, user, pass)));
        }

        services.AddHostedService<EmailDispatcherHostedService>();
        return services;
    }
}
