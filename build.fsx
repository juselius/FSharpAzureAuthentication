#r "paket: groupref build //"
#load "./.fake/build.fsx/intellisense.fsx"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open System

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.SystemHelper

Target.initEnvironment ()

let serverPath = Path.getFullName "./src"
let deployDir = Path.getFullName "./deploy"

let release = ReleaseNotes.load "RELEASE_NOTES.md"

let platformTool tool winTool =
    let tool = if Environment.isUnix then tool else winTool
    match ProcessUtils.tryFindFileOnPath tool with
    | Some t -> t
    | _ ->
        let errorMsg =
            tool + " was not found in path. " +
            "Please install it and make sure it's available from your path. " +
            "See https://safe-stack.github.io/docs/quickstart/#install-pre-requisites for more info"
        failwith errorMsg

let runTool cmd args workingDir =
    let arguments = args |> String.split ' ' |> Arguments.OfArgs
    RawCommand (cmd, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let runDotNet cmd workingDir =
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd ""
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir

Target.create "Clean" (fun _ ->
    [ deployDir ]
    |> Shell.cleanDirs
)

Target.create "Build" (fun _ ->
    runDotNet "build" serverPath
)

Target.create "Run" (fun _ ->
    async {
        runDotNet "watch run" serverPath
    }
    |> Async.RunSynchronously
    |> ignore
)

Target.create "Release" (fun _ ->
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory serverPath) "publish" "-c Release -o ../../deploy"
    if result.ExitCode <> 0 then failwithf "'dotnet publish' failed in %s" serverPath
)

Target.create "Debug" (fun _ ->
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory serverPath) "publish" "-c Debug -o ../../deploy"
    if result.ExitCode <> 0 then failwithf "'dotnet publish' failed in %s" serverPath
)

open Fake.Core.TargetOperators

"Clean"
    ==> "Build"


"Clean"
    ==> "Run"

"Clean"
    ==> "Release"

"Clean"
    ==> "Debug"

Target.runOrDefaultWithArguments "Build"
