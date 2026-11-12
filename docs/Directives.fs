module Docs.Directives

open Feliz.ViewEngine
open Nacara.Core
open Nacara.Plugins

/// `:::cards` lays out the `:::card` links inside it in a grid.
let private cardsDirective =
    Directive.create "cards" Decode.node
    |> Directive.render (fun _ _ contents -> Html.div [ prop.className "rv-cards"; prop.children contents ])

/// `:::card title="..." href="..."`: a titled link, or a titled box without `href`.
let private cardDirective =
    Directive.create
        "card"
        (Decode.object (fun get ->
            {|
                title = get.Required.Field "title" Decode.string
                href = get.Optional.Field "href" Decode.string
            |}
        ))
    |> Directive.render (fun _ args contents ->
        Html.a
            [
                prop.className "rv-card"
                if args.href.IsSome then
                    prop.href args.href.Value
                prop.children
                    [
                        Html.strong [ prop.className "rv-card__title"; prop.text args.title ]
                        contents
                    ]
            ]
    )

let register = Directives.register [ cardsDirective; cardDirective ]
