using Cav.Core.Ioc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Cav;

public static class ServiceCollectionsExtensions
{
    #region Extensions

    /// <summary>
    /// Registers all items for given assemblies
    /// </summary>
    /// <param name="host"></param>
    /// <param name="assemblies"></param>
    /// <returns></returns>
    public static IHostApplicationBuilder AddServiced(this IHostApplicationBuilder host, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentNullException.ThrowIfNull(host);

        var registerTypes = (assemblies.Any() ? assemblies : getAssemblies())
            .SelectMany(x =>
            {
                try
                {
                    return x.GetTypes();
                }
                catch (ReflectionTypeLoadException rtle)
                {
                    return rtle.Types;
                }
                catch
                {
                    return [];
                }
            })
            .Where(t => typeof(IServiced).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(x => (Implement: x!, IsOption: typeof(IOption).IsAssignableFrom(x)));

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

            foreach (var tInterface in serviceType.GetInterfaces().Where(x => !typeof(IServiced).IsAssignableFrom(x)).ToArray())
            {
                services.Add(new ServiceDescriptor(tInterface, serviceType, lifetime));
            }

            var baseClass = serviceType.BaseType ?? typeof(object);
            while (baseClass != typeof(object))
            {
                services.Add(new ServiceDescriptor(baseClass, serviceType, lifetime));
                baseClass = baseClass.BaseType ?? typeof(object);
            }
        }
    }

    #endregion Registration

    #region Helpers

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

    private static IEnumerable<Assembly> getAssemblies()
    {
        var loadedAss = AppDomain.CurrentDomain.GetAssemblies();
        var loadedAssNames = loadedAss.Select(x => x.FullName).ToList();
        var stack = new Stack<Assembly>(loadedAss);

        do
        {
            var asm = stack.Pop();

            yield return asm;

            foreach (var reference in asm.GetReferencedAssemblies())
                if (!loadedAssNames.Contains(reference.FullName))
                {
                    try
                    {
                        stack.Push(Assembly.Load(reference));
                    }
                    catch { }

                    loadedAssNames.Add(reference.FullName);
                }
        }
        while (stack.Count > 0);

    }

    #endregion Helpers
}
