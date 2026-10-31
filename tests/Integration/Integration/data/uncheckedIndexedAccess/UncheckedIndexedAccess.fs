module UncheckedIndexedAccess

open System

let charAt (s: string) (i: int) = s.Chars i

let parseChar (s: string) = Char.Parse s

let parseGuid (s: string) = Guid.Parse s

let host (uri: Uri) = uri.Host
