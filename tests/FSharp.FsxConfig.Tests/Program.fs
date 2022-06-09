module Program

open Expecto

type MyConfiguration = {
    athing: int
    something: string
}
module MyConfiguration =
    let internal Default: MyConfiguration = { athing = 1; something = "default value" }

let workingTest pathPrefix = test "3 scripts are applied" {
    let applyScriptConfiguration path config =
        let scriptFile = System.IO.Path.Combine(pathPrefix, path)
        printfn "Using script: %s" scriptFile
        FSharp.FsxConfig.applyScriptConfiguration scriptFile config
    let actual =
        MyConfiguration.Default
        |> applyScriptConfiguration "changing athing copy.fsx"
        |> Result.bind (applyScriptConfiguration "changing_something.fsx")
        |> Result.bind (applyScriptConfiguration "changingathing.fsx")

    let expected = { athing = 21; something = "customized" } |> Ok

    Expect.equal actual expected "Should be the same"
}

[<EntryPoint>]
let main args =
    FSharp.FsxConfig.verbose <- true
    let pathPrefix =
        args
        |> Array.tryFind (fun x -> x.ToLower().StartsWith("scriptpath>"))
        |> function
            | Some scriptpath -> scriptpath.Substring("scriptpath>".Length)
            | None -> "."

    runTestsWithCLIArgs [] [||] (workingTest pathPrefix)