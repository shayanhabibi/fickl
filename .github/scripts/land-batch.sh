#!/usr/bin/env bash
# fickl: merges several draft PRs into one batch branch and opens one ready PR for it, so the full CI
# runs once for the batch instead of once per PR.
#
#   .github/scripts/land-batch.sh [--dry-run] [--test] <pr> <pr>...
#
# The PRs are merged with --no-ff onto the latest main, in the order given. A conflict stops the
# script and names the PR. --test runs the TypeScript, JavaScript and integration suites on the batch
# before pushing. --dry-run merges and tests, but pushes nothing and opens no PR.
#
# Land the batch PR with "Create a merge commit", not a squash: each PR's commits then reach main
# unchanged, GitHub marks each PR as merged, and the changelog keeps one entry per feature. So each PR
# should be one conventional commit before it is batched; the script warns about any that are not.
# If CI fails on the batch, run the script again without the PR that broke it.
set -euo pipefail

REPO=${REPO:-shayanhabibi/fickl}
dry_run=false
run_tests=false
prs=()
for arg in "$@"; do
  case "$arg" in
    --dry-run) dry_run=true ;;
    --test) run_tests=true ;;
    -h | --help) sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    [0-9]*) prs+=("$arg") ;;
    *) echo "unknown argument: $arg" >&2; exit 2 ;;
  esac
done
[ ${#prs[@]} -gt 0 ] || { echo "usage: $0 [--dry-run] [--test] <pr>..." >&2; exit 2; }

remote=${REMOTE:-$(git remote -v | awk -v r="$REPO" '$2 ~ "[/:]" r "(\\.git)?$" && $3 == "(fetch)" { print $1; exit }')}
[ -n "$remote" ] || { echo "no remote points at $REPO; set REMOTE" >&2; exit 2; }

worktree=$(mktemp -d)
cleanup() {
  git worktree remove --force "$worktree" 2> /dev/null || true
  for p in "${prs[@]}"; do git update-ref -d "refs/batch-pr/$p" 2> /dev/null || true; done
}
trap cleanup EXIT
git fetch -q "$remote" main
refspecs=()
for p in "${prs[@]}"; do refspecs+=("+refs/pull/$p/head:refs/batch-pr/$p"); done
git fetch -q "$remote" "${refspecs[@]}"

titles=()
for p in "${prs[@]}"; do
  read -r state base title < <(gh pr view "$p" -R "$REPO" --json state,baseRefName,title \
    --jq '[.state, .baseRefName, .title] | join(" ")')
  [ "$state" = OPEN ] || { echo "#$p is $state" >&2; exit 1; }
  [ "$base" = main ] || { echo "#$p targets $base, not main" >&2; exit 1; }
  titles+=("$title")
  subjects=$(git log --format=%s "$remote/main..refs/batch-pr/$p")
  if [ "$(wc -l <<< "$subjects")" -gt 1 ] || ! grep -Eq '^[a-z]+(\([^)]+\))?!?: ' <<< "$subjects"; then
    echo "warning: #$p is not one conventional commit; squash it first for a clean changelog:" >&2
    head -5 <<< "$subjects" | sed 's/^/  /' >&2
  fi
done

branch="batch/$(date +%Y-%m-%d)"
n=2
while git ls-remote --exit-code -q "$remote" "refs/heads/$branch" > /dev/null; do
  branch="batch/$(date +%Y-%m-%d)-$n"
  n=$((n + 1))
done

git worktree add -q --detach "$worktree" "$remote/main"
# the commit hooks need .husky/_, which git does not track
(cd "$worktree" && dotnet tool restore > /dev/null && dotnet husky install > /dev/null)

for i in "${!prs[@]}"; do
  p=${prs[$i]}
  if ! out=$(git -C "$worktree" merge -q --no-ff -m "chore: merge #$p ${titles[$i]}" "refs/batch-pr/$p" 2>&1); then
    conflicts=$(git -C "$worktree" diff --name-only --diff-filter=U)
    git -C "$worktree" merge --abort 2> /dev/null || true
    if [ -z "$conflicts" ]; then
      echo "$out" >&2
      echo "merging #$p failed" >&2
      exit 1
    fi
    sed 's/^/  conflict: /' <<< "$conflicts" >&2
    so_far="main"
    [ "$i" -gt 0 ] && so_far="main plus #$(echo "${prs[*]:0:$i}" | sed 's/ /, #/g')"
    echo "#$p conflicts with $so_far. Rebase it on main, or leave it out." >&2
    exit 1
  fi
  echo "merged #$p ${titles[$i]}"
done

if $run_tests; then
  for target in typescript javascript integration; do
    echo "testing $target"
    (cd "$worktree" && dotnet run --project src/Fickl.Build -- test "$target")
  done
fi

list=$(for i in "${!prs[@]}"; do echo "- #${prs[$i]} ${titles[$i]}"; done)
body="Lands these PRs together, so CI runs once for all of them:

$list

Merge with **Create a merge commit**, not a squash, so each PR keeps its own commit on main and is marked as merged.

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
title="chore: land #$(IFS=' '; echo "${prs[*]}" | sed 's/ /, #/g')"

if $dry_run; then
  echo "dry run: would push $(git -C "$worktree" rev-parse --short HEAD) to $remote/$branch and open:"
  echo "  $title"
  sed 's/^/  /' <<< "$list"
  exit 0
fi

git -C "$worktree" push -q "$remote" "HEAD:refs/heads/$branch"
gh pr create -R "$REPO" --base main --head "$branch" --title "$title" --body "$body"
