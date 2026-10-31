module Brands

open Fickl.Core

[<Measure>]
type UserId

[<Measure>]
type Admin

type User =
    {
        Id: string<UserId>
        IsAdmin: bool<Admin>
        Initial: char<UserId>
    }

let id: string<UserId> = Brand.tag "u1"

let raw: string = Brand.untag id

let user =
    {
        Id = id
        IsAdmin = Brand.tagBool true
        Initial = Brand.tagChar 'u'
    }

let find (key: string<UserId>) (users: User list) = users |> List.tryFind (fun u -> u.Id = key)

let isAdmin (user: User) = Brand.untagBool user.IsAdmin

let tagAs<[<Measure>] 'm> (value: string) : string<'m> = Brand.tag value
