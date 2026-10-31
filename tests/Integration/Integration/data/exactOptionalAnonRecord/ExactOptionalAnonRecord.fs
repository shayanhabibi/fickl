module ExactOptionalAnonRecord

// The suite type-checks this under exactOptionalPropertyTypes, where an optional property
// holds `undefined` only when its type says so

let label (text: string option) = {| Size = 1; Text = text |}

let labelSome (text: string) = {| Size = 1; Text = Some text |}

let count (n: int option) = {| Count = n |}

let nested (x: string option option) = {| X = x |}
