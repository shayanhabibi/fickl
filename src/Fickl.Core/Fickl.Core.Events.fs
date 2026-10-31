namespace Fickl.Core

#nowarn "1182"

open Fable.Core

/// <summary>Web <c>EventTarget</c> and Node.js event emitter events as F# events, so that the <c>Event</c> and
/// <c>Observable</c> functions apply to them. Targets JavaScript and TypeScript.</summary>
/// <remarks>The event adds a JavaScript listener for each F# handler or subscription, and removes it when the handler
/// is removed or the subscription is disposed. Nothing listens while there are no handlers.</remarks>
/// <example><code lang="fsharp">
/// let clicks: IEvent&lt;MouseEvent> = JsEvent.ofTarget button "click"
/// use _ = clicks |> Observable.filter (fun e -> e.button = 0) |> Observable.subscribe onClick
/// let data: IEvent&lt;Buffer> = JsEvent.ofEmitter stream "data"
/// </code></example>
[<RequireQualifiedAccess>]
module JsEvent =
    /// <summary>The events of a type an <c>EventTarget</c> dispatches: <c>addEventListener</c> and <c>removeEventListener</c>.</summary>
    /// <param name="target">The target, such as a DOM element, an <c>AbortSignal</c> or a <c>JS.EventTarget</c> subclass.</param>
    /// <param name="eventType">The event type, such as <c>"click"</c>.</param>
    /// <remarks>The F# event's sender is the target. <c>'E</c> is the event object's type, which nothing checks.</remarks>
    let ofTarget<'E> (target: JS.EventTarget) (eventType: string) : IEvent<'E> = nativeOnly

    /// <summary>The events of a type an <c>EventTarget</c> dispatches, listened to with the given options, such as
    /// <c>passive</c> or <c>capture</c>.</summary>
    /// <param name="options">The options passed to <c>addEventListener</c>, and to <c>removeEventListener</c>.</param>
    /// <param name="target">The target.</param>
    /// <param name="eventType">The event type.</param>
    let ofTargetWith<'E>
        (options: JS.AddEventListenerOptions)
        (target: JS.EventTarget)
        (eventType: string)
        : IEvent<'E>
        =
        nativeOnly

    /// <summary>The events of a name an emitter emits: <c>on</c> and <c>off</c>.</summary>
    /// <param name="emitter">An object with <c>on</c> and <c>off</c> (or <c>removeListener</c>) methods, such as a Node.js <c>EventEmitter</c>, stream or socket.</param>
    /// <param name="eventName">The event name, such as <c>"data"</c>.</param>
    /// <remarks>The F# event value is the first argument the emitter passes; the others are dropped. The sender is the
    /// emitter. <c>'T</c> is the argument's type, which nothing checks.</remarks>
    let ofEmitter<'T> (emitter: obj) (eventName: string) : IEvent<'T> = nativeOnly

    /// <summary>Dispatches a <c>CustomEvent</c> carrying a value to a target:
    /// <c>target.dispatchEvent(new CustomEvent(eventType, { detail }))</c>.</summary>
    /// <param name="target">The target.</param>
    /// <param name="eventType">The event type.</param>
    /// <param name="detail">The value the event carries.</param>
    /// <returns><c>false</c> if a listener cancelled the event.</returns>
    let dispatch (target: JS.EventTarget) (eventType: string) (detail: 'T) : bool = nativeOnly
