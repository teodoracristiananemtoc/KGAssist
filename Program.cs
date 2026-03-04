
using GraphTools;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;
namespace MCPServer
{
    public class SimpleFileLoggerProvider : ILoggerProvider
    {
        private readonly string path;
        public SimpleFileLoggerProvider(string path) => this.path = path;

        public ILogger CreateLogger(string categoryName) => new SimpleFileLogger(path);

        public void Dispose() { }

        private class SimpleFileLogger : ILogger
        {
            private readonly string _path;
            public SimpleFileLogger(string path) => _path = path;

            IDisposable ILogger.BeginScope<TState>(TState state)
            {
                return default!;
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var message = $"{DateTime.UtcNow:o} [{logLevel}] {formatter(state, exception)}";
                File.AppendAllText(_path, message + Environment.NewLine);
            }
        }
    }

    public partial class Program
    {
        public async static Task Main(string[] args)
        {
            WebApplication app = AppBuilder(args);
            await app.RunAsync();
        }

        public static WebApplication AppBuilder(string[] args)
        {
            var options = new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = AppContext.BaseDirectory
            };
            var builder = WebApplication.CreateBuilder(options);

            builder.Services.AddHttpClient();
            builder.Services.AddRazorPages();
            builder.Logging.ClearProviders();
            builder.Logging.AddDebug();
            builder.Logging.AddProvider(new SimpleFileLoggerProvider("EPE_MCPServer_internal_log.txt"));


            var configuration = builder.Configuration;
            builder.Services.RegisterMCPServerServices(configuration);
      
            var app = builder.Build();
            app.UseStaticFiles();
            app.UseRouting();
            app.MapRazorPages();
            app.UseSession();
            return app;
        }
    }
    public static class MCPServerConfigurationExtension
    {
        public static IServiceCollection RegisterMCPServerServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpContextAccessor();
            services.AddMemoryCache();
            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true; 
            });
            services.AddAuthorization();
            services.Configure<Deployment>(configuration.GetSection(nameof(Deployment)));
            services.Configure<CacheConfiguration>(configuration.GetSection(nameof(CacheConfiguration)));
            services.Configure<DefaultValues>(configuration.GetSection(nameof(DefaultValues)));
            services.ConfigureWritable<ConnectionConfiguration>(configuration.GetSection(nameof(ConnectionConfiguration)), Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

            services.AddSingleton<HttpClientService>();
            services.AddScoped<HttpClientWrapper>();

            services.AddEndpointsApiExplorer();
            services.AddHttpClient();
            services.AddMediatR(Cfg => Cfg.RegisterServicesFromAssemblies(AppDomain.CurrentDomain.GetAssemblies()));
            services.AddMediatR(m => m.RegisterServicesFromAssemblies(typeof(Program).Assembly));
       
            services.AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly(Assembly.GetExecutingAssembly())

                .WithToolsFromAssembly(typeof(GraphTools).Assembly)
                .WithResourcesFromAssembly(Assembly.GetExecutingAssembly())
      

            ;
            return services;
        }
        public static void ConfigureWritable<T>(
            this IServiceCollection services,
            IConfigurationSection section,
            Action<T, IServiceProvider>? configureOptionsDecryptor,
            string filePath) where T : class, new()
        {
            services.Configure<T>(section);
            services.AddScoped<IWritableOptions<T>>(provider =>
            {
                var configuration = provider.GetService<IConfiguration>() as IConfigurationRoot;
                if (configuration is null)
                    throw new InvalidOperationException("IConfigurationRoot service is not registered.");
                var environment = provider.GetService<Microsoft.Extensions.Hosting.IHostEnvironment>();
                if (environment is null)
                    throw new InvalidOperationException("IHostEnvironment service is not registered.");
                var options = provider.GetService<IOptionsSnapshot<T>>();
                if (options is null)
                    throw new InvalidOperationException($"IOptionsSnapshot<{typeof(T).Name}> service is not registered.");
                if (configureOptionsDecryptor is not null)
                {
                    configureOptionsDecryptor(options.Value, provider);
                }
                var optionsCache = provider.GetService<IOptionsMonitorCache<T>>();
                if (optionsCache is null)
                    throw new InvalidOperationException($"IOptionsMonitorCache<{typeof(T).Name}> service is not registered.");
                return new WritableOptions<T>(environment, options, optionsCache, configuration, section, filePath);
            });
            services.AddScoped<IDecryptedOption<T>>(provider =>
            {
                var options = provider.GetService<IWritableOptions<T>>();
                if (options is null)
                    throw new InvalidOperationException($"IWritableOptions<{typeof(T).Name}> service is not registered.");
                return options;
            });
        }

        public static void ConfigureWritable<T>(
                this IServiceCollection services,
                IConfigurationSection section,
                string filePath) where T : class, new()
        {
            ConfigureWritable<T>(services, section, null, filePath);
        }

    }
}