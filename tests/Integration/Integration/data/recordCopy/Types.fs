module Types

type Settings =
    {
        Title: string
        Width: int
        Height: int
        Visible: bool
        Opacity: float
        Parent: string option
        ``z index``: int
        OnClose: unit -> unit
        Combine: int -> int -> int
        Tags: string list
    }
