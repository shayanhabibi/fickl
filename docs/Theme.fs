module Docs.Theme

open System.IO
open Feliz.ViewEngine
open Nacara.Core
open Nacara.Plugins
open Partas.Nacara.Theme

// The stylesheets are read once at startup; under `watch`, CSS edits take effect after a restart.

/// A stylesheet from docs/theme, with the `__BASE__` placeholder in asset URLs replaced by baseUrl.
let private themeCss name =
    File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "theme", name)).Replace("__BASE__", Spec.baseUrl)

/// theme/tokens.css, the single source of the --rv-* colours, with its [data-fickl-theme] selectors
/// mapped to the docs theme's :root[data-theme].
let private brandTokensCss =
    (themeCss "tokens.css").Replace("[data-fickl-theme=", ":root[data-theme=")

/// Colour tokens shared by both schemes. Each resolves to a --rv-* brand token for the active scheme.
let private brandColours (t: Tokens) =
    { t with
        Bg = "var(--rv-canvas)"
        BgSubtle = "var(--rv-surface)"
        BgRaised = "var(--rv-raised)"
        Border = "var(--rv-border)"
        Text = "var(--rv-text)"
        TextMuted = "var(--rv-muted)"
        Heading = "var(--rv-text)"
        Primary = "var(--rv-accent-text)"
        PrimaryContrast = "var(--rv-on-accent)"
        PrimarySubtle = "color-mix(in oklab, var(--rv-contour) 14%, var(--rv-canvas))"
        Note = "var(--rv-accent-text)"
        Tip = "var(--rv-value)"
        Warning = "var(--rv-pending)"
        Danger = "var(--rv-error)"
        CodeInlineBg = "var(--rv-raised)"
        CodeInlineBorder = "var(--rv-border)"
        CodeInlineText = "var(--rv-text)"
    }

/// Syntax colours from the accent axis and the text tokens. Value and error mark diff inserts,
/// deletes and invalid tokens only.
let private brandSyntax (s: SyntaxTokens) =
    { s with
        Comment = "var(--rv-muted)"
        Number = "var(--rv-violet)"
        Constant = "var(--rv-violet)"
        Constructor = "var(--rv-grad-b)"
        Property = "var(--rv-text)"
        Escape = "var(--rv-accent-text)"
        Keyword = "var(--rv-violet)"
        Operator = "var(--rv-muted)"
        Function = "var(--rv-grad-b)"
        Type = "var(--rv-accent-text)"
        Namespace = "var(--rv-accent-text)"
        Variable = "var(--rv-text)"
        Parameter = "var(--rv-text)"
        Punctuation = "var(--rv-muted)"
        Tag = "var(--rv-accent-text)"
        Attribute = "var(--rv-violet)"
        Preprocessor = "var(--rv-violet)"
        Invalid = "var(--rv-error)"
        Inserted = "var(--rv-value)"
        Deleted = "var(--rv-error)"
    }

/// Non-colour tokens, applied to both schemes: the dark record is independent of the light one, so a
/// value left at its default would be re-emitted under :root[data-theme="dark"].
let private sizing (t: Tokens) =
    { t with
        FontSans =
            "system-ui, -apple-system, \"Segoe UI\", Roboto, \"Helvetica Neue\", \"Noto Sans\", \"Liberation Sans\", Arial, sans-serif"
        FontMono =
            "\"Geist Mono\", ui-monospace, \"Cascadia Code\", \"JetBrains Mono\", SFMono-Regular, Menlo, Consolas, \"Liberation Mono\", monospace"
        Radius = "0.625rem"
        RadiusSm = "0.375rem"
        ContentWidth = "46rem"
        SidebarWidth = "16.5rem"
        TocWidth = "14rem"
        NavbarOpacity = "76%"
        NavbarBlur = "12px"
    }

/// Makes dark the default scheme. With no stored choice, the page renders dark before first paint and
/// the scheme picker shows "Dark" once the DOM is loaded.
let private darkByDefault =
    Html.script
        [
            prop.dangerouslySetInnerHTML
                "(()=>{try{if(!localStorage.getItem(\"nacara-theme\")){var d=document.documentElement;d.dataset.theme=\"dark\";d.dataset.themeSetting=\"dark\";document.addEventListener(\"DOMContentLoaded\",()=>{for(const s of document.querySelectorAll(\"[data-nacara-theme]\"))s.value=d.dataset.themeSetting})}}catch{}})();"
        ]

/// Geist and Geist Mono from Google Fonts. Geist sets headings, navigation and the wordmark; body
/// copy stays on the system stack.
let private fontLinks =
    [
        Html.link [ prop.rel "preconnect"; prop.href "https://fonts.googleapis.com" ]
        Html.link
            [
                prop.rel "preconnect"
                prop.href "https://fonts.gstatic.com"
                prop.custom ("crossorigin", "")
            ]
        Html.link
            [
                prop.rel "stylesheet"
                prop.href
                    "https://fonts.googleapis.com/css2?family=Geist:wght@400..700&family=Geist+Mono:wght@400..600&display=swap"
            ]
    ]

let theme (fn: ThemeOptions -> ThemeOptions) =
    Theme.defaults
    |> Theme.favIcon "favicon.svg"
    |> Theme.lightTokens (fun t ->
        { brandColours (sizing t) with
            Shadow = "0 1px 2px rgb(30 26 23 / 6%), 0 2px 8px rgb(30 26 23 / 5%)"
            ShadowFloating = "0 12px 32px -8px rgb(30 26 23 / 18%), 0 2px 6px rgb(30 26 23 / 6%)"
        }
    )
    |> Theme.darkTokens (fun t ->
        { brandColours (sizing t) with
            Shadow = "0 1px 2px rgb(0 0 0 / 45%), 0 4px 16px rgb(0 0 0 / 30%)"
            ShadowFloating = "0 16px 40px -8px rgb(0 0 0 / 55%), 0 0 0 1px var(--rv-border)"
        }
    )
    // Strings keep a code-only green per scheme; everything else is a brand token.
    |> Theme.lightSyntax (fun s -> { brandSyntax s with String = "#1F7A5A" })
    |> Theme.darkSyntax (fun s -> { brandSyntax s with String = "#9FDDBF" })
    |> Theme.layerAfter "responsive" "brand" (brandTokensCss + System.Environment.NewLine + themeCss "brand.css")
    |> Theme.layerAfter "brand" "landing" (themeCss "landing.css")
    |> Theme.layerAfter "landing" "fickl" (themeCss "fickl.css")
    |> Theme.headExtra (darkByDefault :: fontLinks)
    |> fn
