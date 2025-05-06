using Cav.Core.Ioc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Cav;

public static class ServiceCollectionsExtensions
{
    #region Extensions

    /// <summary>
    /// Регитсрация класов из сборок. 
    /// </summary>
    /// <param name="host">Хост приложения</param>
    /// <param name="assemblyNameStartWith">Начало названия просматриваемыз сборок. Если не указано - берется название сборки до первой точки</param>
    /// <returns></returns>
    public static IHostApplicationBuilder AddServiced(this IHostApplicationBuilder host, string? assemblyNameStartWith = null)
    {
        if (string.IsNullOrWhiteSpace(assemblyNameStartWith))
            assemblyNameStartWith = Assembly.GetEntryAssembly()?.GetName().Name;

        if (string.IsNullOrWhiteSpace(assemblyNameStartWith))
            throw new InvalidOperationException("Not found EntryAssembly");

        assemblyNameStartWith = assemblyNameStartWith.Split('.').First();

        var asmbl = IocHelper.LoadAssemblies(assemblyNameStartWith);

        return host.AddServiced(asmbl.First(), asmbl.Skip(1).ToArray());
    }

    /// <summary>
    /// Регистрация помеченных классов из указанных сборок
    /// </summary>
    /// <param name="host"></param>
    /// <param name="assemblies"></param>
    /// <returns></returns>
    public static IHostApplicationBuilder AddServiced(this IHostApplicationBuilder host, Assembly assembly, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentNullException.ThrowIfNull(host);

        var registerTypes = IocHelper.LoadImplements(assemblies.Concat([assembly]));

        registerOptions(host.Services, registerTypes.Where(x => x.IsOption).Select(x => x.Implement));
        registerWithTypes(host.Services, registerTypes.Where(x => !x.IsOption).Select(x => x.Implement));

        return host;
    }

    /// <summary>
    /// Registers all items for calling assembly
    /// </summary>
    /// <param name="host"></param>
    /// <returns></returns>
    public static IHostApplicationBuilder AddServicedForCallingAssembly(this IHostApplicationBuilder host) =>
        AddServiced(host, Assembly.GetCallingAssembly());

    #endregion

    #region Registration
    private static void registerOptions(IServiceCollection services, IEnumerable<Type> optionsTypes)
    {

        var miAddOptions = typeof(OptionsServiceCollectionExtensions).GetMethod(nameof(OptionsServiceCollectionExtensions.AddOptions), [typeof(IServiceCollection), typeof(string)])
            ?? throw new InvalidOperationException($"не найден метод {typeof(OptionsServiceCollectionExtensions).FullName}.{nameof(OptionsServiceCollectionExtensions.AddOptions)}({typeof(IServiceCollection).FullName}, {typeof(string).FullName})");
        var miBindConfiguration = typeof(OptionsBuilderConfigurationExtensions).GetMethod(nameof(OptionsBuilderConfigurationExtensions.BindConfiguration))
            ?? throw new InvalidOperationException($"не найден метод {typeof(OptionsBuilderConfigurationExtensions).FullName}.{nameof(OptionsBuilderConfigurationExtensions.BindConfiguration)}");

        var miValidateDataAnnotations = typeof(OptionsBuilderDataAnnotationsExtensions).GetMethod(nameof(OptionsBuilderDataAnnotationsExtensions.ValidateDataAnnotations))
            ?? throw new InvalidOperationException($"не найден метод {typeof(OptionsBuilderDataAnnotationsExtensions).FullName}.{nameof(OptionsBuilderDataAnnotationsExtensions.ValidateDataAnnotations)}");
        var miValidateOnStart = typeof(OptionsBuilderExtensions).GetMethod(nameof(OptionsBuilderExtensions.ValidateOnStart))
            ?? throw new InvalidOperationException($"не найден метод {typeof(OptionsBuilderExtensions).FullName}.{nameof(OptionsBuilderExtensions.ValidateOnStart)}");

        foreach (var optionType in optionsTypes)
        {
            var configs = optionType.GetCustomAttributes<OptionConfigAttribute>().ToList();

            if (!configs.Any())
                configs.Add(new());

            foreach (var conf in configs)
            {
                var optionsBuilder = miAddOptions.MakeGenericMethod(optionType).Invoke(null, [services, conf.OptionsName]);
                miBindConfiguration.MakeGenericMethod(optionType).Invoke(null, [optionsBuilder, conf.SectionPath, null]);

                if (conf.ValidateOnStart)
                {
                    miValidateDataAnnotations.MakeGenericMethod(optionType).Invoke(null, [optionsBuilder]);
                    miValidateOnStart.MakeGenericMethod(optionType).Invoke(null, [optionsBuilder]);
                }
            }
        }
    }

    private static void registerWithTypes(IServiceCollection services, IEnumerable<Type> servicesTypes)
    {
        foreach (var serviceType in servicesTypes)
        {
            var lifetime = getLifetime(serviceType);

            services.Add(new ServiceDescriptor(serviceType, serviceType, lifetime));

            foreach (var tInterface in serviceType.GetInterfaces()
                .Where(x =>
                    !typeof(IServiced).IsAssignableFrom(x) &&
                    !typeof(IDisposable).IsAssignableFrom(x)
                    )
                .ToArray())
            {
                services.Add(new ServiceDescriptor(tInterface, serviceType, lifetime));
            }

            var baseClass = serviceType.BaseType ?? typeof(object);
            while (baseClass != typeof(object))
            {
                if (baseClass.IsAbstract)
                    services.Add(new ServiceDescriptor(baseClass, serviceType, lifetime));
                baseClass = baseClass.BaseType ?? typeof(object);
            }
        }
    }

    #endregion Registration

    private static ServiceLifetime getLifetime(Type serviceToRegister)
    {
        var lifetime = ServiceLifetime.Transient;

        if (typeof(IScoped).IsAssignableFrom(serviceToRegister))
        {
            lifetime = ServiceLifetime.Scoped;
        }
        else if (typeof(ISingleton).IsAssignableFrom(serviceToRegister))
        {
            lifetime = ServiceLifetime.Singleton;
        }

        return lifetime;
    }
}
