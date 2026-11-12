/// <summary>
/// Pages that show a block of the repository README, such as the benchmark charts the release workflow draws and the
/// table of differences from Fable that the differences workflow checks. A line <c>{{readme:benchmarks}}</c> is
/// replaced by the block between <c>&lt;!-- fickl-benchmarks:start --&gt;</c> and its end marker, so the site shows
/// what the workflows last committed.
/// </summary>
module Docs.Readme

open System.IO
open System.Text.RegularExpressions
open Nacara.Core

let private repository = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))

let private readme =
    lazy (File.ReadAllText(Path.Combine(repository, "README.md")).Replace("\r\n", "\n"))

let private block name =
    let m =
        Regex.Match(
            readme.Force(),
            $"<!-- fickl-%s{name}:start -->\n(.*?)\n<!-- fickl-%s{name}:end -->",
            RegexOptions.Singleline
        )

    if m.Success then
        Some m.Groups[1].Value
    else
        None

/// <summary>Where the charts the README shows are copied, under docs/static.</summary>
let private chartFolder = "benchmarks"

/// <summary>
/// Copies the charts the README's benchmark block shows into docs/static/benchmarks, where the site serves them.
/// Run before the build.
/// </summary>
let copyCharts () =
    match block "benchmarks" with
    | None -> ()
    | Some text ->
        let target = Path.Combine(__SOURCE_DIRECTORY__, "static", chartFolder)
        Directory.CreateDirectory target |> ignore

        for m in Regex.Matches(text, @"\]\((notes/benchmarks/[^)]+\.svg)\)") do
            let source = Path.Combine(repository, m.Groups[1].Value)
            File.Copy(source, Path.Combine(target, Path.GetFileName source), true)

/// <summary>Points the README's links at the repository, and its charts at the copies the site serves.</summary>
let private rewrite (text: string) =
    let text =
        Regex.Replace(text, @"\]\(notes/benchmarks/([^)]+\.svg)\)", $"](%s{Spec.baseUrl}%s{chartFolder}/$1)")

    Regex.Replace(text, @"\]\((?!https?://|/|#)([^)]+)\)", $"](%s{Spec.repository}/blob/main/$1)")

let private placeholder =
    Regex(@"^\{\{readme:([a-z-]+)\}\}\s*$", RegexOptions.Multiline)

let private transformPage (context: TransformContext) (page: Page) =
    if not (page.Body.Contains "{{readme:") then
        page
    else
        let body =
            placeholder.Replace(
                page.Body.Replace("\r\n", "\n"),
                fun m ->
                    match block m.Groups[1].Value with
                    | Some text -> rewrite text
                    | None ->
                        context.Diagnostics.Add(
                            Diagnostic.error "readme" $"README.md has no block named fickl-%s{m.Groups[1].Value}"
                        )

                        ""
            )

        { page with Body = body }

/// <summary>Registers the placeholder. Its transform runs before Markdown.</summary>
let register site =
    site
    |> Site.plugin
        { new IPlugin with
            member _.Name = "readme-blocks"

            member _.Configure registry =
                let registry =
                    registry
                    |> Registry.transform
                        {
                            Name = "readme-blocks"
                            Extensions = [ ".md"; ".markdown" ]
                            Transform = transformPage
                        }

                { registry with
                    Transforms =
                        List.last registry.Transforms
                        :: List.take (registry.Transforms.Length - 1) registry.Transforms
                }
        }
