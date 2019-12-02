module Graph

open System.Globalization
open System.Net.Http
open System.Net.Http.Headers
open System.Threading.Tasks
open System.Security.Claims
open Microsoft.Graph
open Microsoft.Extensions.Configuration
open Microsoft.Identity.Client
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open FSharp.Control.Tasks.V2
open Microsoft.Graph
open AzureAdOptions
open Microsoft.Graph
open Microsoft.Extensions.Primitives

type Uri = System.Uri
type StringSplitOptions = System.StringSplitOptions

type IGraphAuthProvider =
    abstract GetUserAccessTokenAsync : string -> Task<string>
    abstract GetUserAccessTokenByAuthorizationCode : string -> Task<AuthenticationResult>

type GraphAuthProvider(configuration : IConfiguration) =
    let azureOptions = AzureAdOptions()

    do configuration.Bind("AzureAd", azureOptions)

    let authority = sprintf "%s/%s" azureOptions.Instance azureOptions.TenantId

    let _app =
        ConfidentialClientApplicationBuilder
            .Create(azureOptions.ClientId)
            .WithClientSecret(azureOptions.ClientSecret)
            .WithAuthority(Uri(authority))
            // .WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount)
            .WithRedirectUri(azureOptions.BaseUrl + azureOptions.CallbackPath)
            .Build()

    let _scopes = azureOptions.GraphScopes.Split(' ' , StringSplitOptions.RemoveEmptyEntries)

    // More info about MSAL Client Applications: https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/Client-Applications

    let serviceErrorExn code msg =
        let e = Error()
        e.Code <- code
        e.Message <- msg
        raise (ServiceException(e))

    let getUserAccessTokenAsync (userId : string) =
        task {
            // let! account = _app.GetAccountAsync userId
            // if isNull account then
            //     return serviceErrorExn
            //         "TokenNotFound"
            //         "User not found in token cache. Maybe the server was restarted."
            // else
            try
                let! result =
                    // _app.AcquireTokenSilent(_scopes, account)
                    _app.AcquireTokenForClient(_scopes)
                        .ExecuteAsync()
                return result.AccessToken
            with _ ->
                return serviceErrorExn
                    (GraphErrorCode.AuthenticationFailure |> string)
                    "Caller needs to authenticate. Unable to retrieve the access token silently."
        }

    let getUserAccessTokenByAuthorizationCode (authorizationCode : string) =
        task {
            return!
                _app.AcquireTokenByAuthorizationCode(_scopes, authorizationCode)
                    .ExecuteAsync()
        }

    interface IGraphAuthProvider with
        // Gets an access token. First tries to get the access token from the token cache.
        // Using password (secret) to authenticate. Production apps should use a certificate.
        member this.GetUserAccessTokenAsync (userId : string) =
            getUserAccessTokenAsync userId
        member this.GetUserAccessTokenByAuthorizationCode (authCode : string) =
            getUserAccessTokenByAuthorizationCode authCode

let private getIdent (userIdentity : ClaimsIdentity) (t : string) =
    match userIdentity.FindFirst t with
    | null -> ""
    | x ->  x.Value

let graphAuthToken (userIdentity : ClaimsIdentity) (ctx : HttpContext) =
    let authProvider = ctx.RequestServices.GetService<IGraphAuthProvider>()
    let id1 = getIdent userIdentity objectIdentifierType
    let id2 = getIdent userIdentity tenantIdType
    let identifier = id1 + "." + id2
    task {
        let! accessToken = authProvider.GetUserAccessTokenAsync identifier
        let authHeader = AuthenticationHeaderValue("Bearer", accessToken)
        return authHeader.ToString ()
    }

let graphAppToken (ctx : HttpContext) =
    let authProvider = ctx.RequestServices.GetService<IGraphAuthProvider>()
    task {
        let! accessToken = authProvider.GetUserAccessTokenAsync ""
        let authHeader = AuthenticationHeaderValue("Bearer", accessToken)
        return authHeader.ToString ()
    }

let graphAuthProvider
    (authProvider : IGraphAuthProvider)
    (userIdentity : ClaimsIdentity)
    (requestMessage : HttpRequestMessage) =
    // Get user's id for token cache.
    let id1 = getIdent userIdentity objectIdentifierType
    let id2 = getIdent userIdentity tenantIdType
    let identifier = id1 + "." + id2
    task {
        // Passing tenant ID to the sample auth provider to use as a cache key
        let! accessToken = authProvider.GetUserAccessTokenAsync identifier
        // Append the access token to the request
        requestMessage.Headers.Authorization <-
            AuthenticationHeaderValue("Bearer", accessToken)
        // requestMessage.Headers.Add("SampleID", "aspnetcore-connect-sample")
    }

type IGraphSdkHelper =
    abstract member GetAuthenticatedClient : ClaimsIdentity -> GraphServiceClient

type GraphSdkHelper (authProvider : IGraphAuthProvider) =
    let _authProvider = authProvider

    let authProvider (userIdentity : ClaimsIdentity) (requestMessage : HttpRequestMessage) =
        graphAuthProvider _authProvider userIdentity requestMessage :> Task

    // Get an authenticated Microsoft Graph Service client.
    let getAuthenticatedClient (userIdentity : ClaimsIdentity) =
        GraphServiceClient(
            DelegateAuthenticationProvider(fun req ->
                authProvider userIdentity req
            )
        )

    interface IGraphSdkHelper with
        member this.GetAuthenticatedClient (userIdentity : ClaimsIdentity) =
            getAuthenticatedClient userIdentity

let graphUrl x = "https://graph.microsoft.com/v1.0/" + x

let inline graphUrlf (fmt : Printf.StringFormat<_, _>) =
    let f =  Printf.StringFormat<_, _>("https://graph.microsoft.com/v1.0/" + fmt.Value)
    sprintf f


// using GraphSdkHelper:
// let graphSdk = ctx.RequestServices.GetService<IGraphSdkHelper>()
// let graphClient = graphSdk.GetAuthenticatedClient(cid)
// let uid = ctx.User.FindFirst "preferred_username"
// let! r = graphClient.Users.[uid.Value].Request().GetAsync()
// let e = Newtonsoft.Json.JsonConvert.SerializeObject r
// printfn "sdk: %A" e

// let getUserJson (graphClient : GraphServiceClient) (email : string) (ctx : HttpContext) =
//     try
//         let user = await graphClient.Users[email].Request().GetAsync();
//         JsonConvert.SerializeObject(user, Formatting.Indented);
//     with e -> e.Message
