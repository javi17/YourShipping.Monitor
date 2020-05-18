namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    using YourShipping.Monitor.Server.Services.Attributes;

    public class TimedHostedServiceBase : IHostedService, IDisposable
    {
        private readonly IHostApplicationLifetime applicationLifetime;

        private readonly IServiceProvider serviceProvider;

        private readonly object syncObj = new object();

        private Timer _timer;

        public TimedHostedServiceBase(ILogger<TimedHostedServiceBase> logger, IServiceProvider serviceProvider)
        {
            this.Logger = logger;
            this.applicationLifetime = serviceProvider.GetRequiredService<IHostApplicationLifetime>();
            this.serviceProvider = serviceProvider;
        }

        protected ILogger<TimedHostedServiceBase> Logger { get; }

        public void Dispose()
        {
            this._timer?.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return this.StartAsync(cancellationToken, true);
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            this.Logger.LogInformation("Timed Hosted Service is stopping.");

            this._timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        private Task DoWorkAsync(CancellationToken cancellationToken)
        {
            try
            {
                lock (this.syncObj)
                {
                    var executeMethod = this.GetType()
                        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        .FirstOrDefault(info => info.GetCustomAttribute<ExecuteAttribute>() != null);

                    if (executeMethod != null)
                    {
                        var parameters = this.ResolveParameters(executeMethod, cancellationToken);
                        var result = executeMethod.Invoke(this, parameters);
                        if (result is Task task)
                        {
                            return task;
                        }

                        return Task.FromResult(result);
                    }
                }
            }
            catch (Exception e)
            {
                this.Logger.LogError(e.Message);
            }

            return Task.CompletedTask;
        }

        private object[] ResolveParameters(MethodInfo executeMethodInfo, CancellationToken cancellationToken)
        {
            var objects = new List<object>();
            var parameterInfos = executeMethodInfo.GetParameters();
            var serviceScope = this.serviceProvider.CreateScope();

            foreach (var parameterInfo in parameterInfos)
            {
                if (parameterInfo.ParameterType == typeof(CancellationToken))
                {
                    objects.Add(cancellationToken);
                }
                else
                {
                    object service;

                    try
                    {
                        service = this.serviceProvider.GetService(parameterInfo.ParameterType);
                    }
                    catch (InvalidOperationException)
                    {
                        service = serviceScope.ServiceProvider.GetService(parameterInfo.ParameterType);
                    }

                    objects.Add(service);
                }
            }

            return objects.ToArray();
        }

        private Task StartAsync(CancellationToken cancellationToken, bool sync)
        {
            if (sync)
            {
                this.applicationLifetime.ApplicationStarted.Register(() => this.StartAsync(cancellationToken, false));
            }
            else
            {
                this.Logger.LogInformation("Timed Hosted Service running.");

                this._timer = new Timer(
                    o => this.DoWorkAsync(cancellationToken),
                    null,
                    TimeSpan.Zero,
                    TimeSpan.FromMinutes(10));
            }

            return Task.CompletedTask;
        }
    }
}