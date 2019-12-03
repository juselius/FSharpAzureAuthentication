module Program.WebApp

open System.Net
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.OpenIdConnect
open FSharp.Core
open Giraffe
open Graph
open Program
open System.Security.Claims
open FSharp.Control.Tasks.V2
open FSharp.Data
open Thoth.Json.Net

let authenticate : HttpHandler =
    challenge OpenIdConnectDefaults.AuthenticationScheme
    |> requiresAuthentication

let signIn (next : HttpFunc) (ctx : HttpContext) =
    printfn "signin: %A" (ctx.User.Identity.Name, ctx.User.Identity.IsAuthenticated)
    authenticate next ctx

let signOut (next : HttpFunc) (ctx : HttpContext) =
    task {
        do! ctx.SignOutAsync()
        return! next ctx
    }

let private getAzureUser (ctx : HttpContext) =
    let name = ctx.User.Identity.Name
    let emailDecoder =
        Decode.field "value" (Decode.list (Decode.field "mail" Decode.string))
    let upn =
        try
            ctx.User.FindFirst(ClaimTypes.Name).Value.ToLower()
        with _ -> "unknown"
    task {
        let! token = graphAppToken ctx
        let query = graphUrlf "users?$filter=userPrincipalName eq '%s'&$select=mail" upn
        let content =
            Http.RequestString (
                query, headers = [ HttpRequestHeaders.Authorization token ]
            )
        let email =
            match Decode.fromString emailDecoder content with
            | Ok e -> try e.Head.ToLower() with _ -> "unknown"
            | Error _ -> "unknown"
        printfn "azureUser: %A" (name, upn, email)
        return (name, email)
    }

let getUser (next : HttpFunc) (ctx : HttpContext) =
    task {
        let! name, email = getAzureUser ctx
        return! json (name, email) next ctx
    }

let indexHtml (next : HttpFunc) (ctx : HttpContext) =
    task {
        let! name, email = getAzureUser ctx
        let usr = sprintf "%s <%s>" name email
        return! htmlView (Index.indexView usr) next ctx
    }

let webApp (next : HttpFunc) (ctx : HttpContext) =
    choose [
        routex "(/?)" >=> indexHtml
        route "/signin" >=> signIn >=> redirectTo false "/?signin"
        route "/signout" >=> signOut >=> redirectTo false "/?signout"
        route "/api/me" >=> getUser
    ] next ctx
