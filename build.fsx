#r "paket:
nuget Fake.Core.Target
nuget Fake.DotNet.Cli //"

#load ".fake/build.fsx/intellisense.fsx"
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

Target.initEnvironment()

let solutionFile = "ReadFsharp.sln"
let fsharperProject = "../FShaper/FSharper.Tests/FSharper.Tests.fsproj"
let readFsharpProject = "test/ReadFsharp.Tests/ReadFsharp.Tests.fsproj"
let readFsharpProjectDir = "src/ReadFsharp"

// On OSX there is a bug running the tests. Requires this version of dotnet to run correctly.
let Release_2_1_302 (option: DotNet.CliInstallOptions) =
    { option with
        InstallerOptions = (fun io ->
            { io with
                Branch = "release/2.1"
            })
        Channel = None
        Version = DotNet.CliVersion.Version "2.1.302"
    }

// Lazily install DotNet SDK in the correct version if not available
let install = lazy DotNet.install Release_2_1_302

// Set general properties without arguments
let inline dotnetSimple arg = DotNet.Options.lift install.Value arg

let inline withWorkDir wd =
    DotNet.Options.lift install.Value
    >> DotNet.Options.withWorkingDirectory wd

Target.create "Clean" (fun _ ->
    !! "src/**/bin"
    ++ "src/**/obj"
    ++ "../FShaper/**/obj"
    ++ "../FShaper/**/bin"
    |> Shell.cleanDirs 
)

Target.create "Restore" (fun _ -> DotNet.restore dotnetSimple solutionFile )
Target.create "Build" (fun _ -> DotNet.build dotnetSimple solutionFile )
Target.create "Test" (fun _ -> 
  DotNet.test id fsharperProject
  DotNet.test id readFsharpProject )
  
Target.create "Package" (fun _ -> DotNet.exec (withWorkDir readFsharpProjectDir) "lambda" "package" |> ignore)
Target.create "Deploy" (fun _ -> 
  DotNet.exec 
    (withWorkDir readFsharpProjectDir) 
    "lambda deploy-function ReadFsharp --region eu-west-1 -frun dotnetcore2.1" "" 
  |> ignore)

Target.create "All" ignore

"Clean"
  ==> "Restore"
  ==> "Build"
  ==> "Test"
  ==> "Deploy"
  ==> "All"

"Test"
  ==> "Package"

Target.runOrDefault "Build"
