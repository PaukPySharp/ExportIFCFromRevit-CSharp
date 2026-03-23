// ReSharper disable once CheckNamespace
// Namespace intentional: compiler compatibility types must live in System.Runtime.CompilerServices.

#if NETSTANDARD2_0 || NET48

namespace System.Runtime.CompilerServices;

/// <summary>
/// Совместимый служебный тип для поддержки <c>init</c>-сеттеров на старых target framework.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Даёт компилятору тип, который требуется для генерации кода с <c>init</c>-свойствами.
/// 2. Позволяет использовать современный синтаксис моделей при сборке под старые target framework.
///
/// Контракты:
/// 1. Тип существует только для компиляции и не содержит прикладной логики.
/// 2. Имя и namespace нельзя менять: компилятор ожидает именно этот контракт.
/// 3. Polyfill подключается только для <c>NETSTANDARD2_0</c> и <c>NET48</c>.
/// </remarks>
internal static class IsExternalInit
{
}

/// <summary>
/// Совместимый атрибут для поддержки <c>required</c>-членов на старых target framework.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Даёт компилятору тип атрибута, который используется вместе с ключевым словом <c>required</c>.
/// 2. Позволяет сохранять единый стиль моделей без отказа от <c>required</c> в старых target framework.
///
/// Контракты:
/// 1. Атрибут нужен только как часть компиляторного контракта.
/// 2. Имя, namespace и область применения нельзя менять произвольно.
/// 3. При переходе всех целевых платформ на framework, где тип уже доступен, этот polyfill можно удалить.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property,
    Inherited = false,
    AllowMultiple = false)]
internal sealed class RequiredMemberAttribute : Attribute
{
}

/// <summary>
/// Совместимый атрибут для объявления требуемых возможностей компилятора.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Поддерживает компиляторный контракт, связанный с <c>required</c>-членами.
/// 2. Даёт минимально необходимую реализацию служебного атрибута для старых target framework.
///
/// Контракты:
/// 1. Атрибут не используется как прикладной механизм в коде проекта.
/// 2. Его форма должна оставаться совместимой с ожиданиями компилятора.
/// 3. Polyfill подключается только для <c>NETSTANDARD2_0</c> и <c>NET48</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
internal sealed class CompilerFeatureRequiredAttribute : Attribute
{
    /// <summary>
    /// Инициализирует атрибут именем требуемой возможности.
    /// </summary>
    /// <param name="featureName">Имя требуемой возможности компилятора.</param>
    public CompilerFeatureRequiredAttribute(string featureName)
    {
        FeatureName = featureName;
    }

    /// <summary>
    /// Имя требуемой возможности компилятора.
    /// </summary>
    public string FeatureName { get; }

    /// <summary>
    /// Признак необязательности требуемой возможности.
    /// </summary>
    public bool IsOptional { get; set; }

    /// <summary>
    /// Стандартное имя возможности для <c>required</c>-членов.
    /// </summary>
    public const string RequiredMembers = "RequiredMembers";
}

#endif