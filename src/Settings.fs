module Program.Settings

open Microsoft.Extensions.Configuration

type Directory = System.IO.Directory

let private configurationRoot =
    ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory ())
        .AddJsonFile("appsettings.json")
        .Build()

let getAppSettings () = configurationRoot
