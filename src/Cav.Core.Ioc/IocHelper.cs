using System.Reflection;

namespace Cav.Core.Ioc;
public static class IocHelper
{
    /// <summary>
    /// Получение и загрузка сборок в домен, начинающихся на указаную строку
    /// </summary>
    /// <param name="startName"></param>
    /// <returns></returns>
    public static IEnumerable<Assembly> LoadAssemblies(string startName)
    {
        var loadedAss = AppDomain.CurrentDomain.GetAssemblies().Where(x => x.GetName().FullName.StartsWith(startName));
        var loadedAssNames = loadedAss.Select(x => x.FullName).ToList();
        var stack = new Stack<Assembly>(loadedAss);

        do
        {
            var asm = stack.Pop();

            yield return asm;

            foreach (var reference in asm.GetReferencedAssemblies().Where(x => x.FullName.StartsWith(startName)))
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

    /// <summary>
    /// Получение маркированных типов - реализаций и опций. Игнорируются интерфейсы и абстрактные классы.
    /// </summary>
    /// <param name="assemblies">Коллекция просматриваемых сборок</param>
    /// <returns>Кортеж(Реализация, Опция)</returns>
    public static IEnumerable<(Type Implement, bool IsOption)> LoadImplements(IEnumerable<Assembly> assemblies) =>
        assemblies.SelectMany(x =>
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

}
