module AzureAd

open System.Globalization
open System.Threading.Tasks
open Microsoft.AspNetCore
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.OpenIdConnect
open Microsoft.IdentityModel.Protocols.OpenIdConnect
open Microsoft.IdentityModel.Tokens
open Microsoft.Extensions.Options
open FSharp.Control.Tasks.V2
open AzureAdOptions
open Graph

type Action<'a> = System.Action<'a>
type Task = System.Threading.Tasks.Task

let inline runAsTask t =
    Task.Factory.StartNew (fun _ -> t |> Async.RunSynchronously)

type ConfigureAzureOptions(azureOptions: IOptions<AzureAdOptions>, authProvider : IGraphAuthProvider) =
    let _azureOptions = azureOptions.Value
    let _authProvider = authProvider

    let authority = sprintf "%s/%s" _azureOptions.Instance _azureOptions.TenantId

    let _configure (name : string, options : OpenIdConnectOptions) =
        options.ClientId <- _azureOptions.ClientId
        options.Authority <- authority
        options.CallbackPath <- _azureOptions.CallbackPath |> Http.PathString
        options.RequireHttpsMetadata <- false
        options.ResponseType <- OpenIdConnectResponseType.CodeIdToken
        _azureOptions.Scopes + "  " + _azureOptions.GraphScopes
        |> fun s -> s.Split ' '
        |> Array.iter options.Scope.Add

        let tvp = TokenValidationParameters()
        // Ensure that User.Identity.Name is set correctly after login
        tvp.NameClaimType <- "name"
        // Instead of using the default validation (validating against a single issuer value, as we do in line of business apps),
        // we inject our own multitenant validation logic
        tvp.ValidateIssuer <- false
        // If the app is meant to be accessed by entire organizations, add your issuer validation logic here.
        //IssuerValidator <- fun (issuer, securityToken, validationParameters) -> if (myIssuerValidationLogic(issuer)) issuer else ()
        options.TokenValidationParameters <- tvp

        let oidev = OpenIdConnectEvents()

        // If your authentication logic is based on users then add your logic here
        oidev.OnTicketReceived <- fun _ -> Task.CompletedTask
        oidev.OnAuthenticationFailed <-
            fun ctx ->
                ctx.Response.Redirect("/error")
                ctx.HandleResponse() // Suppress the exception
                Task.CompletedTask
        oidev.OnAuthorizationCodeReceived <- fun ctx ->
            task {
                let code = ctx.ProtocolMessage.Code
                let! result =
                    _authProvider.GetUserAccessTokenByAuthorizationCode code
                ctx.HandleCodeRedemption (result.AccessToken, result.IdToken)
                return 1
            } :> Task
        // If your application needs to do authenticate single users, add your user validation below.
        // oidev.OnTokenValidated <- fun context ->
        //    return myUserValidationLogic(context.Ticket.Principal)
        options.Events <- oidev

    member this.GetAzureAdOptions () = _azureOptions

    interface IConfigureNamedOptions<OpenIdConnectOptions> with
        member this.Configure (name : string, options : OpenIdConnectOptions) =
            _configure (name, options)
        member this.Configure (options : OpenIdConnectOptions) =
            _configure (Options.DefaultName, options)

type AuthenticationBuilder with
    member this.AddAzureAd (options : Action<AzureAdOptions>) =
        this.Services.Configure(options) |> ignore
        this.Services.AddSingleton<IConfigureOptions<OpenIdConnectOptions>, ConfigureAzureOptions>() |> ignore
        this.AddOpenIdConnect() |> ignore
        this
