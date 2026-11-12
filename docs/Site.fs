module Docs.Site

open Feliz.ViewEngine
open Nacara.Core
open Nacara.Plugins
open Partas.Nacara.Theme

let versions = [ SiteVersion.root "0.9" ]

let theme =
    Theme.theme (
        Theme.navbar
            [
                NavbarSection("Guide", "guide", "/guide/")
                NavbarSection("Features", "features", "/features/")
                NavbarSection("Flags", "flags", "/flags/")
                NavbarDropdown(
                    "More",
                    [
                        NavbarSection("Performance", "performance", "/performance/")
                        NavbarLink("Releases", Spec.repository + "/releases")
                        NavbarLink("Fable", "https://fable.io")
                    ]
                )
            ]
        >> Theme.menu
            "guide"
            [
                Menu.section
                    "Introduction"
                    [
                        Menu.page "guide/index.md"
                        Menu.page "guide/installation.md"
                        Menu.page "guide/compatibility.md"
                        Menu.page "guide/typescript.md"
                    ]
            ]
        >> Theme.menu
            "features"
            [
                Menu.section "Features" [ Menu.page "features/index.md" ]
                Menu.section
                    "Interop"
                    [
                        Menu.page "features/types.md"
                        Menu.page "features/interfaces.md"
                        Menu.page "features/classes.md"
                        Menu.page "features/emit.md"
                        Menu.page "features/jsx.md"
                    ]
                Menu.section
                    "Runtime"
                    [
                        Menu.page "features/async.md"
                        Menu.page "features/generators.md"
                        Menu.page "features/errors.md"
                        Menu.page "features/runtime.md"
                        Menu.page "features/reflection.md"
                    ]
            ]
        >> Theme.menu
            "flags"
            [
                Menu.section "Compiler flags" [ Menu.page "flags/index.md"; Menu.page "flags/strict-options.md" ]
            ]
        >> Theme.menu
            "performance"
            [
                Menu.section "Performance" [ Menu.page "performance/index.md"; Menu.page "performance/differences.md" ]
            ]
        >> Theme.navbarEnd [ NavbarIcon("GitHub", Spec.repository, Icons.github) ]
        >> Theme.editUrl (Spec.repository + "/edit/main/docs")
        >> Theme.footer (
            Html.p
                [
                    Html.text "Fickl · "
                    Html.a [ prop.href Spec.repository; prop.text "GitHub" ]
                    Html.text " · Built with Nacara"
                ]
        )
    )

let site =
    Site.create "Fickl"
    |> Site.baseUrl Spec.baseUrl
    |> Site.origin "https://shayanhabibi.github.io"
    |> Site.output "output"
    |> Site.staticFiles "static"
    |> FicklExamples.register
    |> Readme.register
    |> Markdown.register
    |> TreeSitter.register
    |> Docs.Directives.register
    |> Sitemap.register
    // The site's own pages are checked as files; its published URLs exist only once it is deployed.
    |> LinkValidator.registerWith (
        LinkValidator.ignoring
            [
                "https://shayanhabibi.github.io/fickl/"
                "https://fonts.googleapis.com"
                "https://fonts.gstatic.com"
            ]
    )
    |> LightningCss.register
    |> Esbuild.register
    |> Nuglify.minifyHtml
    |> Versions.register versions
    |> Theme.register theme
    |> Site.collection (Theme.docs theme "content")

[<EntryPoint>]
let main argv =
    Readme.copyCharts ()
    Nacara.run site argv
