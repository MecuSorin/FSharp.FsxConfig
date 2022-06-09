#r "paket: groupref Build //"
#load "./.fake/build.fsx/intellisense.fsx"

open Fake.Api
open Fake.Core
open Fake.IO
open Fake.DotNet
open Fake.IO.FileSystemOperators
open Fake.Core.TargetOperators
open Fake.DotNet.Testing
open Fake.IO.Globbing.Operators
open Fake.Tools
open System
open System.IO

let concatPath pathTokens =
    let rec add accumulated = function
        | [] -> accumulated
        // | [a] -> System.IO.Path.Combine(accumulated, a)
        | head::tail -> add (System.IO.Path.Combine(accumulated, head)) tail
    add "" pathTokens

let project = "FSharp.FsxConfig"
let summary = "FSharp.FsxConfig provides a simple tool for using fsx files as configuration"
let configuration = "Release"
let solutionFile = "FSharp.FsxConfig.sln"
let libraryFile = "src/FSharp.FsxConfig/FSharp.FsxConfig.fsproj"

let srcCodeGlob =
    !! (__SOURCE_DIRECTORY__  </> "src/**/*.fs")
    ++ (__SOURCE_DIRECTORY__  </> "src/**/*.fsx")
    -- (__SOURCE_DIRECTORY__  </> "src/**/obj/**/*.fs")

let testsCodeGlob =
    !! (__SOURCE_DIRECTORY__  </> "tests/**/*.fs")
    ++ (__SOURCE_DIRECTORY__  </> "tests/**/*.fsx")
    -- (__SOURCE_DIRECTORY__  </> "tests/**/obj/**/*.fs")

let gitOwner ="mecumihaisorin"

let distDir = __SOURCE_DIRECTORY__  @@ "nuget_dist"
let distGlob = distDir @@ "*.nupkg"

let githubToken = Environment.environVarOrNone "GITHUB_TOKEN"
Option.iter(TraceSecrets.register "<GITHUB_TOKEN>" )

let nugetToken = Environment.environVarOrNone "NUGET_TOKEN"
Option.iter(TraceSecrets.register "<NUGET_TOKEN>")



let failOnBadExitAndPrint (p : ProcessResult) =
    if p.ExitCode <> 0 then
        p.Errors |> Seq.iter Trace.traceError
        failwithf "failed with exitcode %d" p.ExitCode
module dotnet =
    let watch cmdParam program args =
        DotNet.exec cmdParam (sprintf "watch %s" program) args

    let run cmdParam args =
        DotNet.exec cmdParam "run" args

    let tool optionConfig command args =
        DotNet.exec optionConfig (sprintf "%s" command) args
        |> failOnBadExitAndPrint

    let fantomas args =
        DotNet.exec id "fantomas" args


let formatCode _ =
    let result =
        [
            srcCodeGlob
            testsCodeGlob
        ]
        |> Seq.collect id
        // Ignore AssemblyInfo
        |> Seq.filter(fun f -> f.EndsWith("AssemblyInfo.fs") |> not)
        |> String.concat " "
        |> dotnet.fantomas

    if not result.OK then
        printfn "Errors while formatting all files: %A" result.Messages


let checkFormatCode _ =
    let result =
        [
            srcCodeGlob
            testsCodeGlob
        ]
        |> Seq.collect id
        // Ignore AssemblyInfo
        |> Seq.filter(fun f -> f.EndsWith("AssemblyInfo.fs") |> not)
        |> String.concat " "
        |> sprintf "%s --check"
        |> dotnet.fantomas

    if result.ExitCode = 0 then
        Trace.log "No files need formatting"
    elif result.ExitCode = 99 then
        failwith "Some files need formatting, check output for more info"
    else
        Trace.logf "Errors while formatting: %A" result.Errors


Target.create "Clean" (fun _ ->
  !! "bin"
  ++ "src/**/bin"
  ++ "tests/**/bin"
  ++ "src/**/obj"
  ++ "tests/**/obj"
  |> Shell.cleanDirs

  [
    "paket-files/paket.restore.cached"
  ]
  |> Seq.iter Shell.rm
)

Target.create "Build" (fun _ ->
  let setParams (defaults:DotNet.BuildOptions) =
        { defaults with
            NoRestore = true
            Configuration = DotNet.BuildConfiguration.fromString configuration}
  DotNet.build setParams solutionFile
)

Target.create "Restore" (fun _ ->
  Fake.DotNet.Paket.restore (fun p -> {p with ToolType = ToolType.CreateLocalTool()} )
  DotNet.restore id solutionFile
)

let runTestAssembly (testAssembly, args: string list option)=

    printfn "Should execute %s" testAssembly
    let exitCode =
        let parameters = Expecto.Params.DefaultParams
        let workingDir =
            if String.isNotNullOrEmpty parameters.WorkingDirectory
            then parameters.WorkingDirectory else Fake.IO.Path.getDirectory testAssembly
        let extraArgs =
            match args with
            | Some a -> a
            | None -> []
        Command.RawCommand("dotnet", Arguments.OfArgs [testAssembly; yield! extraArgs; parameters |> string ])
        |> CreateProcess.fromCommand
        |> CreateProcess.withWorkingDirectory workingDir
        |> Proc.run
        |> fun pr -> pr.ExitCode

    testAssembly, exitCode

let runTests testAssemblies =
    let details = testAssemblies |> Seq.map fst |> String.separated ", "
    use __ = Trace.traceTask "Expecto" details
    let res =
        testAssemblies
        |> Seq.map runTestAssembly
        |> Seq.filter( snd >> (<>) 0)
        |> Seq.toList

    match res with
    | [] -> ()
    | failedAssemblies ->
        failedAssemblies
        |> List.map (fun (testAssembly,exitCode) ->
            sprintf "Expecto test of assembly '%s' failed. Process finished with exit code %d." testAssembly exitCode )
        |> String.concat System.Environment.NewLine
        |> Fake.Testing.Common.FailedTestsException |> raise
    __.MarkSuccess()

let testProjects ()= [
    ["tests"; "FSharp.FsxConfig.Tests"], Some ["scriptpath>" + concatPath [".."; ".."; ".."]] //"tests"; "FSharp.FsxConfig.Tests"]]
]

let testAssemblies () = [
    for (proj, extraArgs) in testProjects () do
        let projName = proj |> List.last
        let pattern = concatPath proj </> "bin" </> configuration </> "**" </> (projName + ".dll")

        yield! (!!pattern |> Seq.map(fun x -> x, extraArgs))
]

Target.create "RunTests" (fun _ ->
    testAssemblies () |> runTests
)

let release = ReleaseNotes.load "RELEASE_NOTES.md"

Target.create "AssemblyInfo" (fun _ ->
    let getAssemblyInfoAttributes projectName =
        [ AssemblyInfo.Title (projectName)
          AssemblyInfo.Product project
          AssemblyInfo.Description summary
          AssemblyInfo.Version release.AssemblyVersion
          AssemblyInfo.FileVersion release.AssemblyVersion
          AssemblyInfo.Configuration configuration ]

    let getProjectDetails (projectPath: string) =
        let projectName = Path.GetFileNameWithoutExtension(projectPath)
        ( projectPath,
          projectName,
          Path.GetDirectoryName(projectPath),
          (getAssemblyInfoAttributes projectName)
        )

    !! "src/**/*.??proj"
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (_, _, folderName, attributes) ->
        AssemblyInfoFile.createFSharp (folderName </> "AssemblyInfo.fs") attributes)
)

let releaseNotes = String.toLines release.Notes
Target.create "NuGet" (fun _ ->
    [libraryFile]
    |> Seq.iter (
        DotNet.pack(fun p ->
           { p with
               // ./bin from the solution root matching the "PublishNuget" target WorkingDir
               OutputPath = Some distDir
               Configuration = DotNet.BuildConfiguration.Release
               MSBuildParams = {MSBuild.CliArguments.Create() with
                                    // "/p" (property) arguments to MSBuild.exe
                                    Properties = [("Version", release.NugetVersion)
                                                  ("PackageReadmeFile", "readme.md")
                                                  ("PackageProjectUrl", "https://github.com/MecuSorin/FSharp.FsxConfig")
                                                  ("PackageLicenseFile", "license")
                                                  ("PackageReleaseNotes", releaseNotes)]}}))
)


Target.create "FormatCode" formatCode
Target.create "CheckFormatCode" checkFormatCode

Target.create "PublishNuget" (fun _ ->
    Paket.push(fun p ->
        { p with
            ToolType = ToolType.CreateLocalTool()
            PublishUrl = "https://www.nuget.org"
            WorkingDir = distDir
            ApiKey =
              match nugetToken with
              | Some s -> s
              | _ -> p.ApiKey // assume paket-config was set properly
        }
    )
)

Target.create "GitRelease"(fun _ ->
    let remote = "origin"
    Git.Staging.stageAll ""
    Git.Commit.exec "" (sprintf "Bump version to %s\n\n%s" release.NugetVersion releaseNotes)
    Git.Branches.push ""

    Git.Branches.tag "" release.NugetVersion
    Git.Branches.pushTag "" remote release.NugetVersion
)

Target.create "GitHubRelease" (fun _ ->
    let token =
        match githubToken with
        | Some s -> s
        | _ -> failwith "please set the github_token environment variable to a github personal access token with repo access."

    let files = !! distGlob
    GitHub.createClientWithToken token
    |> GitHub.draftNewRelease gitOwner project release.NugetVersion (release.SemVer.PreRelease <> None) (releaseNotes |> Seq.singleton)
    |> GitHub.uploadFiles files
    |> GitHub.publishDraft
    |> Async.RunSynchronously
)

Target.create "Release" ignore

Target.create "UpdateDocs" (fun _ ->
    Git.Staging.stageAll ""
    Git.Commit.exec "" "update docs"
    Git.Branches.push ""
)

"UpdateDocs"

// *** Define Dependencies ***
"Clean"
  ==> "AssemblyInfo"
  ==> "Restore"
  ==> "Build"
  ==> "RunTests"
  ==> "NuGet"
  ==> "PublishNuGet"
//   ==> "GitRelease"
//   ==> "GitHubRelease"
//   ==> "Release"

// *** Start Build ***
Target.runOrDefaultWithArguments "RunTests"