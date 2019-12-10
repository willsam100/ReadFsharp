#r "paket:
nuget Fake.Core.Target
nuget FSharp.Data
nuget Fake.DotNet.Cli //"

#load ".fake/build.fsx/intellisense.fsx"
open System
open FSharp.Data
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

Target.initEnvironment()

let [<Literal>] AwsLambdaVersionResponse = 
  """ 
    {
        "Versions": [
            {
                "FunctionName": "ReadFsharp",
                "FunctionArn": "arn:aws:lambda:eu-west-1:532053120208:function:ReadFsharp:$LATEST",
                "Runtime": "dotnetcore2.1",
                "Role": "arn:aws:iam::532053120208:role/service-role/ReadFsharpRole",
                "Handler": "ReadFsharp::Setup+LambdaEntryPoint::FunctionHandlerAsync",
                "CodeSize": 12459694,
                "Description": "",
                "Timeout": 15,
                "MemorySize": 512,
                "LastModified": "2019-11-29T23:12:45.862+0000",
                "CodeSha256": "zLxWyAPYHmSKcB3itCJWob4vJ5wGxiHF3fjaoEm4oLg=",
                "Version": "$LATEST",
                "VpcConfig": {
                    "SubnetIds": [],
                    "SecurityGroupIds": [],
                    "VpcId": ""
                },
                "TracingConfig": {
                    "Mode": "PassThrough"
                },
                "RevisionId": "2f475489-e997-4225-8d36-151ef5e350d7"
            },
            {
                "FunctionName": "ReadFsharp",
                "FunctionArn": "arn:aws:lambda:eu-west-1:532053120208:function:ReadFsharp:1",
                "Runtime": "dotnetcore2.1",
                "Role": "arn:aws:iam::532053120208:role/service-role/ReadFsharpRole",
                "Handler": "ReadFsharp::Setup+LambdaEntryPoint::FunctionHandlerAsync",
                "CodeSize": 12459694,
                "Description": "1.0.0",
                "Timeout": 15,
                "MemorySize": 512,
                "LastModified": "2019-11-29T23:12:45.862+0000",
                "CodeSha256": "zLxWyAPYHmSKcB3itCJWob4vJ5wGxiHF3fjaoEm4oLg=",
                "Version": "1",
                "VpcConfig": {
                    "SubnetIds": [],
                    "SecurityGroupIds": [],
                    "VpcId": ""
                },
                "TracingConfig": {
                    "Mode": "PassThrough"
                },
                "RevisionId": "8f8f1951-c4a8-4759-a959-682392410006"
            }
        ]
    }"""

type LambdaVersions = JsonProvider<AwsLambdaVersionResponse>



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

Target.create "PublishVersion" (fun _ -> 
    CreateProcess.fromRawCommandLine "aws" "lambda publish-version --function-name ReadFsharp --region eu-west-1"
    |> CreateProcess.withWorkingDirectory readFsharpProjectDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore)  

Target.create "ListVersion" (fun _ -> 
  Shell.Exec
    ("aws", "lambda list-versions-by-function --function-name ReadFsharp --region eu-west-1", readFsharpProjectDir)
  |> fun x -> if x <> 0 then failwith "Failed...")    


Target.create "UpdateAliasToLatest" (fun _ -> 
    let x =       
      CreateProcess.fromRawCommandLine "aws" "lambda list-versions-by-function --function-name ReadFsharp --region eu-west-1"
      |> CreateProcess.withWorkingDirectory readFsharpProjectDir
      |> CreateProcess.redirectOutput
      |> Proc.run

    printfn "----VERISONS---\n%s\n-----"  x.Result.Output

    let x = LambdaVersions.Parse x.Result.Output

    let latestVersion = 
        x.Versions 
        |> Array.choose (fun x -> x.Version.Number) 
        |> Array.max
        
    printfn "Latest Version: %d" latestVersion

    sprintf "lambda update-alias --function-name ReadFsharp --function-version %d --name latest --region eu-west-1"  latestVersion
    |> CreateProcess.fromRawCommandLine "aws" 
    |> CreateProcess.withWorkingDirectory readFsharpProjectDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore )    

Target.create "UpdateProvisionedConcurrency" (fun _ ->      
        "lambda put-provisioned-concurrency-config --function-name ReadFsharp --qualifier latest --provisioned-concurrent-executions 1 --region eu-west-1"
      |> CreateProcess.fromRawCommandLine "aws" 
      |> CreateProcess.withWorkingDirectory readFsharpProjectDir
      |> CreateProcess.redirectOutput
      |> CreateProcess.ensureExitCode
      |> Proc.run 
      |> ignore)    

Target.create "All" ignore

"Clean"
  ==> "Restore"
  ==> "Build"
  ==> "Test"
  ==> "Deploy"
  ==> "PublishVersion"
  ==> "UpdateAliasToLatest"
  ==> "UpdateProvisionedConcurrency"
  ==> "All"

"Test"
  ==> "Package"

Target.runOrDefault "All"
