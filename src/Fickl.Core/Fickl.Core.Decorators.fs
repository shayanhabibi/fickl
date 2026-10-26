namespace Fickl.Core

open System

/// <summary>Emits a native JavaScript or TypeScript decorator on a class or concrete class member.</summary>
/// <remarks>Imported expressions use exported identifiers and source-relative paths. Mutable auto-properties need literals before effects.
/// isField selects native fields, whose keys cannot collide with inherited attached members; use distinct SymbolKey values or accessor mode.
/// Consumer tools execute decorators. Property and accessor decorators cannot mix; decorating both accessors requires standard tools.</remarks>
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
