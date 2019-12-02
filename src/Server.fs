open System
open System.Net
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.OpenIdConnect
open Microsoft.AspNetCore.HttpsPolicy
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Configuration
open FSharp.Core
open Giraffe
open Graph
open AzureAd
open Program

let tryGetEnv = Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x
let publicPath = IO.Path.GetFullPath "."
let port = "SERVER_PORT" |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

let configureLogging (builder : ILoggingBuilder) =
    let filter (l : LogLevel) = l.Equals LogLevel.Error
    builder.AddFilter(filter).AddConsole().AddDebug() |> ignore

let configureCors (builder : CorsPolicyBuilder) =
    builder.WithOrigins([|
            "https://login.microsoftonline.com"
            "*"
        |])
       .AllowAnyMethod()
       .AllowAnyHeader()
       |> ignore

let appSettings (ctx : WebHostBuilderContext) (config : IConfigurationBuilder) =
    config
        .AddJsonFile("appsettings.json", false, true)
        .AddEnvironmentVariables() |> ignore

let cookiePolicyOptions (opt : CookiePolicyOptions) =
        opt.CheckConsentNeeded <- fun _ -> true
        opt.MinimumSameSitePolicy <- Http.SameSiteMode.None

let authenticationOptions (opt : AuthenticationOptions) =
    opt.DefaultScheme <- CookieAuthenticationDefaults.AuthenticationScheme
    opt.DefaultChallengeScheme <- OpenIdConnectDefaults.AuthenticationScheme
    opt.DefaultAuthenticateScheme <- CookieAuthenticationDefaults.AuthenticationScheme

let hstsOptions (opt : HstsOptions) =
    opt.IncludeSubDomains <- true
    opt.MaxAge <- TimeSpan.FromDays(365.0)

let staticFileOptions =
    let opt = StaticFileOptions()
    opt

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    match env.EnvironmentName  with
    | "Production" -> app.UseGiraffeErrorHandler errorHandler
    | _  -> app.UseDeveloperExceptionPage()
    |> ignore
    printfn "Webroot path: %s" publicPath
    app.UseDefaultFiles()
        .UseHttpsRedirection()
        .UsePathBase(PathString "/")
        .UseStaticFiles(staticFileOptions)
        .UseCookiePolicy()
        .UseCors(configureCors)
        .UseAuthentication()
        .UseSession()
        .UseGiraffe WebApp.webApp

type IJsonSerializer = Giraffe.Serialization.Json.IJsonSerializer

let configureServices (services : IServiceCollection) =
    let sp  = services.BuildServiceProvider()
    let config = sp.GetService<IConfiguration>()
    let jsonSerializer = Thoth.Json.Giraffe.ThothSerializer()
    services.AddGiraffe() |> ignore
    services.AddSingleton<IJsonSerializer>(jsonSerializer) |> ignore
    services.Configure(cookiePolicyOptions) |> ignore
    services.Configure(hstsOptions) |> ignore
    services.AddCors() |> ignore
    services.AddSingleton<IGraphAuthProvider, GraphAuthProvider>() |> ignore
    services.AddTransient<IGraphSdkHelper, GraphSdkHelper>() |> ignore
    services.AddResponseCaching() |> ignore
    services.AddDistributedMemoryCache() |> ignore
    services.AddSession() |> ignore
    services.AddHttpContextAccessor() |> ignore
    services.AddAuthentication(authenticationOptions)
        .AddAzureAd(fun options -> config.Bind("AzureAd", options))
        .AddCookie() |> ignore

WebHost
    .CreateDefaultBuilder()
    .UseKestrel()
    .ConfigureAppConfiguration(appSettings)
    .Configure(Action<IApplicationBuilder> configureApp)
    .ConfigureServices(configureServices)
    .UseWebRoot(publicPath)
    .UseUrls("https://0.0.0.0:" + port.ToString() + "/")
    .ConfigureLogging(configureLogging)
    .Build()
    .Run()
