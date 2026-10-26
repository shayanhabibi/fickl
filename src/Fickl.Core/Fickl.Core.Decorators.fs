namespace Fickl.Core

open System

/// <summary>Emits a native JavaScript or TypeScript decorator on a class or concrete class member.</summary>
/// <remarks>Imported decorator expressions begin with an exported identifier; paths are relative to the declaring file.
/// Mutable auto-properties need literal initializers before constructor effects; isField selects native field storage.
/// Property-level decorators cannot be combined with explicit accessor decorators.
/// Decorating both accessors explicitly requires standard decorators; legacy tools allow only one.
/// Decorators execute through the consumer's standard or legacy decorator toolchain.</remarks>
[<AttributeUsage(AttributeTargets.Class
                 ||| AttributeTargets.Struct
                 ||| AttributeTargets.Method
                 ||| AttributeTargets.Property
                 ||| AttributeTargets.Field,
                 AllowMultiple = true)>]
type JsDecoratorAttribute(expression: string, from: string, isField: bool) =
    inherit Attribute()
    /// <summary>Emits a global decorator expression.</summary>
    new(expression: string) = JsDecoratorAttribute(expression, "", false)
    /// <summary>Emits a decorator expression whose root identifier is imported from a module.</summary>
    new(expression: string, from: string) = JsDecoratorAttribute(expression, from, false)
    /// <summary>Emits a global decorator, optionally selecting native field storage for an auto-property.</summary>
    new(expression: string, isField: bool) = JsDecoratorAttribute(expression, "", isField)
