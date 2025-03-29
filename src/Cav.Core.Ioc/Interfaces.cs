namespace Cav.Core.Ioc;

/// <summary>
/// Базовый интерфейс для инфраструктуры
/// </summary>
public interface IServiced { }

/// <summary>
/// Добавление класса с временем жизний "Scoped"
/// </summary>
public interface IScoped : IServiced { }

/// <summary>
/// Добавление класса с временем жизний "Singleton"
/// </summary>
public interface ISingleton : IServiced { }

/// <summary>
/// Добавление класса с временем жизний "Transient"
/// </summary>
public interface ITransient : IServiced { }

/// <summary>
/// Добавление класса как опции. Для настройки используйте атрибут <see cref="OptionConfigAttribute"/>
/// </summary>
public interface IOption : IServiced { }

/// <summary>
/// Настройка опций
/// </summary>
/// <param name="sectionPath"></param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OptionConfigAttribute(string sectionPath) : Attribute
{
    public OptionConfigAttribute() : this(string.Empty) { }

    /// <summary>
    /// Путь секции для привязки опций
    /// </summary>
    public string SectionPath { get; } = sectionPath;
    /// <summary>
    /// Иенование экземпляра опций
    /// </summary>
    public string OptionsName { get; set; } = string.Empty;
    /// <summary>
    /// Валидировать при старе приложения. В классе опций необходимые свойства помечаются атрибутами из пространств имен
    /// "Microsoft.Extensions.Options" и/или "System.ComponentModel.DataAnnotations"
    /// </summary>
    public bool ValidateOnStart { get; set; }
    //public bool BindNonPublicProperties { get; set; }
    //public bool ErrorOnUnknownConfiguration { get; set; }
}